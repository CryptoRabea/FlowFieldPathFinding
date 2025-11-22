using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Authoring component for flow field configuration singleton.
    /// Add to a GameObject in your scene to configure the pathfinding grid.
    ///
    /// NOTE: This only configures GRID SETTINGS (size, cell size, costs).
    /// To set the TARGET position at runtime, use FlowFieldBootstrap.SetTargetPosition() or
    /// set FlowFieldBootstrap.targetPosition in the Inspector.
    /// </summary>
    public class FlowFieldConfigAuthoring : MonoBehaviour
    {
        [Header("Grid Settings")]
        [Tooltip("Grid width in cells")]
        public int gridWidth = 100;

        [Tooltip("Grid height in cells")]
        public int gridHeight = 100;

        [Tooltip("Size of each cell in world units")]
        public float cellSize = 2.0f;

        [Tooltip("Bottom-left corner of the grid in world space")]
        public Vector3 gridOrigin = new Vector3(-100, 0, -100);

        [Header("Cost Settings")]
        [Tooltip("Cost value for impassable obstacle cells")]
        public byte obstacleCost = 255;

        [Tooltip("Cost value for free traversable cells")]
        public byte defaultCost = 1;

        [Tooltip("Smoothing factor for flow directions (0-1)")]
        [Range(0f, 1f)]
        public float directionSmoothFactor = 0.5f;

        private class Baker : Baker<FlowFieldConfigAuthoring>
        {
            public override void Bake(FlowFieldConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                // Create flow field config singleton (grid settings only)
                AddComponent(entity, new FlowFieldConfig
                {
                    GridWidth = authoring.gridWidth,
                    GridHeight = authoring.gridHeight,
                    CellSize = authoring.cellSize,
                    GridOrigin = authoring.gridOrigin,
                    ObstacleCost = authoring.obstacleCost,
                    DefaultCost = authoring.defaultCost,
                    DirectionSmoothFactor = authoring.directionSmoothFactor
                });

                // Create flow field target singleton (will be set by FlowFieldBootstrap at runtime)
                AddComponent(entity, new FlowFieldTarget
                {
                    Position = float3.zero,
                    HasChanged = false // Bootstrap will set the initial target
                });
            }
        }
    }
}
