using Unity.Entities;
using Unity.Mathematics;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Core agent component containing movement parameters and behavior weights.
    /// Separated from transform for better cache coherency.
    /// </summary>
    public struct Agent : IComponentData
    {
        /// <summary>Maximum movement speed in units per second</summary>
        public float Speed;

        /// <summary>Weight for local avoidance (0-1). Higher = more separation from neighbors</summary>
        public float AvoidanceWeight;

        /// <summary>Weight for flow field following (0-1). Higher = stronger path adherence</summary>
        public float FlowFollowWeight;

        /// <summary>Weight for cohesion/grouping (0-1). Higher = stronger attraction to neighbors (zombie swarm)</summary>
        public float CohesionWeight;
    }

    /// <summary>
    /// Cached velocity component updated each frame.
    /// Separated from Agent for cache-friendly access in movement jobs.
    /// </summary>
    public struct AgentVelocity : IComponentData
    {
        /// <summary>Current velocity vector in world space</summary>
        public float3 Value;
    }

    /// <summary>
    /// Grid cell index for flow field lookup and spatial hashing.
    /// Updated by UpdateCellIndexJob each frame.
    /// 1D index = cellY * GridWidth + cellX (cache-friendly sequential access)
    /// </summary>
    public struct AgentCellIndex : IComponentData
    {
        /// <summary>1D grid cell index. -1 if not yet assigned or out of bounds</summary>
        public int Value;
    }

    /// <summary>
    /// Tag component for active agents in the simulation.
    /// Uses IEnableableComponent for zero-cost enable/disable pooling.
    /// Disabled agents are moved off-screen and skipped in queries.
    /// </summary>
    public struct AgentActive : IComponentData, IEnableableComponent
    {
    }

    /// <summary>
    /// Optional component for camera distance-based LOD or culling.
    /// Can be used to reduce update frequency for distant agents.
    /// </summary>
    public struct AgentDistanceToCamera : IComponentData
    {
        /// <summary>Squared distance to main camera (avoids sqrt)</summary>
        public float Value;
    }

    /// <summary>
    /// Optional component for attack state.
    /// Add this component to enable attack animations.
    /// </summary>
    public struct AgentAttack : IComponentData
    {
        /// <summary>Whether the agent is currently attacking</summary>
        public bool IsAttacking;

        /// <summary>Time remaining for current attack animation</summary>
        public float AttackTimer;

        /// <summary>Attack duration in seconds</summary>
        public float AttackDuration;
    }
}
