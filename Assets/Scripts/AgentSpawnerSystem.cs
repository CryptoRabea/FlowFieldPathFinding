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
        private float _prefabScale;

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
                // Capture the prefab's scale to preserve it during spawning
                var prefabTransform = EntityManager.GetComponentData<LocalTransform>(_prefabEntity);
                _prefabScale = prefabTransform.Scale;
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
            Entity firstEntity = Entity.Null;

            for (int i = 0; i < count; i++)
            {
                // Instantiate prefab - all Agent component values come from the prefab
                var entity = EntityManager.Instantiate(_prefabEntity);

                if (i == 0)
                    firstEntity = entity;

                // Randomize spawn position within radius
                float2 randomOffset = _random.NextFloat2Direction() * _random.NextFloat(0f, config.SpawnRadius);
                float3 spawnPosition = config.SpawnCenter + new float3(randomOffset.x, 0, randomOffset.y);

                // Override only position-related components
                EntityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
                    spawnPosition, quaternion.identity, _prefabScale));
                EntityManager.SetComponentData(entity, new AgentVelocity { Value = float3.zero });
                EntityManager.SetComponentData(entity, new AgentCellIndex { Value = -1 });

                // Agent component (speed, weights) is inherited from prefab - no override needed
            }

            config.ActiveCount += count;

            // Debug: Log first agent's component values
            if (firstEntity != Entity.Null && EntityManager.HasComponent<Agent>(firstEntity))
            {
                var agent = EntityManager.GetComponentData<Agent>(firstEntity);
                Debug.Log($"[AgentSpawnerSystem] Spawned {count} agents. Total active: {config.ActiveCount}");
                Debug.Log($"[AgentSpawnerSystem] First agent - Speed: {agent.Speed}, FlowFollow: {agent.FlowFollowWeight}, Avoidance: {agent.AvoidanceWeight}, Cohesion: {agent.CohesionWeight}");
            }
            else
            {
                Debug.LogError($"[AgentSpawnerSystem] Spawned {count} agents but first agent has NO Agent component!");
                config.ActiveCount += count;
            }
        }
    }
}
