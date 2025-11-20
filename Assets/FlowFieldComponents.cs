using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

// ============================================================================
// FLOW FIELD DATA COMPONENTS
// ============================================================================

/// <summary>
/// Singleton component holding the entire flow field grid data.
/// Stored as a singleton to avoid per-agent duplication.
/// All arrays use NativeArray for safe parallel access from Jobs.
/// </summary>
public struct FlowFieldData : IComponentData
{
    // Grid dimensions
    public int GridWidth;
    public int GridHeight;
    public float CellSize;           // World-space size of each cell (e.g., 2.0f)

    // Grid world-space bounds
    public float3 GridOrigin;        // Bottom-left corner of grid

    // Destination cell for pathfinding (agents try to reach this)
    public int2 DestinationCell;

    // Flag to trigger flow field regeneration when destination changes
    public bool NeedsUpdate;
}

/// <summary>
/// Holds the actual grid data buffers. Stored separately as a blob asset or
/// in a companion component, but here we use DynamicBuffer for flexibility.
///
/// Memory layout: all arrays are 1D, indexed as [y * GridWidth + x]
/// This layout is cache-friendly for sequential access patterns.
/// </summary>
[InternalBufferCapacity(0)] // Prevent stack allocation, always heap
public struct FlowFieldCostBuffer : IBufferElementData
{
    public byte Value; // Cost to traverse cell: 0=free, 255=obstacle, 1-254=varying cost
}

[InternalBufferCapacity(0)]
public struct FlowFieldIntegrationBuffer : IBufferElementData
{
    public ushort Value; // Cumulative cost from destination: 0=destination, 65535=unreachable
}

[InternalBufferCapacity(0)]
public struct FlowFieldDirectionBuffer : IBufferElementData
{
    public float2 Value; // Normalized flow direction vector for this cell
}

/// <summary>
/// Configuration singleton for flow field generation parameters.
/// </summary>
public struct FlowFieldConfig : IComponentData
{
    public int GridWidth;
    public int GridHeight;
    public float CellSize;
    public float3 GridOrigin;

    // Obstacle cost (typically 255 = impassable)
    public byte ObstacleCost;

    // Default traversal cost for open cells
    public byte DefaultCost;

    // Smoothing factor for flow directions (0-1, higher = smoother but less responsive)
    public float DirectionSmoothFactor;
}

/// <summary>
/// Tag to mark obstacle entities in the scene. The flow field generation
/// system will sample these positions to mark obstacle cells.
/// </summary>
public struct FlowFieldObstacle : IComponentData
{
    public float Radius; // Obstacle occupies all cells within this radius
}

/// <summary>
/// Singleton holding the target position that agents navigate toward.
/// Change this at runtime to make agents re-path.
/// </summary>
public struct FlowFieldTarget : IComponentData
{
    public float3 Position;
    public bool HasChanged; // Set true when position updates
}
