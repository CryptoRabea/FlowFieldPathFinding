using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Authoring component to add physics colliders to agent entities.
    /// Attach this to your agent prefab alongside AgentRenderingAuthoring.
    ///
    /// ⚠️ PERFORMANCE WARNING:
    /// For most flow field use cases, physics is NOT needed!
    /// The AgentMovementSystem handles all movement through transform updates.
    /// Adding physics components may REDUCE performance significantly.
    ///
    /// Only use physics if you need:
    /// - Collision detection with non-agent objects
    /// - Raycasting and overlap queries
    /// - Trigger events
    ///
    /// For 1000+ agents: Use AgentRenderingAuthoring WITHOUT this component
    /// for best performance (60+ FPS vs 10 FPS with physics).
    /// </summary>
    public class AgentPhysicsAuthoring : MonoBehaviour
    {
        [Header("Collider Settings")]
        [Tooltip("Collider shape type")]
        public ColliderType colliderType = ColliderType.Box;

        [Tooltip("Size of the collider (for box) or radius (for sphere)")]
        public float3 size = new float3(1f, 1f, 1f);

        [Tooltip("Sphere radius (only used if colliderType is Sphere)")]
        public float radius = 0.5f;

        [Header("Physics Material")]
        [Tooltip("Friction coefficient")]
        [Range(0f, 1f)]
        public float friction = 0.5f;

        [Tooltip("Restitution (bounciness)")]
        [Range(0f, 1f)]
        public float restitution = 0f;

        [Header("Collision Filter")]
        [Tooltip("Belongs to layer (bitmask)")]
        public uint belongsTo = 1;

        [Tooltip("Collides with layers (bitmask)")]
        public uint collidesWith = uint.MaxValue;

        public enum ColliderType
        {
            Box,
            Sphere,
            Capsule
        }

        private class Baker : Baker<AgentPhysicsAuthoring>
        {
            public override void Bake(AgentPhysicsAuthoring authoring)
            {
                // Skip if there's already a Rigidbody (Unity's RigidbodyBaker will handle physics)
                if (GetComponent<Rigidbody>() != null)
                    return;

                // Skip if there are Unity colliders on the same GameObject (to avoid conflicts with built-in bakers)
                // unless we're using composite mode (which uses child colliders)
                if (!authoring.useComposite && GetComponent<UnityEngine.Collider>() != null)
                {
                    UnityEngine.Debug.LogWarning($"[AgentPhysicsAuthoring] GameObject '{authoring.name}' has Unity colliders. " +
                        "Either enable 'useComposite' to combine child colliders, or remove Unity colliders to use AgentPhysicsAuthoring's built-in collider creation.");
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Create collision filter
                var filter = new CollisionFilter
                {
                    BelongsTo = authoring.belongsTo,
                    CollidesWith = authoring.collidesWith,
                    GroupIndex = 0
                };

                // Create physics material
                var material = new Unity.Physics.Material
                {
                    Friction = authoring.friction,
                    Restitution = authoring.restitution,
                    FrictionCombinePolicy = Unity.Physics.Material.CombinePolicy.GeometricMean,
                    RestitutionCombinePolicy = Unity.Physics.Material.CombinePolicy.Maximum,
                    CollisionResponse = CollisionResponsePolicy.Collide
                };

                BlobAssetReference<Unity.Physics.Collider> colliderBlob;

                switch (authoring.colliderType)
                {
                    case ColliderType.Sphere:
                        colliderBlob = Unity.Physics.SphereCollider.Create(
                            new SphereGeometry
                            {
                                Center = float3.zero,
                                Radius = authoring.radius
                            },
                            filter,
                            material);
                        break;

                    case ColliderType.Capsule:
                        colliderBlob = Unity.Physics.CapsuleCollider.Create(
                            new CapsuleGeometry
                            {
                                Vertex0 = new float3(0, -authoring.size.y * 0.5f + authoring.radius, 0),
                                Vertex1 = new float3(0, authoring.size.y * 0.5f - authoring.radius, 0),
                                Radius = authoring.radius
                            },
                            filter,
                            material);
                        break;

                    case ColliderType.Box:
                    default:
                        colliderBlob = Unity.Physics.BoxCollider.Create(
                            new BoxGeometry
                            {
                                Center = float3.zero,
                                Orientation = quaternion.identity,
                                Size = authoring.size,
                                BevelRadius = 0.05f
                            },
                            filter,
                            material);
                        break;
                }

<<<<<<< Updated upstream
            private BlobAssetReference<Unity.Physics.Collider> CreateCompositeCollider(
                AgentPhysicsAuthoring authoring, CollisionFilter filter, Unity.Physics.Material material)
            {
                // Get all child colliders (excluding those on the authoring GameObject itself)
                var childColliders = new System.Collections.Generic.List<UnityEngine.Collider>();
                authoring.GetComponentsInChildren<UnityEngine.Collider>(childColliders);

                if (childColliders.Count == 0)
=======
                // Add physics collider component
                AddComponent(entity, new PhysicsCollider
>>>>>>> Stashed changes
                {
                    Value = colliderBlob
                });

                // Add physics velocity for dynamic bodies (kinematic agents)
                AddComponent(entity, new PhysicsVelocity());

<<<<<<< Updated upstream
                foreach (var childCollider in childColliders)
                {
                    // Skip if disabled or on the authoring GameObject itself (to avoid conflicts with Unity's built-in bakers)
                    if (!childCollider.enabled || childCollider.gameObject == authoring.gameObject)
                        continue;

                    BlobAssetReference<Unity.Physics.Collider> childBlob = default;
                    var localToWorld = childCollider.transform.localToWorldMatrix;
                    var parentToWorld = authoring.transform.localToWorldMatrix;
                    var localTransform = parentToWorld.inverse * localToWorld;

                    float3 position = localTransform.GetPosition();
                    quaternion rotation = localTransform.rotation;

                    // Create collider based on type
                    if (childCollider is UnityEngine.BoxCollider boxCollider)
                    {
                        childBlob = Unity.Physics.BoxCollider.Create(
                            new BoxGeometry
                            {
                                Center = boxCollider.center,
                                Orientation = quaternion.identity,
                                Size = boxCollider.size,
                                BevelRadius = 0.05f
                            },
                            filter,
                            material);
                    }
                    else if (childCollider is UnityEngine.SphereCollider sphereCollider)
                    {
                        childBlob = Unity.Physics.SphereCollider.Create(
                            new SphereGeometry
                            {
                                Center = sphereCollider.center,
                                Radius = sphereCollider.radius
                            },
                            filter,
                            material);
                    }
                    else if (childCollider is UnityEngine.CapsuleCollider capsuleCollider)
                    {
                        float height = capsuleCollider.height;
                        float radius = capsuleCollider.radius;
                        float3 center = capsuleCollider.center;

                        // Calculate vertices based on capsule direction
                        float3 vertex0, vertex1;
                        switch (capsuleCollider.direction)
                        {
                            case 0: // X-axis
                                vertex0 = center + new float3(-height * 0.5f + radius, 0, 0);
                                vertex1 = center + new float3(height * 0.5f - radius, 0, 0);
                                break;
                            case 1: // Y-axis
                                vertex0 = center + new float3(0, -height * 0.5f + radius, 0);
                                vertex1 = center + new float3(0, height * 0.5f - radius, 0);
                                break;
                            case 2: // Z-axis
                            default:
                                vertex0 = center + new float3(0, 0, -height * 0.5f + radius);
                                vertex1 = center + new float3(0, 0, height * 0.5f - radius);
                                break;
                        }

                        childBlob = Unity.Physics.CapsuleCollider.Create(
                            new CapsuleGeometry
                            {
                                Vertex0 = vertex0,
                                Vertex1 = vertex1,
                                Radius = radius
                            },
                            filter,
                            material);
                    }
                    else
                    {
                        // Unsupported collider type, skip
                        UnityEngine.Debug.LogWarning($"[AgentPhysicsAuthoring] Unsupported collider type: {childCollider.GetType().Name} on {childCollider.name}");
                        continue;
                    }

                    if (childBlob.IsCreated)
                    {
                        children.Add(new CompoundCollider.ColliderBlobInstance
                        {
                            Collider = childBlob,
                            CompoundFromChild = new RigidTransform(rotation, position)
                        });
                    }
                }

                if (children.Count == 0)
                {
                    // No valid child colliders, create default
                    UnityEngine.Debug.LogWarning($"[AgentPhysicsAuthoring] No valid child colliders found on {authoring.name}. Creating single collider instead.");
                    return CreateSingleCollider(authoring, filter, material);
                }

                // Create compound collider
                var compoundCollider = CompoundCollider.Create(children);

                // Dispose child blobs (compound collider has copied the data)
                foreach (var child in children)
                {
                    child.Collider.Dispose();
                }

                return compoundCollider;
=======
                // Add mass properties for physics simulation
                // Using infinite mass makes this a kinematic body (moved by code, not physics)
                AddComponent(entity, PhysicsMass.CreateKinematic(
                    MassProperties.UnitSphere));
>>>>>>> Stashed changes
            }
        }
    }
}
