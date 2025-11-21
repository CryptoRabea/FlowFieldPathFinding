using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Authoring component for agent entities with GPU instanced rendering.
    ///
    /// Requirements:
    /// - Attach to a GameObject with MeshRenderer and MeshFilter
    /// - Material must be SRP Batcher compatible (URP/HDRP)
    /// - Entities.Graphics handles GPU instancing automatically
    ///
    /// Result: 10,000 agents rendered in ~10 draw calls vs 10,000 without instancing
    /// </summary>
    public class AgentRenderingAuthoring : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Maximum movement speed in units per second")]
        public float speed = 5.0f;

        [Header("Behavior Weights (0-1)")]
        [Tooltip("Weight for local avoidance. Higher = more separation from neighbors")]
        [Range(0f, 1f)]
        public float avoidanceWeight = 0.5f;

        [Tooltip("Weight for flow field following. Higher = stronger path adherence")]
        [Range(0f, 1f)]
        public float flowFollowWeight = 1.0f;

        private class Baker : Baker<AgentRenderingAuthoring>
        {
            public override void Bake(AgentRenderingAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Add agent component with settings
                AddComponent(entity, new Agent
                {
                    Speed = authoring.speed,
                    AvoidanceWeight = authoring.avoidanceWeight,
                    FlowFollowWeight = authoring.flowFollowWeight
                });

                // Add velocity component (initialized to zero)
                AddComponent(entity, new AgentVelocity
                {
                    Value = float3.zero
                });

                // Add cell index component (initialized to -1)
                AddComponent(entity, new AgentCellIndex
                {
                    Value = -1
                });

                // Add active tag (always enabled for instantiated entities)
                AddComponent(entity, new AgentActive());

                // Rendering is handled automatically by Entities.Graphics
                // The GameObject's MeshRenderer and MeshFilter are converted to:
                // - RenderMeshUnmanaged
                // - MaterialMeshInfo
                // - LocalToWorld (for GPU instancing)
            }
        }
    }
}
