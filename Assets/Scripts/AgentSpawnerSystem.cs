using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Agent spawning system using entity instantiation from prefab.
    /// Lets Unity handle rendering automatically.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(FlowFieldGenerationSystem))]
    public partial class AgentSpawnerSystem : SystemBase
    {
        private bool _initialized;
        private Unity.Mathematics.Random _random;
        private Entity _prefabEntity;

        protected override void OnCreate()
        {
            RequireForUpdate<AgentSpawnerConfig>();
            RequireForUpdate<AgentPrefabReference>();
            _random = Unity.Mathematics.Random.CreateFromIndex((uint)System.DateTime.Now.Ticks);
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<AgentSpawnerConfig>();

            if (!_initialized)
            {
                _prefabEntity = SystemAPI.GetSingleton<AgentPrefabReference>().Prefab;
                _initialized = true;

                if (config.InitialSpawnCount > 0)
                {
                    SpawnAgents(config.InitialSpawnCount, ref config);
                    SystemAPI.SetSingleton(config);
                }
                return;
            }

            if (config.SpawnRequested)
            {
                SpawnAgents(config.SpawnCount, ref config);
                config.SpawnRequested = false;
                SystemAPI.SetSingleton(config);
            }
        }

        private void SpawnAgents(int count, ref AgentSpawnerConfig config)
        {
            for (int i = 0; i < count; i++)
            {
                var entity = EntityManager.Instantiate(_prefabEntity);

                float2 randomOffset = _random.NextFloat2Direction() * _random.NextFloat(0f, config.SpawnRadius);
                float3 spawnPosition = config.SpawnCenter + new float3(randomOffset.x, 0, randomOffset.y);

                EntityManager.SetComponentData(entity, LocalTransform.FromPosition(spawnPosition));
                EntityManager.SetComponentData(entity, new AgentVelocity { Value = float3.zero });
                EntityManager.SetComponentData(entity, new AgentCellIndex { Value = -1 });
                EntityManager.SetComponentData(entity, new Agent
                {
                    Speed = config.DefaultSpeed,
                    AvoidanceWeight = config.DefaultAvoidanceWeight,
                    FlowFollowWeight = config.DefaultFlowFollowWeight
                });
            }

            config.ActiveCount += count;
            Debug.Log($"[AgentSpawnerSystem] Spawned {count} agents. Total active: {config.ActiveCount}");
        }
    }
}
