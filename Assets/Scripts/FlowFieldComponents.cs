using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Managed component holding the agent prefab GameObject reference.
    /// Used for runtime instantiation of agents.
    /// </summary>
    public class AgentPrefabManaged : IComponentData
    {
        public GameObject Prefab;
    }

    /// <summary>
    /// Singleton component holding flow field grid metadata.
    /// Attached to a dedicated FlowFieldEntity.
    /// All buffers (cost, integration, direction) are attached to the same entity.
    /// </summary>
    public struct FlowFieldData : IComponentData
    {
        /// <summary>Grid width in cells</summary>
        public int GridWidth;

        /// <summary>Grid height in cells</summary>
        public int GridHeight;

        /// <summary>Size of each cell in world units</summary>
        public float CellSize;

        /// <summary>Bottom-left corner of the grid in world space</summary>
        public float3 GridOrigin;

        /// <summary>Current destination cell (2D grid coordinates)</summary>
        public int2 DestinationCell;

        /// <summary>Flag indicating flow field needs regeneration</summary>
        public bool NeedsUpdate;
    }

    /// <summary>
    /// Configuration singleton for flow field generation parameters.
    /// </summary>
    public struct FlowFieldConfig : IComponentData
    {
        /// <summary>Cost value for impassable cells (typically 255)</summary>
        public byte ObstacleCost;

        /// <summary>Cost value for free traversable cells (typically 1)</summary>
        public byte DefaultCost;

        /// <summary>Smoothing factor for flow directions (0-1). Higher = smoother paths</summary>
        public float DirectionSmoothFactor;

        /// <summary>Grid width in cells</summary>
        public int GridWidth;

        /// <summary>Grid height in cells</summary>
        public int GridHeight;

        /// <summary>Size of each cell in world units</summary>
        public float CellSize;

        /// <summary>Bottom-left corner of the grid in world space</summary>
        public float3 GridOrigin;
    }

    /// <summary>
    /// Target destination singleton for flow field pathfinding.
    /// When Position changes, flow field regenerates.
    /// </summary>
    public struct FlowFieldTarget : IComponentData
    {
        /// <summary>Target position in world space</summary>
        public float3 Position;

        /// <summary>Flag set when target changes, triggering regeneration</summary>
        public bool HasChanged;
    }

    /// <summary>
    /// Dynamic buffer element for cost field.
    /// One cost value per grid cell (0-255, where 255 = impassable obstacle).
    /// 1D indexing: costBuffer[y * GridWidth + x]
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct FlowFieldCostBuffer : IBufferElementData
    {
        /// <summary>Traversal cost (0-255). 255 = obstacle, 1 = free cell</summary>
        public byte Value;
    }

    /// <summary>
    /// Dynamic buffer element for integration field.
    /// Stores cumulative distance from each cell to the destination.
    /// 65535 = unreachable cell.
    /// 1D indexing: integrationBuffer[y * GridWidth + x]
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct FlowFieldIntegrationBuffer : IBufferElementData
    {
        /// <summary>Integration value (distance to goal). 65535 = unreachable</summary>
        public ushort Value;
    }

    /// <summary>
    /// Dynamic buffer element for flow direction field.
    /// Stores normalized 2D direction vector pointing toward goal.
    /// (0,0) = no valid direction (unreachable or at destination).
    /// 1D indexing: directionBuffer[y * GridWidth + x]
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct FlowFieldDirectionBuffer : IBufferElementData
    {
        /// <summary>Normalized 2D direction vector (XZ plane). (0,0) if unreachable</summary>
        public float2 Value;
    }

    /// <summary>
    /// Component marking entities as obstacles in the flow field.
    /// FlowFieldGenerationSystem queries these to mark cells as impassable.
    /// </summary>
    public struct FlowFieldObstacle : IComponentData
    {
        /// <summary>Obstacle radius in world units</summary>
        public float Radius;
    }

    /// <summary>
    /// Singleton configuration for agent spawning system.
    /// </summary>
    public struct AgentSpawnerConfig : IComponentData
    {
        /// <summary>Prefab entity to instantiate for agents</summary>
        public Entity AgentPrefab;

        /// <summary>Total number of pre-allocated entities in the pool</summary>
        public int PoolSize;

        /// <summary>Number of agents to spawn initially</summary>
        public int InitialSpawnCount;

        /// <summary>Spawn area center in world space</summary>
        public float3 SpawnCenter;

        /// <summary>Spawn area radius</summary>
        public float SpawnRadius;

        /// <summary>Default agent speed</summary>
        public float DefaultSpeed;

        /// <summary>Default avoidance weight</summary>
        public float DefaultAvoidanceWeight;

        /// <summary>Default flow follow weight</summary>
        public float DefaultFlowFollowWeight;

        /// <summary>Flag to request spawning</summary>
        public bool SpawnRequested;

        /// <summary>Number of agents to spawn when SpawnRequested is true</summary>
        public int SpawnCount;

        /// <summary>Current number of active agents</summary>
        public int ActiveCount;
    }
}
