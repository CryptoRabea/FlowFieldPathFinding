using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Optimizes Rigidbody settings for agents controlled by flow field movement.
    ///
    /// CRITICAL PERFORMANCE NOTE:
    /// For maximum performance with 1000+ agents, REMOVE Rigidbody components entirely!
    /// The AgentMovementSystem handles all movement through transform updates,
    /// making physics simulation completely unnecessary.
    ///
    /// Only use this if you absolutely need physics interactions (pushing objects, etc).
    /// For most use cases, use AgentRenderingAuthoring WITHOUT AgentPhysicsAuthoring.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AgentRigidbodyOptimizer : MonoBehaviour
    {
        [Header("Optimization Settings")]
        [Tooltip("Make kinematic to disable physics simulation (best performance)")]
        public bool makeKinematic = true;

        [Tooltip("Disable gravity if not needed")]
        public bool disableGravity = true;

        [Tooltip("Reduce collision detection cost")]
        public CollisionDetectionMode collisionMode = CollisionDetectionMode.Discrete;

        [Tooltip("Interpolation for smooth visuals (slight overhead)")]
        public RigidbodyInterpolation interpolation = RigidbodyInterpolation.None;

        [Tooltip("Freeze position axes if agents only move on XZ plane")]
        public bool freezeYPosition = true;

        [Tooltip("Freeze rotation to prevent physics from rotating agents")]
        public bool freezeRotation = true;

        private Rigidbody rb;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            ApplyOptimizations();
        }

        [ContextMenu("Apply Rigidbody Optimizations")]
        public void ApplyOptimizations()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();

            if (rb == null)
                return;

            // Kinematic mode: no physics simulation, just collision detection
            rb.isKinematic = makeKinematic;

            // Disable gravity (flow field handles movement)
            rb.useGravity = !disableGravity;

            // Collision detection mode
            rb.collisionDetectionMode = collisionMode;

            // Interpolation
            rb.interpolation = interpolation;

            // Freeze constraints
            RigidbodyConstraints constraints = RigidbodyConstraints.None;

            if (freezeYPosition)
                constraints |= RigidbodyConstraints.FreezePositionY;

            if (freezeRotation)
                constraints |= RigidbodyConstraints.FreezeRotation;

            rb.constraints = constraints;

            // Additional optimizations
            rb.maxAngularVelocity = 0; // Agents don't spin
            rb.maxDepenetrationVelocity = 1.0f; // Reduce depenetration force

            Debug.Log($"[AgentRigidbodyOptimizer] Optimized {gameObject.name}:\n" +
                     $"- Kinematic: {rb.isKinematic}\n" +
                     $"- Gravity: {rb.useGravity}\n" +
                     $"- Collision Mode: {rb.collisionDetectionMode}");
        }

        void OnValidate()
        {
            if (Application.isPlaying && rb != null)
            {
                ApplyOptimizations();
            }
        }
    }
}
