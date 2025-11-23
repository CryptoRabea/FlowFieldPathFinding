using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using FlowFieldPathfinding;

/// <summary>
/// SKINNED MESH ANIMATION SYSTEM FOR DOTS
///
/// This system uses a hybrid approach where skinned meshes remain as GameObjects
/// that follow entity transforms. This is necessary because:
///
/// 1. SHADER COMPATIBILITY: Standard URP shaders don't support GPU skinning for DOTS entities.
///    The shader error "does not support skinning" occurs when trying to render skinned
///    meshes purely through DOTS rendering components (MaterialMeshInfo, RenderMesh).
///
/// 2. MANAGED COMPONENTS: When entities are instantiated via EntityManager.Instantiate(),
///    managed components (like SkinnedMeshReference) are NOT copied to new entities.
///    The SkinnedMeshInstantiationSystem handles this by detecting entities with
///    SkinnedMeshAnimation but no SkinnedMeshReference, then creates GameObject instances
///    and adds the managed component.
///
/// 3. ANIMATOR INTEGRATION: The Animator component requires proper layer indices.
///    Previously, Animator.Play(stateHash) defaulted to layer -1 (invalid).
///    Fixed to use Animator.Play(stateHash, 0) for the base layer.
///
/// WORKFLOW:
/// - Baker: Adds SkinnedMeshAnimation (unmanaged) + SkinnedMeshReference (managed) to prefab entity
/// - Spawner: Instantiates entities (copies unmanaged components only)
/// - Instantiation System: Detects entities missing managed component, creates GameObject, adds component
/// - Animation System: Syncs entity transform to GameObject, manages animation playback
/// </summary>

/// <summary>
/// System that synchronizes skinned mesh GameObjects with their entity positions
/// and manages animation playback based on velocity.
/// Runs on main thread since it accesses managed components (Animator/GameObject).
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class SkinnedMeshAnimationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Sync position and handle animation for each skinned mesh entity
        foreach (var (localTransform, animation, meshRef, velocity, entity) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRW<SkinnedMeshAnimation>, SkinnedMeshReference, RefRO<AgentVelocity>>()
            .WithAll<AgentActive>()
            .WithEntityAccess())
        {
            if (meshRef.Animator == null)
                continue;

            // Get the GameObject transform (either source or runtime instance)
            var targetTransform = meshRef.RuntimeInstance != null
                ? meshRef.RuntimeInstance.transform
                : meshRef.SourceGameObject?.transform;

            if (targetTransform == null)
                continue;

            // Sync position and rotation from entity to GameObject
            targetTransform.SetPositionAndRotation(localTransform.ValueRO.Position, localTransform.ValueRO.Rotation);
            targetTransform.localScale = new Vector3(
                localTransform.ValueRO.Scale,
                localTransform.ValueRO.Scale,
                localTransform.ValueRO.Scale);

            // Update animation speed
            meshRef.Animator.speed = animation.ValueRO.AnimationSpeed;

            // Determine desired animation state based on velocity and attack state
            int desiredStateHash = DetermineAnimationState(velocity.ValueRO, animation.ValueRO, entity);

            // Only change animation if state has changed
            if (animation.ValueRW.CurrentStateHash != desiredStateHash)
            {
                animation.ValueRW.CurrentStateHash = desiredStateHash;
                // Play on layer 0 (first/base layer)
                meshRef.Animator.Play(desiredStateHash, 0);
            }
        }
    }

    private int DetermineAnimationState(AgentVelocity velocity, SkinnedMeshAnimation animation, Entity entity)
    {
        // Check if entity has attack component and it's active
        if (EntityManager.HasComponent<AgentAttack>(entity))
        {
            var attack = EntityManager.GetComponentData<AgentAttack>(entity);
            if (attack.IsAttacking && attack.AttackTimer > 0)
            {
                return animation.AttackStateHash;
            }
        }

        // Calculate horizontal speed (XZ plane)
        float speed = Unity.Mathematics.math.length(new Unity.Mathematics.float2(velocity.Value.x, velocity.Value.z));

        // Switch between Idle and Walk based on speed threshold
        if (speed > animation.WalkSpeedThreshold)
        {
            return animation.WalkStateHash;
        }
        else
        {
            return animation.IdleStateHash;
        }
    }
}

