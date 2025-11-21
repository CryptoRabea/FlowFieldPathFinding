using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// High-performance agent spawning system using entity pooling.
    /// Uses runtime GameObject prefab instantiation for rendering setup.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(FlowFieldGenerationSystem))]
    public partial class AgentSpawnerSystem : SystemBase
    {
        private bool _poolInitialized;
        private Unity.Mathematics.Random _random;
        private Entity _prefabEntity;

        protected override void OnCreate()
        {
            RequireForUpdate<AgentSpawnerConfig>();
            RequireForUpdate<AgentPrefabManaged>();
            _random = Unity.Mathematics.Random.CreateFromIndex((uint)System.DateTime.Now.Ticks);
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<AgentSpawnerConfig>();

            // Initialize pool on first run
            if (!_poolInitialized)
            {
                InitializePool(ref config);
                _poolInitialized = true;

                // Spawn initial agents if configured
                if (config.InitialSpawnCount > 0)
                {
                    SpawnAgents(config.InitialSpawnCount, ref config);
                    SystemAPI.SetSingleton(config);
                }
                return;
            }

            // Handle spawn requests
            if (config.SpawnRequested)
            {
                SpawnAgents(config.SpawnCount, ref config);
                config.SpawnRequested = false;
                SystemAPI.SetSingleton(config);
            }
        }

        private void InitializePool(ref AgentSpawnerConfig config)
        {
            // Get managed prefab
            var prefabManaged = SystemAPI.ManagedAPI.GetSingleton<AgentPrefabManaged>();
            if (prefabManaged.Prefab == null)
            {
                Debug.LogError("[AgentSpawnerSystem] Agent prefab is not assigned!");
                return;
            }

            // Get mesh and material from prefab
            var meshFilter = prefabManaged.Prefab.GetComponent<MeshFilter>();
            var meshRenderer = prefabManaged.Prefab.GetComponent<MeshRenderer>();

            if (meshFilter == null || meshRenderer == null)
            {
                Debug.LogError("[AgentSpawnerSystem] Prefab must have MeshFilter and MeshRenderer!");
                return;
            }

            var mesh = meshFilter.sharedMesh;
            var material = meshRenderer.sharedMaterial;

            // Create rendering setup
            var renderMeshDescription = new RenderMeshDescription(
                shadowCastingMode: UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows: true
            );

            var renderMeshArray = new RenderMeshArray(new[] { material }, new[] { mesh });
            var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);

            // Create archetype for pooled agents
            var archetype = EntityManager.CreateArchetype(
                typeof(Agent),
                typeof(AgentVelocity),
                typeof(AgentCellIndex),
                typeof(AgentActive),
                typeof(AgentPooled),
                typeof(LocalTransform),
                typeof(LocalToWorld)
            );

            // Pre-allocate all entities
            var entities = new NativeArray<Entity>(config.PoolSize, Allocator.Temp);
            EntityManager.CreateEntity(archetype, entities);

            // Initialize each entity
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];

                // Set agent parameters
                EntityManager.SetComponentData(entity, new Agent
                {
                    Speed = config.DefaultSpeed,
                    AvoidanceWeight = config.DefaultAvoidanceWeight,
                    FlowFollowWeight = config.DefaultFlowFollowWeight
                });

                EntityManager.SetComponentData(entity, new AgentVelocity { Value = float3.zero });
                EntityManager.SetComponentData(entity, new AgentCellIndex { Value = -1 });
                EntityManager.SetComponentData(entity, LocalTransform.FromPosition(new float3(0, -1000, 0)));

                // Add rendering components
                RenderMeshUtility.AddComponents(
                    entity,
                    EntityManager,
                    renderMeshDescription,
                    renderMeshArray,
                    materialMeshInfo
                );

                // Disable by default
                EntityManager.SetComponentEnabled<AgentActive>(entity, false);
            }

            entities.Dispose();
            Debug.Log($"[AgentSpawnerSystem] Initialized pool with {config.PoolSize} entities");
        }

        private void SpawnAgents(int count, ref AgentSpawnerConfig config)
        {
            using var queryBuilder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<AgentPooled, LocalTransform>()
                .WithDisabled<AgentActive>();
            var query = GetEntityQuery(queryBuilder);

            var entities = query.ToEntityArray(Allocator.Temp);

            int spawned = 0;
            int maxToSpawn = math.min(count, entities.Length);

            for (int i = 0; i < maxToSpawn; i++)
            {
                var entity = entities[i];

                float2 randomOffset = _random.NextFloat2Direction() * _random.NextFloat(0f, config.SpawnRadius);
                float3 spawnPosition = config.SpawnCenter + new float3(randomOffset.x, 0, randomOffset.y);

                EntityManager.SetComponentData(entity, LocalTransform.FromPosition(spawnPosition));
                EntityManager.SetComponentData(entity, new AgentVelocity { Value = float3.zero });
                EntityManager.SetComponentData(entity, new AgentCellIndex { Value = -1 });
                EntityManager.SetComponentEnabled<AgentActive>(entity, true);

                spawned++;
            }

            entities.Dispose();
            config.ActiveCount += spawned;

            if (spawned < count)
            {
                Debug.LogWarning($"[AgentSpawnerSystem] Could only spawn {spawned}/{count} agents. Pool exhausted.");
            }
            else
            {
                Debug.Log($"[AgentSpawnerSystem] Spawned {spawned} agents. Total active: {config.ActiveCount}");
            }
        }
    }
}
