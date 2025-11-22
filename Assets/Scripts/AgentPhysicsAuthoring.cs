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

                // Add physics collider component
                AddComponent(entity, new PhysicsCollider
                {
                    Value = colliderBlob
                });

                // Add physics velocity for dynamic bodies (kinematic agents)
                AddComponent(entity, new PhysicsVelocity());

                // Add mass properties for physics simulation
                // Using infinite mass makes this a kinematic body (moved by code, not physics)
                AddComponent(entity, PhysicsMass.CreateKinematic(
                    MassProperties.UnitSphere));
            }
        }
    }
}