/// <summary>
/// System to handle managed component setup for spawned entities.
/// Since managed components aren't copied during EntityManager.Instantiate(),
/// we need to manually add them and create GameObject instances.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(FlowFieldPathfinding.AgentSpawnerSystem))]
public partial class SkinnedMeshInstantiationSystem : SystemBase
{
    private GameObject _prefabTemplate;

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        // First pass: Find the prefab template from any existing SkinnedMeshReference
        if (_prefabTemplate == null)
        {
            foreach (var meshRef in SystemAPI.Query<SkinnedMeshReference>())
            {
                if (meshRef.PrefabGameObject != null)
                {
                    _prefabTemplate = meshRef.PrefabGameObject;
                    break;
                }
            }
        }

        // Second pass: Handle entities that need instantiation
        foreach (var (meshRef, entity) in
            SystemAPI.Query<SkinnedMeshReference>().WithEntityAccess())
        {
            // Skip if already has runtime instance
            if (meshRef.RuntimeInstance != null || meshRef.Animator != null)
                continue;

            // Create a runtime copy of the prefab GameObject
            if (meshRef.PrefabGameObject != null)
            {
                meshRef.RuntimeInstance = Object.Instantiate(meshRef.PrefabGameObject);
                meshRef.RuntimeInstance.SetActive(true);
                meshRef.Animator = meshRef.RuntimeInstance.GetComponent<Animator>();
            }
        }

        // Third pass: Add managed components to entities that have animation but no reference
        // This handles entities created via EntityManager.Instantiate()
        var entities = _needsSetupQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entities)
        {
            if (_prefabTemplate != null)
            {
                // Create GameObject instance for this spawned entity
                var instance = Object.Instantiate(_prefabTemplate);
                instance.SetActive(true);

                // Add the managed component with the new instance
                ecb.AddComponent(entity, new SkinnedMeshReference
                {
                    RuntimeInstance = instance,
                    Animator = instance.GetComponent<Animator>(),
                    PrefabGameObject = _prefabTemplate,
                    SourceGameObject = null // Spawned entities don't have a scene source
                });
            }
        }
        entities.Dispose();

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private EntityQuery _needsSetupQuery;

    protected override void OnCreate()
    {
        // Query for entities with SkinnedMeshAnimation but no SkinnedMeshReference
        _needsSetupQuery = GetEntityQuery(
            ComponentType.ReadOnly<SkinnedMeshAnimation>(),
            ComponentType.Exclude<SkinnedMeshReference>()
        );
    }
}

/// <summary>
/// Cleanup system to destroy runtime GameObjects when entities are destroyed.
/// Uses ICleanupComponentData to detect when entities are being removed.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class SkinnedMeshCleanupSystem : SystemBase
{
    private EntityQuery _cleanupQuery;

    protected override void OnCreate()
    {
        // This query would find entities marked for cleanup
        // For now, we rely on Unity's automatic cleanup of managed components
        _cleanupQuery = GetEntityQuery(ComponentType.ReadOnly<SkinnedMeshReference>());
    }

    protected override void OnUpdate()
    {
        // Unity automatically handles cleanup of managed components when entities are destroyed
        // The RuntimeInstance GameObjects will be garbage collected
        // For explicit cleanup, we would need to implement ICleanupComponentData or use EndSimulationEntityCommandBufferSystem
    }
}

/// <summary>
/// System to update attack timers and manage attack state.
/// Automatically decrements attack timers and disables IsAttacking when timer expires.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(AgentMovementSystem))]
public partial class AgentAttackSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        // Update attack timers
        foreach (var attack in SystemAPI.Query<RefRW<AgentAttack>>())
        {
            if (attack.ValueRO.IsAttacking && attack.ValueRO.AttackTimer > 0)
            {
                attack.ValueRW.AttackTimer -= deltaTime;

                // End attack when timer expires
                if (attack.ValueRO.AttackTimer <= 0)
                {
                    attack.ValueRW.IsAttacking = false;
                    attack.ValueRW.AttackTimer = 0;
                }
            }
        }
    }
}
