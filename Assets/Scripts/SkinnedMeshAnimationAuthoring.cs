using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring component for entities with skinned mesh animation.
/// Uses hybrid rendering approach - the SkinnedMeshRenderer stays as a companion GameObject
/// that follows the entity's position.
/// </summary>
public class SkinnedMeshAnimationAuthoring : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("The Animator component (auto-detected if not set)")]
    public Animator animator;

    [Tooltip("Idle animation state name")]
    public string idleAnimationState = "Idle";

    [Tooltip("Walk/Run animation state name")]
    public string walkAnimationState = "Walk";

    [Tooltip("Attack animation state name")]
    public string attackAnimationState = "Attack";

    [Tooltip("Animation speed multiplier")]
    public float animationSpeed = 1f;

    [Tooltip("Minimum speed to trigger walk animation")]
    public float walkSpeedThreshold = 0.1f;

    class Baker : Baker<SkinnedMeshAnimationAuthoring>
    {
        public override void Bake(SkinnedMeshAnimationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add component to mark this entity as having a skinned mesh
            AddComponent(entity, new SkinnedMeshAnimation
            {
                AnimationSpeed = authoring.animationSpeed,
                IdleStateHash = Animator.StringToHash(authoring.idleAnimationState),
                WalkStateHash = Animator.StringToHash(authoring.walkAnimationState),
                AttackStateHash = Animator.StringToHash(authoring.attackAnimationState),
                CurrentStateHash = 0, // Will be set by the system
                WalkSpeedThreshold = authoring.walkSpeedThreshold
            });

            // Add managed component reference to keep the GameObject alive
            AddComponentObject(entity, new SkinnedMeshReference
            {
                Animator = authoring.animator != null ? authoring.animator : authoring.GetComponent<Animator>(),
                SourceGameObject = authoring.gameObject,
                PrefabGameObject = authoring.gameObject // Store for spawning copies
            });

            // IMPORTANT: Do not add rendering components (MaterialMeshInfo, RenderMesh, etc.)
            // The hybrid approach uses the GameObject's SkinnedMeshRenderer directly
            // This prevents the "shader does not support skinning" warning
        }
    }
}

/// <summary>
/// Unmanaged component for animation data
/// </summary>
public struct SkinnedMeshAnimation : IComponentData
{
    public float AnimationSpeed;
    public int IdleStateHash;
    public int WalkStateHash;
    public int AttackStateHash;
    public int CurrentStateHash;
    public float NormalizedTime;

    // Animation state thresholds
    public float WalkSpeedThreshold;  // Minimum speed to trigger walk animation
}

/// <summary>
/// Managed component to hold reference to the Animator (hybrid approach)
/// </summary>
public class SkinnedMeshReference : IComponentData
{
    public Animator Animator;
    public GameObject SourceGameObject; // Original GameObject from scene/prefab
    public GameObject RuntimeInstance; // Instantiated copy for spawned entities
    public GameObject PrefabGameObject; // Template for creating new instances
}
