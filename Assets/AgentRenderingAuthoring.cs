using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

// ============================================================================
// AGENT RENDERING AUTHORING
// ============================================================================
// Converts GameObject prefabs to ECS entities with rendering components.
// Uses Entities.Graphics (aka Hybrid Renderer V2) for GPU instancing.
//
// Requirements:
// 1. Prefab must have MeshFilter and MeshRenderer components
// 2. Material must be compatible with SRP Batcher (URP/HDRP)
// 3. Bake creates RenderMeshUnmanaged and MaterialMeshInfo components
//
// Performance: GPU instancing allows rendering 10k+ agents with <1ms GPU time
// ============================================================================

public class AgentRenderingAuthoring : MonoBehaviour
{
    [Header("Rendering")]
    public Mesh mesh;
    public Material material;

    [Header("Agent Properties")]
    public float speed = 5.0f;
    public float avoidanceWeight = 0.5f;
    public float flowFollowWeight = 1.0f;
}

public class AgentRenderingBaker : Baker<AgentRenderingAuthoring>
{
    public override void Bake(AgentRenderingAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        // Add agent components
        AddComponent(entity, new Agent
        {
            Speed = authoring.speed,
            AvoidanceWeight = authoring.avoidanceWeight,
            FlowFollowWeight = authoring.flowFollowWeight
        });

        AddComponent(entity, new AgentVelocity { Value = float3.zero });
        AddComponent(entity, new AgentCellIndex { Value = -1 });
        AddComponent<AgentActive>(entity);
        AddComponent<AgentPooled>(entity);

        // Add rendering components (Entities.Graphics)
        // This enables GPU instancing automatically
        var renderMeshDescription = new RenderMeshDescription
        {
            FilterSettings = RenderFilterSettings.Default,
            LightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off
        };

        RenderMeshUtility.AddComponents(
            entity,
            this,
            renderMeshDescription,
            authoring.mesh,
            authoring.material
        );
    }
}
