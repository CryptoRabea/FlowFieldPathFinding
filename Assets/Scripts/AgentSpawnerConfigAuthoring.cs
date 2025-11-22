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
        [Tooltip("The agent prefab to instantiate (must be set up for ECS baking)")]
        public GameObject agentPrefab;

        [Header("Spawn Settings")]
        [Tooltip("Number of agents to spawn on startup")]
        public int initialSpawnCount = 5000;

        [Tooltip("Center of the spawn area in world space")]
        public Vector3 spawnCenter = new Vector3(0, 0, 0);

        [Tooltip("Radius of the spawn area")]
        public float spawnRadius = 20f;

        [Header("Default Agent Settings")]
        [Tooltip("Default agent speed")]
        public float defaultSpeed = 5f;

        [Tooltip("Default avoidance weight (0-1) - prevents overlapping")]
        [Range(0f, 1f)]
        public float defaultAvoidanceWeight = 0.8f;

        [Tooltip("Default flow follow weight (0-1) - follows path")]
        [Range(0f, 1f)]
        public float defaultFlowFollowWeight = 0.7f;

        [Tooltip("Default cohesion weight (0-1) - groups together")]
        [Range(0f, 1f)]
        public float defaultCohesionWeight = 0.2f;

        private class Baker : Baker<AgentSpawnerConfigAuthoring>
        {
            public override void Bake(AgentSpawnerConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new AgentSpawnerConfig
                {
                    InitialSpawnCount = authoring.initialSpawnCount,
                    SpawnCenter = authoring.spawnCenter,
                    SpawnRadius = authoring.spawnRadius,
                    DefaultSpeed = authoring.defaultSpeed,
                    DefaultAvoidanceWeight = authoring.defaultAvoidanceWeight,
                    DefaultFlowFollowWeight = authoring.defaultFlowFollowWeight,
                    DefaultCohesionWeight = authoring.defaultCohesionWeight,
                    SpawnRequested = false,
                    SpawnCount = 0,
                    ActiveCount = 0
                });

                // Bake prefab as entity reference
                if (authoring.agentPrefab != null)
                {
                    var prefabEntity = GetEntity(authoring.agentPrefab, TransformUsageFlags.Dynamic);
                    AddComponent(entity, new AgentPrefabReference { Prefab = prefabEntity });
                }
            }
        }
    }
}
