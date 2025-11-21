using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// MonoBehaviour controller for runtime management of the flow field and agent spawning.
    ///
    /// Features:
    /// - Set target position to update agent destination
    /// - Spawn/despawn agents dynamically
    /// - Query active agent count
    /// - Toggle debug visualization
    ///
    /// Usage:
    /// 1. Add FlowFieldConfigAuthoring and AgentSpawnerConfigAuthoring to GameObjects in scene
    /// 2. Attach this script to any GameObject
    /// 3. Use public methods or UI to control the system
    /// </summary>
    public class FlowFieldBootstrap : MonoBehaviour
    {
        [Header("Runtime Controls")]
        [Tooltip("Target position for agents to move toward")]
        public Vector3 targetPosition = new Vector3(50, 0, 50);

        [Tooltip("Update target when this field changes")]
        public bool updateTargetOnChange = true;

        [Header("Spawn Controls")]
        [Tooltip("Number of agents to spawn when SpawnAgents() is called")]
        public int spawnCount = 1000;

        [Header("Debug Visualization")]
        [Tooltip("Show flow field direction gizmos")]
        public bool showFlowField = false;

        [Tooltip("Flow field visualization cell stride (1 = all cells, 2 = every other cell, etc.)")]
        public int flowFieldStride = 4;

        [Tooltip("Length of flow direction arrows")]
        public float arrowLength = 1.5f;

        private EntityManager _entityManager;
        private Vector3 _lastTargetPosition;

        private void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _lastTargetPosition = targetPosition;

            // Set initial target
            SetTargetPosition(targetPosition);
        }

        private void Update()
        {
            // Check if target position changed
            if (updateTargetOnChange && !targetPosition.Equals(_lastTargetPosition))
            {
                SetTargetPosition(targetPosition);
                _lastTargetPosition = targetPosition;
            }
        }

        /// <summary>
        /// Set the target position for agents to move toward.
        /// This triggers flow field regeneration.
        /// </summary>
        public void SetTargetPosition(Vector3 position)
        {
            var query = _entityManager.CreateEntityQuery(typeof(FlowFieldTarget));
            if (query.TryGetSingleton<FlowFieldTarget>(out var target))
            {
                target.Position = position;
                target.HasChanged = true;
                query.SetSingleton(target);

                Debug.Log($"[FlowFieldBootstrap] Target position set to {position}");
            }
            else
            {
                Debug.LogWarning("[FlowFieldBootstrap] FlowFieldTarget singleton not found. Add FlowFieldConfigAuthoring to scene.");
            }
        }

        /// <summary>
        /// Spawn additional agents from the pool.
        /// </summary>
        public void SpawnAgents(int count)
        {
            var query = _entityManager.CreateEntityQuery(typeof(AgentSpawnerConfig));
            if (query.TryGetSingleton<AgentSpawnerConfig>(out var config))
            {
                config.SpawnRequested = true;
                config.SpawnCount = count;
                query.SetSingleton(config);

                Debug.Log($"[FlowFieldBootstrap] Requested spawn of {count} agents");
            }
            else
            {
                Debug.LogWarning("[FlowFieldBootstrap] AgentSpawnerConfig singleton not found. Add AgentSpawnerConfigAuthoring to scene.");
            }
        }

        /// <summary>
        /// Spawn agents using the count specified in the inspector.
        /// </summary>
        public void SpawnAgents()
        {
            SpawnAgents(spawnCount);
        }

        /// <summary>
        /// Get the current number of active agents.
        /// </summary>
        public int GetActiveAgentCount()
        {
            var query = _entityManager.CreateEntityQuery(typeof(AgentSpawnerConfig));
            if (query.TryGetSingleton<AgentSpawnerConfig>(out var config))
            {
                return config.ActiveCount;
            }
            return 0;
        }

        /// <summary>
        /// Get the maximum pool size.
        /// </summary>
        public int GetPoolSize()
        {
            var query = _entityManager.CreateEntityQuery(typeof(AgentSpawnerConfig));
            if (query.TryGetSingleton<AgentSpawnerConfig>(out var config))
            {
                return config.PoolSize;
            }
            return 0;
        }

        private void OnDrawGizmos()
        {
            if (!showFlowField || !Application.isPlaying)
                return;

            // Find flow field entity
            var query = _entityManager.CreateEntityQuery(typeof(FlowFieldData));
            if (!query.TryGetSingleton<FlowFieldData>(out var flowFieldData))
                return;

            // Find flow field entity to get buffers
            Entity flowFieldEntity = Entity.Null;
            foreach (var entity in query.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                flowFieldEntity = entity;
                break;
            }

            if (flowFieldEntity == Entity.Null)
                return;

            var directionBuffer = _entityManager.GetBuffer<FlowFieldDirectionBuffer>(flowFieldEntity);
            if (directionBuffer.Length == 0)
                return;

            // Draw flow field arrows
            for (int y = 0; y < flowFieldData.GridHeight; y += flowFieldStride)
            {
                for (int x = 0; x < flowFieldData.GridWidth; x += flowFieldStride)
                {
                    int index = y * flowFieldData.GridWidth + x;
                    if (index >= directionBuffer.Length)
                        continue;

                    float2 direction = directionBuffer[index].Value;

                    // Skip zero directions
                    if (math.lengthsq(direction) < 0.01f)
                        continue;

                    // Calculate cell center
                    float3 cellCenter = new float3(
                        flowFieldData.GridOrigin.x + (x + 0.5f) * flowFieldData.CellSize,
                        flowFieldData.GridOrigin.y + 0.5f,
                        flowFieldData.GridOrigin.z + (y + 0.5f) * flowFieldData.CellSize
                    );

                    float3 direction3D = new float3(direction.x, 0, direction.y);
                    float3 end = cellCenter + direction3D * arrowLength;

                    // Draw arrow
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(cellCenter, end);

                    // Draw arrowhead
                    float3 right = new float3(-direction3D.z, 0, direction3D.x) * 0.3f;
                    Gizmos.DrawLine(end, end - direction3D * 0.5f + right);
                    Gizmos.DrawLine(end, end - direction3D * 0.5f - right);
                }
            }

            // Draw target position
            var targetQuery = _entityManager.CreateEntityQuery(typeof(FlowFieldTarget));
            if (targetQuery.TryGetSingleton<FlowFieldTarget>(out var target))
            {
                Gizmos.color = Color.red;
                Vector3 targetPos = target.Position;
                Gizmos.DrawWireSphere(targetPos, 3f);
                Gizmos.DrawLine(targetPos, targetPos + Vector3.up * 5f);
            }

            // Draw grid bounds
            Gizmos.color = Color.yellow;
            float3 min = flowFieldData.GridOrigin;
            float3 max = flowFieldData.GridOrigin + new float3(
                flowFieldData.GridWidth * flowFieldData.CellSize,
                0,
                flowFieldData.GridHeight * flowFieldData.CellSize
            );

            Gizmos.DrawLine(new Vector3(min.x, 0, min.z), new Vector3(max.x, 0, min.z));
            Gizmos.DrawLine(new Vector3(max.x, 0, min.z), new Vector3(max.x, 0, max.z));
            Gizmos.DrawLine(new Vector3(max.x, 0, max.z), new Vector3(min.x, 0, max.z));
            Gizmos.DrawLine(new Vector3(min.x, 0, max.z), new Vector3(min.x, 0, min.z));
        }

        // UI Button callbacks
        public void OnSpawnButtonClicked()
        {
            SpawnAgents();
        }

        public void OnSetTargetButtonClicked()
        {
            SetTargetPosition(targetPosition);
        }

        // Keyboard shortcuts for testing
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Label($"Active Agents: {GetActiveAgentCount()} / {GetPoolSize()}");
            GUILayout.Label("Controls:");
            GUILayout.Label("  [Space] - Spawn agents");
            GUILayout.Label("  [T] - Set target to mouse position");
            GUILayout.Label("  [F] - Toggle flow field visualization");
            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Space)
                {
                    SpawnAgents();
                }
                else if (Event.current.keyCode == KeyCode.T)
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        targetPosition = hit.point;
                        SetTargetPosition(targetPosition);
                    }
                }
                else if (Event.current.keyCode == KeyCode.F)
                {
                    showFlowField = !showFlowField;
                }
            }
        }
    }
}
