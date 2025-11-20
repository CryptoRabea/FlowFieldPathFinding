using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using System;


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
    [Header("Agent Properties")]
    [Tooltip("Movement speed in units per second")]
    public float speed = 5.0f;

    [Tooltip("How strongly agents avoid each other (0-1)")]
    public float avoidanceWeight = 0.5f;

    [Tooltip("How strongly agents follow flow field (0-1)")]
    public float flowFollowWeight = 1.0f;

    [Header("Rendering Setup")]
    [Tooltip("This GameObject must have MeshRenderer and MeshFilter components. " +
             "Entities.Graphics will automatically convert them for GPU instancing.")]
    public bool _renderingInfo = false;
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

        // Rendering is handled automatically by Entities.Graphics
        // The GameObject MUST have MeshRenderer and MeshFilter components
        // Entities.Graphics will bake them into ECS rendering components with GPU instancing
    }
}
