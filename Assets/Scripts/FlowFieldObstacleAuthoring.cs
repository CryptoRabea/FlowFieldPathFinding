using Unity.Entities;
using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Authoring component for flow field obstacles.
    /// Add to GameObjects that should block agent movement.
    /// </summary>
    public class FlowFieldObstacleAuthoring : MonoBehaviour
    {
        [Tooltip("Obstacle radius in world units")]
        public float radius = 5f;

        private class Baker : Baker<FlowFieldObstacleAuthoring>
        {
            public override void Bake(FlowFieldObstacleAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new FlowFieldObstacle
                {
                    Radius = authoring.radius
                });
            }
        }
    }
}
