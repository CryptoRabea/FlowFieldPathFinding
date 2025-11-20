using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// ============================================================================
// FLOW FIELD BOOTSTRAP AUTHORING
// ============================================================================
// Authoring component to bake flow field configuration into ECS.
// Attach to a GameObject in your SubScene or use the MonoBehaviour bootstrap.
// ============================================================================

public class FlowFieldConfigAuthoring : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridWidth = 100;
    public int gridHeight = 100;
    public float cellSize = 2.0f;
    public Vector3 gridOrigin = new Vector3(-100, 0, -100);

    [Header("Initial Target")]
    public Vector3 targetPosition = new Vector3(50, 0, 50);
}

public class FlowFieldConfigBaker : Baker<FlowFieldConfigAuthoring>
{
    public override void Bake(FlowFieldConfigAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new FlowFieldConfig
        {
            GridWidth = authoring.gridWidth,
            GridHeight = authoring.gridHeight,
            CellSize = authoring.cellSize,
            GridOrigin = authoring.gridOrigin,
            ObstacleCost = 255,
            DefaultCost = 1,
            DirectionSmoothFactor = 0.5f
        });

        AddComponent(entity, new FlowFieldTarget
        {
            Position = authoring.targetPosition,
            HasChanged = true
        });
    }
}

// ============================================================================
// SPAWNER CONFIG AUTHORING
// ============================================================================

public class AgentSpawnerConfigAuthoring : MonoBehaviour
{
    [Header("Pool Settings")]
    public int poolSize = 20000;
    public int initialSpawnCount = 5000;

    [Header("Spawn Area")]
    public Vector3 spawnCenter = Vector3.zero;
    public float spawnRadius = 50f;

    [Header("Agent Properties")]
    public float agentSpeed = 5.0f;
    public float avoidanceWeight = 0.5f;
    public float flowFollowWeight = 1.0f;
}

public class AgentSpawnerConfigBaker : Baker<AgentSpawnerConfigAuthoring>
{
    public override void Bake(AgentSpawnerConfigAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new AgentSpawnerConfig
        {
            PoolSize = authoring.poolSize,
            InitialSpawnCount = authoring.initialSpawnCount,
            SpawnRadius = authoring.spawnRadius,
            SpawnCenter = authoring.spawnCenter,
            AgentSpeed = authoring.agentSpeed,
            AvoidanceWeight = authoring.avoidanceWeight,
            FlowFollowWeight = authoring.flowFollowWeight,
            PrefabEntity = Entity.Null,
            SpawnRequested = false,
            SpawnCount = 0
        });
    }
}

// ============================================================================
// OBSTACLE AUTHORING
// ============================================================================

public class FlowFieldObstacleAuthoring : MonoBehaviour
{
    public float radius = 2.0f;
}

public class FlowFieldObstacleBaker : Baker<FlowFieldObstacleAuthoring>
{
    public override void Bake(FlowFieldObstacleAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent(entity, new FlowFieldObstacle
        {
            Radius = authoring.radius
        });
    }
}
