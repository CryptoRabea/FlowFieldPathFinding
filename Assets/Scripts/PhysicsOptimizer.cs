using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Optimizes Unity Physics settings for large-scale agent simulations.
    ///
    /// IMPORTANT: For flow field agents that don't need physics simulation,
    /// consider removing Rigidbody components entirely and using AgentRenderingAuthoring
    /// without AgentPhysicsAuthoring for best performance.
    ///
    /// This optimizer is only useful if you MUST have physics for other objects
    /// in your scene (obstacles, projectiles, etc).
    /// </summary>
    public class PhysicsOptimizer : MonoBehaviour
    {
        [Header("Physics Performance Settings")]
        [Tooltip("Increase for better performance, decrease for more accurate physics")]
        [Range(0.01f, 0.1f)]
        public float fixedTimestep = 0.02f; // Default: 0.02 (50 fps), try 0.04 (25 fps) for better performance

        [Tooltip("Disable if agents don't interact with traditional physics objects")]
        public bool disableAutoSyncTransforms = true;

        [Tooltip("Maximum allowed timestep (prevents spiral of death)")]
        [Range(0.1f, 1f)]
        public float maximumAllowedTimestep = 0.1f;

        [Header("Collision Settings")]
        [Tooltip("Reduce iterations for better performance (default: 6)")]
        [Range(1, 20)]
        public int defaultSolverIterations = 4;

        [Tooltip("Reduce iterations for better performance (default: 1)")]
        [Range(1, 10)]
        public int defaultSolverVelocityIterations = 1;

        [Tooltip("Enable layer-based collision filtering in Physics settings")]
        public bool useLayerCollisionMatrix = true;

        [Header("Sleep Settings")]
        [Tooltip("Higher values allow objects to sleep sooner")]
        [Range(0.005f, 0.5f)]
        public float sleepThreshold = 0.05f;

        [Tooltip("Default sleep threshold is 0.005")]
        public bool enableAdaptiveForce = false;

        void Start()
        {
            ApplyOptimizations();
        }

        [ContextMenu("Apply Physics Optimizations")]
        public void ApplyOptimizations()
        {
            // Time settings
            Time.fixedDeltaTime = fixedTimestep;
            Time.maximumDeltaTime = maximumAllowedTimestep;

            // Auto sync transforms (expensive for many dynamic objects)
            Physics.autoSyncTransforms = !disableAutoSyncTransforms;

            // Solver iterations (lower = faster but less stable)
            Physics.defaultSolverIterations = defaultSolverIterations;
            Physics.defaultSolverVelocityIterations = defaultSolverVelocityIterations;

            // Sleep threshold
            Physics.sleepThreshold = sleepThreshold;

            // Adaptive force
            #if !UNITY_2022_1_OR_NEWER
            Physics.defaultContactOffset = 0.01f; // Reduce contact generation
            #endif

            Debug.Log($"[PhysicsOptimizer] Applied optimizations:\n" +
                     $"- Fixed Timestep: {fixedTimestep}s ({1f/fixedTimestep:F0} fps)\n" +
                     $"- Auto Sync Transforms: {!disableAutoSyncTransforms}\n" +
                     $"- Solver Iterations: {defaultSolverIterations}\n" +
                     $"- Solver Velocity Iterations: {defaultSolverVelocityIterations}\n" +
                     $"- Sleep Threshold: {sleepThreshold}");
        }

        void OnValidate()
        {
            if (Application.isPlaying)
            {
                ApplyOptimizations();
            }
        }
    }
}
