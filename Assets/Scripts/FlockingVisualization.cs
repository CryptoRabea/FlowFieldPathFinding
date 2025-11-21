using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Visualizes agent flocking behavior using Debug.DrawLine.
    /// Works at runtime without needing Gizmos enabled.
    /// Attach to any GameObject in the scene.
    /// </summary>
    public class FlockingVisualization : MonoBehaviour
    {
        [Header("Agent Visualization")]
        public bool showAgentVelocity = true;
        public bool showAgentSeparation = false;
        public float velocityLineLength = 1.0f;
        public Color velocityColor = Color.yellow;
        public Color separationColor = Color.magenta;

        [Header("Flow Field Runtime Visualization")]
        public bool showFlowFieldRuntime = false;
        public Color flowRuntimeColor = Color.cyan;

        [Header("Performance")]
        [Tooltip("Max agents to visualize (0 = all)")]
        public int maxAgentsToVisualize = 500;
        public int flowFieldCellSkip = 2;

        private EntityManager _entityManager;
        private bool _initialized;

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            if (!_initialized)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return;
                _entityManager = world.EntityManager;
                _initialized = true;
            }

            if (showAgentVelocity || showAgentSeparation)
            {
                DrawAgentVisualization();
            }

            if (showFlowFieldRuntime)
            {
                DrawFlowFieldRuntime();
            }
        }

        private void DrawAgentVisualization()
        {
            var query = _entityManager.CreateEntityQuery(
                typeof(Agent),
                typeof(AgentVelocity),
                typeof(LocalTransform),
                typeof(AgentActive));

            var entities = query.ToEntityArray(Allocator.Temp);
            int count = maxAgentsToVisualize > 0 ? Mathf.Min(entities.Length, maxAgentsToVisualize) : entities.Length;

            for (int i = 0; i < count; i++)
            {
                var entity = entities[i];
                var transform = _entityManager.GetComponentData<LocalTransform>(entity);
                var velocity = _entityManager.GetComponentData<AgentVelocity>(entity);

                Vector3 pos = new Vector3(transform.Position.x, transform.Position.y + 0.5f, transform.Position.z);

                if (showAgentVelocity && math.lengthsq(velocity.Value) > 0.01f)
                {
                    Vector3 vel = new Vector3(velocity.Value.x, velocity.Value.y, velocity.Value.z);
                    Debug.DrawLine(pos, pos + vel.normalized * velocityLineLength, velocityColor);
                }
            }

            entities.Dispose();
        }

        private void DrawFlowFieldRuntime()
        {
            var query = _entityManager.CreateEntityQuery(typeof(FlowFieldData));
            if (query.CalculateEntityCount() == 0) return;

            var entities = query.ToEntityArray(Allocator.Temp);
            var flowFieldEntity = entities[0];
            entities.Dispose();

            if (!_entityManager.HasBuffer<FlowFieldDirectionBuffer>(flowFieldEntity))
                return;

            var flowFieldData = _entityManager.GetComponentData<FlowFieldData>(flowFieldEntity);
            var directionBuffer = _entityManager.GetBuffer<FlowFieldDirectionBuffer>(flowFieldEntity);

            if (directionBuffer.Length == 0)
                return;

            int skip = Mathf.Max(1, flowFieldCellSkip);

            for (int y = 0; y < flowFieldData.GridHeight; y += skip)
            {
                for (int x = 0; x < flowFieldData.GridWidth; x += skip)
                {
                    int index = y * flowFieldData.GridWidth + x;
                    if (index >= directionBuffer.Length) continue;

                    float2 dir = directionBuffer[index].Value;
                    if (math.lengthsq(dir) < 0.01f) continue;

                    float3 cellCenter = new float3(
                        flowFieldData.GridOrigin.x + (x + 0.5f) * flowFieldData.CellSize,
                        0.1f,
                        flowFieldData.GridOrigin.z + (y + 0.5f) * flowFieldData.CellSize
                    );

                    Vector3 center = new Vector3(cellCenter.x, cellCenter.y, cellCenter.z);
                    Vector3 direction = new Vector3(dir.x, 0, dir.y);
                    Vector3 end = center + direction * flowFieldData.CellSize * 0.4f;

                    Debug.DrawLine(center, end, flowRuntimeColor);
                }
            }
        }
    }
}
