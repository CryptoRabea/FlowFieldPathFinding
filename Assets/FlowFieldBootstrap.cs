using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// ============================================================================
// FLOW FIELD BOOTSTRAP
// ============================================================================
// MonoBehaviour to initialize ECS world and provide runtime controls.
// Attach this to a GameObject in your scene.
//
// Setup instructions:
// 1. Create empty GameObject, add this script
// 2. Create agent prefab: Cube with AgentRenderingAuthoring component
// 3. Assign prefab to this script's Inspector
// 4. Press Play and use GUI to spawn agents and set target
// ============================================================================

public class FlowFieldBootstrap : MonoBehaviour
{
    [Header("Agent Settings")]
    [Tooltip("Prefab with AgentRenderingAuthoring component")]
    public GameObject agentPrefab;

    [Tooltip("Total entities to pre-allocate in pool")]
    public int poolSize = 20000;

    [Tooltip("Agents to spawn at start (0 = manual spawn)")]
    public int initialSpawnCount = 5000;

    [Tooltip("Agent movement speed")]
    public float agentSpeed = 5.0f;

    [Header("Flow Field Settings")]
    [Tooltip("Grid width (cells)")]
    public int gridWidth = 100;

    [Tooltip("Grid height (cells)")]
    public int gridHeight = 100;

    [Tooltip("Cell size in world units")]
    public float cellSize = 2.0f;

    [Tooltip("Grid origin (bottom-left corner)")]
    public Vector3 gridOrigin = new Vector3(-100, 0, -100);

    [Header("Spawn Settings")]
    public Vector3 spawnCenter = Vector3.zero;
    public float spawnRadius = 50f;

    [Header("Target")]
    public Transform targetTransform;
    public Vector3 targetPosition = new Vector3(50, 0, 50);

    [Header("Debug Visualization")]
    public bool showFlowField = false;
    public bool showGrid = false;

    // Runtime state
    private EntityManager _entityManager;
    private Entity _configEntity;
    private Entity _targetEntity;
    private Entity _spawnerConfigEntity;
    private bool _initialized = false;

    // GUI state
    private int _spawnCount = 1000;
    private string _statsText = "";

    void Start()
    {
        InitializeECS();
    }

    void Update()
    {
        if (!_initialized)
            return;

        // Update target position from transform or manual setting
        if (targetTransform != null)
        {
            targetPosition = targetTransform.position;
        }

        // Update target in ECS
        if (_entityManager.Exists(_targetEntity))
        {
            var currentTarget = _entityManager.GetComponentData<FlowFieldTarget>(_targetEntity);
            if (math.distance(currentTarget.Position, targetPosition) > 0.1f)
            {
                _entityManager.SetComponentData(_targetEntity, new FlowFieldTarget
                {
                    Position = targetPosition,
                    HasChanged = true
                });
            }
        }

        // Update stats
        UpdateStats();
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        GUILayout.Box("Flow Field Pathfinding - DOTS");

        GUILayout.Label(_statsText);

        GUILayout.Space(10);
        GUILayout.Label("Spawn Controls:");
        _spawnCount = (int)GUILayout.HorizontalSlider(_spawnCount, 100, 5000);
        GUILayout.Label($"Spawn Count: {_spawnCount}");

        if (GUILayout.Button($"Spawn {_spawnCount} Agents"))
        {
            SpawnAgents(_spawnCount);
        }

        GUILayout.Space(10);
        GUILayout.Label("Target Position:");
        targetPosition.x = GUILayout.HorizontalSlider(targetPosition.x, gridOrigin.x, gridOrigin.x + gridWidth * cellSize);
        GUILayout.Label($"X: {targetPosition.x:F1}");
        targetPosition.z = GUILayout.HorizontalSlider(targetPosition.z, gridOrigin.z, gridOrigin.z + gridHeight * cellSize);
        GUILayout.Label($"Z: {targetPosition.z:F1}");

        GUILayout.Space(10);
        showFlowField = GUILayout.Toggle(showFlowField, "Show Flow Field Vectors");
        showGrid = GUILayout.Toggle(showGrid, "Show Grid");

        GUILayout.EndArea();
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            // Draw grid bounds in editor
            Gizmos.color = Color.yellow;
            Vector3 size = new Vector3(gridWidth * cellSize, 0.1f, gridHeight * cellSize);
            Vector3 center = gridOrigin + size * 0.5f;
            Gizmos.DrawWireCube(center, size);
            return;
        }

        if (!_initialized)
            return;

