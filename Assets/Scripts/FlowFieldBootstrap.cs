using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// MonoBehaviour controller for runtime management of the flow field and agent spawning.
    ///
    /// Features:
    /// - Follow a target GameObject/Transform automatically
    /// - Set target position manually to update agent destination
    /// - Spawn/despawn agents dynamically
    /// - Query active agent count
    /// - Toggle debug visualization
    ///
    /// Usage:
    /// 1. Add FlowFieldConfigAuthoring and AgentSpawnerConfigAuthoring to GameObjects in scene
    /// 2. Attach this script to any GameObject
    /// 3. Assign a target object OR use manual position
    /// 4. Use public methods or UI to control the system
    /// </summary>
    public class FlowFieldBootstrap : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("Target object to follow (if assigned, overrides manual position)")]
        public Transform targetObject;

        [Tooltip("Use Y position from target object (if false, uses Y=0)")]
        public bool useTargetYPosition = false;

        [Tooltip("Update frequency when following target object (updates per second, 0 = every frame)")]
        [Range(0, 60)]
        public float targetUpdateRate = 10f;

        [Header("Manual Target Position")]
        [Tooltip("Manual target position (used when targetObject is not assigned)")]
        public Vector3 targetPosition = new Vector3(50, 0, 50);

        [Tooltip("Update target when manual position field changes")]
        public bool updateTargetOnChange = true;

        [Header("Dynamic Target Tracking")]
        [Tooltip("Optional: Assign a GameObject to track its position as the target")]
        public Transform dynamicTarget;

        [Tooltip("If true, agents will continuously follow the dynamicTarget's position")]
        public bool followDynamicTarget = false;

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
        private float _lastTargetUpdateTime;
        private bool _initialized = false;

        private void Start()
        {
            if (World.DefaultGameObjectInjectionWorld == null)
            {
                Debug.LogWarning("[FlowFieldBootstrap] ECS World not ready yet. Will initialize on first Update.");
                return;
            }

            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _lastTargetPosition = GetCurrentTargetPosition();
        }

        private float _initializationAttemptTime = 0f;
        private int _initializationAttempts = 0;

        private void Initialize()
        {
            if (_initialized) return;

            _initializationAttempts++;

            // Check if ECS world is ready
            if (World.DefaultGameObjectInjectionWorld == null)
            {
                if (Time.time - _initializationAttemptTime > 1f)
                {
                    Debug.LogWarning($"[FlowFieldBootstrap] ECS World not ready (attempt {_initializationAttempts})");
                    _initializationAttemptTime = Time.time;
                }
                return;
            }

            if (_entityManager == default)
                _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Debug: Check what entities exist
            if (Time.time - _initializationAttemptTime > 2f)
            {
                var allEntities = _entityManager.GetAllEntities(Unity.Collections.Allocator.Temp);
                Debug.Log($"[FlowFieldBootstrap] Waiting for baking... Total entities in world: {allEntities.Length} (attempt {_initializationAttempts})");

                // List all entity archetypes to see what's baked
                foreach (var entity in allEntities)
                {
                    var name = _entityManager.GetName(entity);
                    Debug.Log($"  - Entity: {name}");
                }
                allEntities.Dispose();

                _initializationAttemptTime = Time.time;
            }

            // Check if flow field config entity has been baked
            var query = _entityManager.CreateEntityQuery(typeof(FlowFieldTarget));
            if (!query.IsEmpty)
            {
                _initialized = true;
                _lastTargetPosition = GetCurrentTargetPosition();

                // Set initial target now that entities are baked
                SetTargetPosition(_lastTargetPosition);
                Debug.Log($"[FlowFieldBootstrap] Initialized successfully after {_initializationAttempts} attempts");
            }
            else
            {
                // Check for FlowFieldConfig as well
                var configQuery = _entityManager.CreateEntityQuery(typeof(FlowFieldConfig));
                if (Time.time - _initializationAttemptTime > 2f)
                {
                    Debug.LogWarning($"[FlowFieldBootstrap] FlowFieldTarget not found. FlowFieldConfig found: {!configQuery.IsEmpty}");
                }
            }
        }

        /// <summary>
        /// Get the current target position based on assigned target object or manual position.
        /// </summary>
        private Vector3 GetCurrentTargetPosition()
        {
            if (targetObject != null)
            {
                Vector3 pos = targetObject.position;
                if (!useTargetYPosition)
                {
                    pos.y = 0;
                }
                return pos;
            }
            return targetPosition;
        }

        /// <summary>
        /// Check if we should update the target this frame based on update rate.
        /// </summary>
        private bool ShouldUpdateTarget()
        {
            if (targetUpdateRate <= 0)
                return true; // Update every frame

            float timeSinceLastUpdate = Time.time - _lastTargetUpdateTime;
            return timeSinceLastUpdate >= (1f / targetUpdateRate);
        }

        private void Update()
        {
            // Initialize if not yet done (waits for baking to complete)
            if (!_initialized)
            {
                Initialize();
                return;
            }

            // Track dynamic target if enabled
            if (followDynamicTarget && dynamicTarget != null)
            {
                Vector3 dynamicPosition = dynamicTarget.position;
                // Always update when following a dynamic target (throttling happens in FlowFieldGenerationSystem)
                SetTargetPosition(dynamicPosition);
                _lastTargetPosition = dynamicPosition;
                targetPosition = dynamicPosition; // Update inspector field too
            }
            else
            {
                // Update target based on assigned object or manual position
                Vector3 currentTargetPos = GetCurrentTargetPosition();

                // If following a target object
                if (targetObject != null)
                {
                    // Check if position changed and update rate allows it
                    if (!currentTargetPos.Equals(_lastTargetPosition) && ShouldUpdateTarget())
                    {
                        SetTargetPosition(currentTargetPos);
                        _lastTargetPosition = currentTargetPos;
                        _lastTargetUpdateTime = Time.time;
                    }
                }
                // If using manual position and tracking changes
                else if (updateTargetOnChange && !currentTargetPos.Equals(_lastTargetPosition))
                {
                    SetTargetPosition(currentTargetPos);
                    _lastTargetPosition = currentTargetPos;
                }
            }
        }

        /// <summary>
        /// Set the target position for agents to move toward.
        /// This triggers flow field regeneration.
        /// </summary>
        public void SetTargetPosition(Vector3 position)
        {
            if (!_initialized)
            {
                Debug.LogWarning("[FlowFieldBootstrap] Not initialized yet. Waiting for baking to complete...");
                return;
            }

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
                Debug.LogWarning("[FlowFieldBootstrap] FlowFieldTarget singleton not found. Ensure FlowFieldConfigAuthoring is in scene and baking completed.");
            }
        }

        /// <summary>
        /// Set a new target object to follow.
        /// </summary>
        public void SetTargetObject(Transform target)
        {
            targetObject = target;
            if (target != null && _initialized)
            {
                _lastTargetPosition = GetCurrentTargetPosition();
                SetTargetPosition(_lastTargetPosition);
            }
        }

        /// <summary>
        /// Clear the target object and use manual position instead.
        /// </summary>
        public void ClearTargetObject()
        {
            targetObject = null;
        }

        /// <summary>
        /// Spawn additional agents from the pool.
        /// </summary>
        public void SpawnAgents(int count)
        {
            if (!_initialized)
            {
                Debug.LogWarning("[FlowFieldBootstrap] Not initialized yet. Waiting for baking to complete...");
                return;
            }

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
                Debug.LogWarning("[FlowFieldBootstrap] AgentSpawnerConfig singleton not found. Ensure AgentSpawnerConfigAuthoring is in scene and baking completed.");
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
            if (!_initialized || _entityManager == default)
                return 0;

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
            if (!_initialized || _entityManager == default)
                return 0;

            var query = _entityManager.CreateEntityQuery(typeof(AgentSpawnerConfig));
            if (query.TryGetSingleton<AgentSpawnerConfig>(out var config))
            {
                return config.PoolSize;
            }
            return 0;
        }

        private void OnDrawGizmos()
        {
            if (!showFlowField || !Application.isPlaying || !_initialized || _entityManager == default)
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

            // Draw line from target object if assigned
            if (targetObject != null)
            {
                Gizmos.color = Color.green;
                Vector3 objPos = targetObject.position;
                Gizmos.DrawWireCube(objPos, Vector3.one * 2f);
                Gizmos.DrawLine(objPos, targetPosition);
            }
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
            GUILayout.BeginArea(new Rect(10, 10, 400, 220));

            if (!_initialized)
            {
                GUILayout.Label("Initializing... (waiting for baking)");
            }
            else
            {
                GUILayout.Label($"Active Agents: {GetActiveAgentCount()} / {GetPoolSize()}");

                if (targetObject != null)
                {
                    GUILayout.Label($"Following: {targetObject.name}");
                    GUILayout.Label($"Target Position: {GetCurrentTargetPosition()}");
                }
                else
                {
                    GUILayout.Label("Using Manual Position");
                }

                GUILayout.Label("Controls:");
                GUILayout.Label("  [Space] - Spawn agents");
                GUILayout.Label("  [T] - Set target to mouse position");
                GUILayout.Label("  [F] - Toggle flow field visualization");
            }
            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Space)
                {
                    SpawnAgents();
                }
                else if (Event.current.keyCode == KeyCode.T)
                {
                    if (Camera.main != null)
                    {
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                        if (Physics.Raycast(ray, out RaycastHit hit))
                        {
                            targetPosition = hit.point;
                            SetTargetPosition(targetPosition);
                        }
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