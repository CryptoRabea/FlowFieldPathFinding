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

    [Tooltip("Default animation state name to play")]
    public string defaultAnimationState = "Idle";

    [Tooltip("Animation speed multiplier")]
    public float animationSpeed = 1f;

    class Baker : Baker<SkinnedMeshAnimationAuthoring>
    {
        public override void Bake(SkinnedMeshAnimationAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add component to mark this entity as having a skinned mesh
            AddComponent(entity, new SkinnedMeshAnimation
            {
                AnimationSpeed = authoring.animationSpeed,
                DefaultStateHash = Animator.StringToHash(authoring.defaultAnimationState)
            });

            // Add managed component reference to keep the GameObject alive
            AddComponentObject(entity, new SkinnedMeshReference
            {
                Animator = authoring.animator != null ? authoring.animator : authoring.GetComponent<Animator>(),
                SourceGameObject = authoring.gameObject
            });
        }
    }
}

/// <summary>
/// Unmanaged component for animation data
/// </summary>
public struct SkinnedMeshAnimation : IComponentData
{
    public float AnimationSpeed;
    public int DefaultStateHash;
    public int CurrentStateHash;
    public float NormalizedTime;
}

/// <summary>
/// Managed component to hold reference to the Animator (hybrid approach)
/// </summary>
public class SkinnedMeshReference : IComponentData
{
    public Animator Animator;
    public GameObject SourceGameObject;
    public GameObject RuntimeInstance; // Instantiated at runtime for pooled entities
}