        // Draw target
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(targetPosition, 2f);

        // Draw grid
        if (showGrid)
        {
            DrawGrid();
        }

        // Draw flow field
        if (showFlowField)
        {
            DrawFlowField();
        }
    }

    private void InitializeECS()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        // Create flow field config singleton
        _configEntity = _entityManager.CreateEntity();
        _entityManager.AddComponentData(_configEntity, new FlowFieldConfig
        {
            GridWidth = gridWidth,
            GridHeight = gridHeight,
            CellSize = cellSize,
            GridOrigin = gridOrigin,
            ObstacleCost = 255,
            DefaultCost = 1,
            DirectionSmoothFactor = 0.5f
        });

        // Create target singleton
        _targetEntity = _entityManager.CreateEntity();
        _entityManager.AddComponentData(_targetEntity, new FlowFieldTarget
        {
            Position = targetPosition,
            HasChanged = true
        });

        // Create spawner config singleton (prefab will be set via authoring)
        _spawnerConfigEntity = _entityManager.CreateEntity();
        _entityManager.AddComponentData(_spawnerConfigEntity, new AgentSpawnerConfig
        {
            PoolSize = poolSize,
            InitialSpawnCount = initialSpawnCount,
            SpawnRadius = spawnRadius,
            SpawnCenter = spawnCenter,
            AgentSpeed = agentSpeed,
            AvoidanceWeight = 0.5f,
            FlowFollowWeight = 1.0f,
            PrefabEntity = Entity.Null, // Will be set by spawner when pool initializes
            SpawnRequested = false,
            SpawnCount = 0
        });

        _initialized = true;
        Debug.Log("Flow Field ECS initialized");
    }

    private void SpawnAgents(int count)
    {
        if (!_initialized || !_entityManager.Exists(_spawnerConfigEntity))
            return;

        var config = _entityManager.GetComponentData<AgentSpawnerConfig>(_spawnerConfigEntity);
        config.SpawnRequested = true;
        config.SpawnCount = count;
        _entityManager.SetComponentData(_spawnerConfigEntity, config);
    }

    private void UpdateStats()
    {
        if (!_initialized)
            return;

        // Query for spawner state
        var query = _entityManager.CreateEntityQuery(typeof(AgentSpawnerState));
        if (!query.IsEmpty)
        {
            var state = query.GetSingleton<AgentSpawnerState>();
            _statsText = $"FPS: {1.0f / Time.deltaTime:F1}\n" +
                        $"Active Agents: {state.ActiveCount}\n" +
                        $"Pool Size: {state.PoolSize}\n" +
                        $"Grid: {gridWidth}x{gridHeight} ({gridWidth * gridHeight} cells)";
        }
    }

    private void DrawGrid()
    {
        Gizmos.color = Color.gray;
        for (int x = 0; x <= gridWidth; x++)
        {
            Vector3 start = gridOrigin + new Vector3(x * cellSize, 0, 0);
            Vector3 end = gridOrigin + new Vector3(x * cellSize, 0, gridHeight * cellSize);
            Gizmos.DrawLine(start, end);
        }
        for (int z = 0; z <= gridHeight; z++)
        {
            Vector3 start = gridOrigin + new Vector3(0, 0, z * cellSize);
            Vector3 end = gridOrigin + new Vector3(gridWidth * cellSize, 0, z * cellSize);
            Gizmos.DrawLine(start, end);
        }
    }

    private void DrawFlowField()
    {
        // Query for flow field buffers
        var query = _entityManager.CreateEntityQuery(typeof(FlowFieldDirectionBuffer));
        if (query.IsEmpty)
            return;

        var entity = query.GetSingletonEntity();
        var directionBuffer = _entityManager.GetBuffer<FlowFieldDirectionBuffer>(entity);

        Gizmos.color = Color.cyan;
        for (int y = 0; y < gridHeight; y += 2) // Sample every 2 cells for performance
        {
            for (int x = 0; x < gridWidth; x += 2)
            {
                int index = y * gridWidth + x;
                if (index >= directionBuffer.Length)
                    continue;

                var direction = directionBuffer[index].Value;
                if (math.lengthsq(direction) < 0.01f)
                    continue;

                Vector3 cellCenter = gridOrigin + new Vector3((x + 0.5f) * cellSize, 0.5f, (y + 0.5f) * cellSize);
                Vector3 dir3D = new Vector3(direction.x, 0, direction.y);
                Gizmos.DrawRay(cellCenter, dir3D * cellSize * 0.8f);
            }
        }
    }
}
