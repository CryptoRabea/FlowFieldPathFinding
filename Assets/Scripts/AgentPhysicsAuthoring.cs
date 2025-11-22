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
    /// Supports:
    /// - Composite colliders (multiple child colliders combined)
    /// - Dynamic rigidbodies with mass and gravity
    /// - Full physics interactions with other objects
    ///
    /// ⚠️ PERFORMANCE WARNING:
    /// Physics simulation can be expensive with many agents.
    /// Use physics only if you need actual collision response and gravity.
    /// </summary>
    public class AgentPhysicsAuthoring : MonoBehaviour
    {
        [Header("Collider Settings")]
        [Tooltip("Use composite collider (combines child colliders)")]
        public bool useComposite = false;

        [Tooltip("Collider shape type (ignored if useComposite is true)")]
        public ColliderType colliderType = ColliderType.Box;

        [Tooltip("Size of the collider (for box) or radius (for sphere)")]
        public float3 size = new float3(1f, 1f, 1f);

        [Tooltip("Sphere radius (only used if colliderType is Sphere)")]
        public float radius = 0.5f;

        [Header("Rigidbody Settings")]
        [Tooltip("Use dynamic rigidbody (simulated by physics) instead of kinematic")]
        public bool isDynamic = true;

        [Tooltip("Mass of the rigidbody (kg)")]
        [Range(0.1f, 100f)]
        public float mass = 1f;

        [Tooltip("Linear damping (air resistance)")]
        [Range(0f, 10f)]
        public float linearDamping = 0.01f;

        [Tooltip("Angular damping (rotation resistance)")]
        [Range(0f, 10f)]
        public float angularDamping = 0.05f;

        [Tooltip("Use gravity")]
        public bool useGravity = true;

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

                if (authoring.useComposite)
                {
                    // Create composite collider from child colliders
                    colliderBlob = CreateCompositeCollider(authoring, filter, material);
                }
                else
                {
                    // Create single collider
                    colliderBlob = CreateSingleCollider(authoring, filter, material);
                }

                // Add physics collider component
                AddComponent(entity, new PhysicsCollider
                {
                    Value = colliderBlob
                });

                // Add physics velocity for dynamic bodies
                AddComponent(entity, new PhysicsVelocity());

                // Add mass properties
                if (authoring.isDynamic)
                {
                    // Create dynamic rigidbody with mass and gravity
                    var massProperties = colliderBlob.Value.MassProperties;

                    // Scale mass properties by desired mass
                    var physicsMass = PhysicsMass.CreateDynamic(massProperties, authoring.mass);

                    // Apply damping
                    physicsMass.InverseMass = 1.0f / authoring.mass;
                    physicsMass.AngularExpansionFactor = 0f; // Prevent rotation from expanding collider

                    AddComponent(entity, physicsMass);

                    // Add gravity factor
                    if (authoring.useGravity)
                    {
                        AddComponent(entity, new PhysicsGravityFactor { Value = 1f });
                    }
                    else
                    {
                        AddComponent(entity, new PhysicsGravityFactor { Value = 0f });
                    }

                    // Add damping
                    AddComponent(entity, new PhysicsDamping
                    {
                        Linear = authoring.linearDamping,
                        Angular = authoring.angularDamping
                    });
                }
                else
                {
                    // Create kinematic body (moved by code, not physics)
                    AddComponent(entity, PhysicsMass.CreateKinematic(
                        colliderBlob.Value.MassProperties));
                }
            }

            private BlobAssetReference<Unity.Physics.Collider> CreateSingleCollider(
                AgentPhysicsAuthoring authoring, CollisionFilter filter, Unity.Physics.Material material)
            {
                switch (authoring.colliderType)
                {
                    case ColliderType.Sphere:
                        return Unity.Physics.SphereCollider.Create(
                            new SphereGeometry
                            {
                                Center = float3.zero,
                                Radius = authoring.radius
                            },
                            filter,
                            material);

                    case ColliderType.Capsule:
                        return Unity.Physics.CapsuleCollider.Create(
                            new CapsuleGeometry
                            {
                                Vertex0 = new float3(0, -authoring.size.y * 0.5f + authoring.radius, 0),
                                Vertex1 = new float3(0, authoring.size.y * 0.5f - authoring.radius, 0),
                                Radius = authoring.radius
                            },
                            filter,
                            material);

                    case ColliderType.Box:
                    default:
                        return Unity.Physics.BoxCollider.Create(
                            new BoxGeometry
                            {
                                Center = float3.zero,
                                Orientation = quaternion.identity,
                                Size = authoring.size,
                                BevelRadius = 0.05f
                            },
                            filter,
                            material);
                }
            }

            private BlobAssetReference<Unity.Physics.Collider> CreateCompositeCollider(
                AgentPhysicsAuthoring authoring, CollisionFilter filter, Unity.Physics.Material material)
            {
                // Get all child colliders (excluding those on the authoring GameObject itself)
                var childColliders = new System.Collections.Generic.List<UnityEngine.Collider>();
                authoring.GetComponentsInChildren<UnityEngine.Collider>(childColliders);

                if (childColliders.Count == 0)
                {
                    // No child colliders found, create default single collider
                    UnityEngine.Debug.LogWarning($"[AgentPhysicsAuthoring] useComposite is true but no child colliders found on {authoring.name}. Creating single collider instead.");
                    return CreateSingleCollider(authoring, filter, material);
                }

                // Create list of child collider blobs
                var children = new System.Collections.Generic.List<CompoundCollider.ColliderBlobInstance>();

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

                // Convert List to NativeArray for CompoundCollider.Create
                var childrenArray = new Unity.Collections.NativeArray<CompoundCollider.ColliderBlobInstance>(
                    children.Count,
                    Unity.Collections.Allocator.Temp);

                for (int i = 0; i < children.Count; i++)
                {
                    childrenArray[i] = children[i];
                }

                // Create compound collider
                var compoundCollider = CompoundCollider.Create(childrenArray);

                // Dispose the native array
                childrenArray.Dispose();

                // Dispose child blobs (compound collider has copied the data)
                foreach (var child in children)
                {
                    child.Collider.Dispose();
                }

                return compoundCollider;
            }
        }
    }
}