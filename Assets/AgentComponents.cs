using Unity.Entities;
using Unity.Mathematics;

// ============================================================================
// CORE AGENT COMPONENTS
// ============================================================================

/// <summary>
/// Marks an entity as an AI agent that follows flow fields.
/// Using an empty tag component allows efficient filtering in queries.
/// </summary>
public struct Agent : IComponentData
{
    public float Speed;              // Max movement speed (units/sec)
    public float AvoidanceWeight;    // How strongly to avoid neighbors (0-1)
    public float FlowFollowWeight;   // How strongly to follow flow field (0-1)
}

/// <summary>
/// Agent's current velocity. Separated from position (LocalTransform)
/// for cleaner update logic and better cache coherency.
/// </summary>
public struct AgentVelocity : IComponentData
{
    public float3 Value;
}

/// <summary>
/// Tracks which flow field cell this agent occupies.
/// Updated each frame to sample the correct flow direction.
/// Stored as 1D index for faster lookups: index = y * gridWidth + x
/// </summary>
public struct AgentCellIndex : IComponentData
{
    public int Value;
}

/// <summary>
/// Controls agent lifecycle for pooling. Instead of destroying/creating entities,
/// toggle this flag to reuse entities without structural changes.
/// Inactive agents are moved off-screen and skipped in movement updates.
/// </summary>
public struct AgentActive : IComponentData, IEnableableComponent
{
    // IEnableableComponent allows zero-cost enabling/disabling without structural changes
}

/// <summary>
/// Optional: Distance from camera for LOD. Agents far from camera can update
/// less frequently. Calculate once per frame in a separate system.
/// </summary>
public struct AgentDistanceToCamera : IComponentData
{
    public float Value;
}
