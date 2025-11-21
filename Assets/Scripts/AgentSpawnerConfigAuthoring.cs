using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Authoring component for agent spawner configuration singleton.
    /// Add to a GameObject in your scene to configure agent spawning.
    /// </summary>
    public class AgentSpawnerConfigAuthoring : MonoBehaviour
    {
        [Header("Agent Prefab")]
        [Tooltip("The agent prefab to instantiate (must have AgentRenderingAuthoring)")]
        public GameObject agentPrefab;

        [Header("Pool Settings")]
        [Tooltip("Total number of pre-allocated entities (max spawnable agents)")]
        public int poolSize = 20000;

        [Tooltip("Number of agents to spawn on startup")]
        public int initialSpawnCount = 5000;

        [Header("Spawn Area")]
        [Tooltip("Center of the spawn area in world space")]
        public Vector3 spawnCenter = new Vector3(0, 0, 0);

        [Tooltip("Radius of the spawn area")]
        public float spawnRadius = 20f;

        [Header("Default Agent Settings")]
        [Tooltip("Default agent speed")]
        public float defaultSpeed = 5f;

        [Tooltip("Default avoidance weight (0-1)")]
        [Range(0f, 1f)]
        public float defaultAvoidanceWeight = 0.5f;

        [Tooltip("Default flow follow weight (0-1)")]
        [Range(0f, 1f)]
        public float defaultFlowFollowWeight = 1.0f;

        private class Baker : Baker<AgentSpawnerConfigAuthoring>
        {
            public override void Bake(AgentSpawnerConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                // Bake the prefab reference
                var prefabEntity = GetEntity(authoring.agentPrefab, TransformUsageFlags.Dynamic);

                // Create agent spawner config singleton
                AddComponent(entity, new AgentSpawnerConfig
                {
                    AgentPrefab = prefabEntity,
                    PoolSize = authoring.poolSize,
                    InitialSpawnCount = authoring.initialSpawnCount,
                    SpawnCenter = authoring.spawnCenter,
                    SpawnRadius = authoring.spawnRadius,
                    DefaultSpeed = authoring.defaultSpeed,
                    DefaultAvoidanceWeight = authoring.defaultAvoidanceWeight,
                    DefaultFlowFollowWeight = authoring.defaultFlowFollowWeight,
                    SpawnRequested = false,
                    SpawnCount = 0,
                    ActiveCount = 0
                });
            }
        }
    }
}
