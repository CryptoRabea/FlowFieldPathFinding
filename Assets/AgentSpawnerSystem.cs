using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

// ============================================================================
// AGENT SPAWNER SYSTEM WITH POOLING
// ============================================================================
// Creates a pool of agents at startup and enables/disables them as needed.
// This avoids structural changes (Entity create/destroy) during gameplay,
// which is critical for maintaining 60 FPS with thousands of agents.
//
// Design:
// - Pre-allocate N entities with Agent archetype at startup
// - Use IEnableableComponent (AgentActive) for zero-cost enable/disable
// - Disabled agents are moved off-screen (y = -1000) and skipped in queries
// - Spawning = enabling entity + setting position/velocity
// - Despawning = disabling entity
//
// Memory: ~100 bytes per agent (components + chunk overhead)
// 10k agents ≈ 1 MB, 100k agents ≈ 10 MB
// ============================================================================

/// <summary>
/// Configuration for agent spawner. Singleton component.
/// </summary>
public struct AgentSpawnerConfig : IComponentData
{
    public int PoolSize;              // Total entities to pre-allocate
    public int InitialSpawnCount;     // How many to spawn at start
    public float SpawnRadius;         // Spawn random position within this radius
    public float3 SpawnCenter;        // Center point for spawning
    public float AgentSpeed;          // Default agent speed
    public float AvoidanceWeight;     // Default avoidance strength
    public float FlowFollowWeight;    // Default flow following strength

    // Prefab entity to instantiate (must have rendering components)
    public Entity PrefabEntity;

    // Flag to trigger spawning
    public bool SpawnRequested;
    public int SpawnCount;
}

/// <summary>
/// Tracks the spawner state. Singleton.
/// </summary>
public struct AgentSpawnerState : IComponentData
{
    public int ActiveCount;
    public int PoolSize;
    public bool Initialized;
}

/// <summary>
/// Tag to mark entities in the pool.
/// </summary>
public struct AgentPooled : IComponentData { }

[BurstCompile]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct AgentSpawnerSystem : ISystem
{
    private EntityQuery _pooledAgentsQuery;
    private EntityQuery _activeAgentsQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<AgentSpawnerConfig>();

        // Query for pooled agents (both active and inactive)
        _pooledAgentsQuery = state.GetEntityQuery(
            ComponentType.ReadOnly<AgentPooled>(),
            ComponentType.ReadOnly<Agent>()
        );

        // Query for active agents only
        _activeAgentsQuery = state.GetEntityQuery(
            ComponentType.ReadOnly<Agent>(),
            ComponentType.ReadOnly<AgentActive>()
        );
    }

    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<AgentSpawnerConfig>();

        // Initialize pool on first run
        if (!SystemAPI.HasSingleton<AgentSpawnerState>())
        {
            InitializePool(ref state, config);
            return;
        }

        var spawnerState = SystemAPI.GetSingleton<AgentSpawnerState>();

        // Handle spawn requests
        if (config.SpawnRequested)
        {
            SpawnAgents(ref state, config, ref spawnerState);

            // Clear spawn request
            SystemAPI.SetSingleton(new AgentSpawnerConfig
            {
                PoolSize = config.PoolSize,
                InitialSpawnCount = config.InitialSpawnCount,
                SpawnRadius = config.SpawnRadius,
                SpawnCenter = config.SpawnCenter,
                AgentSpeed = config.AgentSpeed,
                AvoidanceWeight = config.AvoidanceWeight,
                FlowFollowWeight = config.FlowFollowWeight,
                PrefabEntity = config.PrefabEntity,
                SpawnRequested = false,
                SpawnCount = 0
            });

            SystemAPI.SetSingleton(spawnerState);
        }
    }

    private void InitializePool(ref SystemState state, AgentSpawnerConfig config)
    {
        // Create pool of entities
        var entities = new NativeArray<Entity>(config.PoolSize, Allocator.Temp);
        state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(
            typeof(Agent),
            typeof(AgentVelocity),
            typeof(AgentCellIndex),
            typeof(AgentActive),
            typeof(AgentPooled),
            typeof(LocalTransform),
            typeof(LocalToWorld)
        ), entities);

        // Initialize all entities as inactive
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];

            state.EntityManager.SetComponentData(entity, new Agent
            {
                Speed = config.AgentSpeed,
                AvoidanceWeight = config.AvoidanceWeight,
                FlowFollowWeight = config.FlowFollowWeight
            });

            state.EntityManager.SetComponentData(entity, new AgentVelocity { Value = float3.zero });
            state.EntityManager.SetComponentData(entity, new AgentCellIndex { Value = -1 });

            // Move off-screen
            state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(new float3(0, -1000, 0)));

            // Disable by default
            state.EntityManager.SetComponentEnabled<AgentActive>(entity, false);
        }

        entities.Dispose();

        // Create spawner state singleton
        var stateEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(stateEntity, new AgentSpawnerState
        {
            ActiveCount = 0,
            PoolSize = config.PoolSize,
            Initialized = true
        });

        UnityEngine.Debug.Log($"Agent pool initialized: {config.PoolSize} entities pre-allocated");

        // Spawn initial batch if requested
        if (config.InitialSpawnCount > 0)
        {
            var spawnerState = new AgentSpawnerState
            {
                ActiveCount = 0,
                PoolSize = config.PoolSize,
                Initialized = true
            };

            SpawnAgents(ref state, config, ref spawnerState);

            SystemAPI.SetSingleton(spawnerState);
        }
    }

    private void SpawnAgents(ref SystemState state, AgentSpawnerConfig config, ref AgentSpawnerState spawnerState)
    {
        int toSpawn = math.min(config.SpawnCount, spawnerState.PoolSize - spawnerState.ActiveCount);
        if (toSpawn <= 0)
        {
            UnityEngine.Debug.LogWarning($"Cannot spawn {config.SpawnCount} agents: pool full ({spawnerState.ActiveCount}/{spawnerState.PoolSize})");
            return;
        }

        // Get all pooled entities
        var allEntities = _pooledAgentsQuery.ToEntityArray(Allocator.Temp);

        int spawned = 0;
        var random = Unity.Mathematics.Random.CreateFromIndex((uint)UnityEngine.Time.frameCount);

        // Find inactive entities and activate them
        foreach (var entity in allEntities)
        {
            if (spawned >= toSpawn)
                break;

            if (!state.EntityManager.IsComponentEnabled<AgentActive>(entity))
            {
                // Generate random spawn position
                float angle = random.NextFloat(0, math.PI * 2);
                float radius = random.NextFloat(0, config.SpawnRadius);
                float3 offset = new float3(
                    math.cos(angle) * radius,
                    0,
                    math.sin(angle) * radius
                );
                float3 spawnPos = config.SpawnCenter + offset;

                // Set position
                state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(spawnPos));

                // Reset velocity
                state.EntityManager.SetComponentData(entity, new AgentVelocity { Value = float3.zero });

                // Enable entity
                state.EntityManager.SetComponentEnabled<AgentActive>(entity, true);

                spawned++;
            }
        }

        spawnerState.ActiveCount += spawned;
        allEntities.Dispose();

        UnityEngine.Debug.Log($"Spawned {spawned} agents ({spawnerState.ActiveCount}/{spawnerState.PoolSize} active)");
    }
}
