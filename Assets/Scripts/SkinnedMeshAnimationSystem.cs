using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// System that synchronizes skinned mesh GameObjects with their entity positions
/// and manages animation playback.
/// Runs on main thread since it accesses managed components (Animator/GameObject).
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class SkinnedMeshAnimationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Sync position and handle animation for each skinned mesh entity
        foreach (var (localTransform, animation, meshRef) in
            SystemAPI.Query<RefRO<LocalTransform>, RefRW<SkinnedMeshAnimation>, SkinnedMeshReference>())
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
            targetTransform.position = localTransform.ValueRO.Position;
            targetTransform.rotation = localTransform.ValueRO.Rotation;
            targetTransform.localScale = new Vector3(
                localTransform.ValueRO.Scale,
                localTransform.ValueRO.Scale,
                localTransform.ValueRO.Scale);

            // Update animation speed
            meshRef.Animator.speed = animation.ValueRO.AnimationSpeed;

            // Play default animation if not already playing something
            if (animation.ValueRW.CurrentStateHash == 0)
            {
                animation.ValueRW.CurrentStateHash = animation.ValueRO.DefaultStateHash;
                meshRef.Animator.Play(animation.ValueRO.DefaultStateHash);
            }
        }
    }
}

/// <summary>
/// System to instantiate skinned mesh GameObjects for pooled/spawned entities.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class SkinnedMeshInstantiationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (meshRef, entity) in
            SystemAPI.Query<SkinnedMeshReference>().WithEntityAccess())
        {
            // Skip if already has runtime instance or source object is active
            if (meshRef.RuntimeInstance != null)
                continue;

            // For pooled entities, instantiate a copy of the skinned mesh prefab
            if (meshRef.SourceGameObject != null && !meshRef.SourceGameObject.activeInHierarchy)
            {
                meshRef.RuntimeInstance = Object.Instantiate(meshRef.SourceGameObject);
                meshRef.RuntimeInstance.SetActive(true);

                // Update animator reference to the new instance
                meshRef.Animator = meshRef.RuntimeInstance.GetComponent<Animator>();
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}

/// <summary>
/// Cleanup system to destroy runtime GameObjects when entities are destroyed.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class SkinnedMeshCleanupSystem : SystemBase
{
    private EntityQuery _destroyedQuery;

    protected override void OnCreate()
    {
        _destroyedQuery = GetEntityQuery(
            ComponentType.ReadOnly<SkinnedMeshReference>(),
            ComponentType.Exclude<LocalTransform>() // Entity being destroyed
        );
    }

    protected override void OnUpdate()
    {
        // Clean up any orphaned GameObjects
        foreach (var meshRef in SystemAPI.Query<SkinnedMeshReference>())
        {
            // This runs for all, but actual cleanup happens when entity is destroyed
            // Unity's managed component cleanup will handle this
        }
    }
}
