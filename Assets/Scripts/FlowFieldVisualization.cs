using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Visualizes the flow field using Unity Gizmos.
    /// Attach to any GameObject in the scene to enable visualization.
    /// </summary>
    public class FlowFieldVisualization : MonoBehaviour
    {
        [Header("Visualization Settings")]
        public bool showFlowField = true;
        public bool showIntegrationField = false;
        public bool showCostField = false;
        public float arrowLength = 0.8f;
        public Color flowColor = Color.cyan;
        public Color obstacleColor = Color.red;
        public Color destinationColor = Color.green;

        [Header("Performance")]
        [Tooltip("Only draw every Nth cell to improve performance")]
        public int cellSkip = 1;

        private EntityManager _entityManager;
        private Entity _flowFieldEntity;
        private bool _initialized;

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !showFlowField)
                return;

            if (!_initialized)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return;
                _entityManager = world.EntityManager;

                // Find flow field entity
                var query = _entityManager.CreateEntityQuery(typeof(FlowFieldData));
                if (query.CalculateEntityCount() == 0) return;

                var entities = query.ToEntityArray(Allocator.Temp);
                _flowFieldEntity = entities[0];
                entities.Dispose();
                _initialized = true;
            }

            if (!_entityManager.Exists(_flowFieldEntity))
            {
                _initialized = false;
                return;
            }

            var flowFieldData = _entityManager.GetComponentData<FlowFieldData>(_flowFieldEntity);

            if (!_entityManager.HasBuffer<FlowFieldDirectionBuffer>(_flowFieldEntity))
                return;

            var directionBuffer = _entityManager.GetBuffer<FlowFieldDirectionBuffer>(_flowFieldEntity);
            var costBuffer = _entityManager.GetBuffer<FlowFieldCostBuffer>(_flowFieldEntity);
            var integrationBuffer = _entityManager.GetBuffer<FlowFieldIntegrationBuffer>(_flowFieldEntity);

            if (directionBuffer.Length == 0)
                return;

            int skip = Mathf.Max(1, cellSkip);

            for (int y = 0; y < flowFieldData.GridHeight; y += skip)
            {
                for (int x = 0; x < flowFieldData.GridWidth; x += skip)
                {
                    int index = y * flowFieldData.GridWidth + x;
                    if (index >= directionBuffer.Length) continue;

                    float3 cellCenter = new float3(
                        flowFieldData.GridOrigin.x + (x + 0.5f) * flowFieldData.CellSize,
                        0.1f,
                        flowFieldData.GridOrigin.z + (y + 0.5f) * flowFieldData.CellSize
                    );

                    Vector3 center = new Vector3(cellCenter.x, cellCenter.y, cellCenter.z);
                    byte cost = costBuffer[index].Value;

                    // Draw obstacle cells
                    if (cost == 255)
                    {
                        Gizmos.color = obstacleColor;
                        Gizmos.DrawWireCube(center, Vector3.one * flowFieldData.CellSize * 0.9f);
                        continue;
                    }

                    // Draw destination
                    if (x == flowFieldData.DestinationCell.x && y == flowFieldData.DestinationCell.y)
                    {
                        Gizmos.color = destinationColor;
                        Gizmos.DrawSphere(center, flowFieldData.CellSize * 0.3f);
                        continue;
                    }

                    // Draw flow direction arrow
                    if (showFlowField)
                    {
                        float2 dir = directionBuffer[index].Value;
                        if (math.lengthsq(dir) > 0.01f)
                        {
                            Vector3 direction = new Vector3(dir.x, 0, dir.y);
                            Vector3 end = center + direction * arrowLength * flowFieldData.CellSize * 0.5f;

                            // Color based on integration value if enabled
                            if (showIntegrationField)
                            {
                                ushort integration = integrationBuffer[index].Value;
                                float t = Mathf.Clamp01(integration / 200f);
                                Gizmos.color = Color.Lerp(destinationColor, flowColor, t);
                            }
                            else
                            {
                                Gizmos.color = flowColor;
                            }

                            Gizmos.DrawLine(center, end);

                            // Draw arrowhead
                            Vector3 arrowHead1 = end - direction * 0.2f + Vector3.Cross(direction, Vector3.up) * 0.1f;
                            Vector3 arrowHead2 = end - direction * 0.2f - Vector3.Cross(direction, Vector3.up) * 0.1f;
                            Gizmos.DrawLine(end, arrowHead1);
                            Gizmos.DrawLine(end, arrowHead2);
                        }
                    }

                    // Draw cost field
                    if (showCostField && cost > 1)
                    {
                        Gizmos.color = new Color(1, 0.5f, 0, 0.5f);
                        Gizmos.DrawWireCube(center, Vector3.one * flowFieldData.CellSize * 0.5f);
                    }
                }
            }
        }
    }
}
