using UnityEngine;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Performance guide and tips for Flow Field Pathfinding system.
    ///
    /// READ THIS to understand how to achieve 60+ FPS with 1000+ agents!
    /// </summary>
    public class PerformanceGuide : MonoBehaviour
    {
        [Header("Performance Issue: 10 FPS with 1000 agents?")]
        [TextArea(20, 50)]
        public string performanceTips = @"
═══════════════════════════════════════════════════════════════
  FLOW FIELD PATHFINDING - PERFORMANCE OPTIMIZATION GUIDE
═══════════════════════════════════════════════════════════════

PROBLEM: 10 FPS with only 1000 cubes with Rigidbody and BoxCollider

ROOT CAUSE:
Unity's built-in Physics system is EXTREMELY expensive for large numbers
of dynamic Rigidbodies. With 1000 agents, the physics simulation becomes
the primary bottleneck:
- Collision detection: O(n²) in worst case
- Contact solving: Iterative constraint solver
- Integration: Velocity and position updates
- Auto-sync transforms: CPU overhead

Meanwhile, the AgentMovementSystem handles ALL movement using:
- Flow field pathfinding (efficient grid-based)
- Spatial hashing for avoidance (O(n) neighbor queries)
- Burst-compiled parallel jobs
→ Physics simulation is completely UNNECESSARY and WASTEFUL!

═══════════════════════════════════════════════════════════════
  SOLUTION 1: Remove Physics (RECOMMENDED - Best Performance)
═══════════════════════════════════════════════════════════════

For agent prefabs that use flow field movement:
1. REMOVE Rigidbody component
2. REMOVE BoxCollider/SphereCollider (or make them triggers if needed)
3. Keep only: AgentRenderingAuthoring component
4. Do NOT add: AgentPhysicsAuthoring component

Expected Performance: 60+ FPS with 10,000+ agents on mid-range hardware

How to convert existing agents:
→ Use: Tools > Flow Field > Agent Physics Converter
   - Select 'Remove Rigidbodies' option
   - Click 'Convert All in Scene'

═══════════════════════════════════════════════════════════════
  SOLUTION 2: Optimize Physics Settings (If Physics Needed)
═══════════════════════════════════════════════════════════════

If you MUST keep physics (for pushing objects, triggers, etc):

A) Add PhysicsOptimizer component to scene:
   - Increases Fixed Timestep (0.02 → 0.04 = 2x faster)
   - Reduces solver iterations (6 → 4 = 1.5x faster)
   - Disables auto-sync transforms
   Expected Gain: 2-3x performance improvement

B) Make Rigidbodies Kinematic:
   - Add AgentRigidbodyOptimizer to each agent prefab
   - Or use: Tools > Flow Field > Agent Physics Converter
     with 'Make Kinematic' option
   - Kinematic bodies skip physics simulation but keep collision detection
   Expected Gain: 5-10x performance improvement

C) Use Layer Collision Matrix:
   - Edit > Project Settings > Physics
   - Disable collisions between Agent layers if not needed
   - Example: Agents don't collide with each other, only obstacles
   Expected Gain: 2-5x performance improvement (depends on setup)

═══════════════════════════════════════════════════════════════
  SOLUTION 3: ECS Physics (Advanced)
═══════════════════════════════════════════════════════════════

If you need physics interactions with ECS entities:
1. Remove built-in Rigidbody/Collider components
2. Add AgentPhysicsAuthoring component instead
3. Configure collision layers appropriately
4. Unity Physics (ECS) is ~5x faster than built-in physics

Note: The AgentPhysicsAuthoring creates kinematic bodies by default,
which means agents are moved by code (flow field) and don't participate
in dynamic physics simulation.

═══════════════════════════════════════════════════════════════
  BENCHMARKING YOUR CHANGES
═══════════════════════════════════════════════════════════════

Use the PerformanceBenchmark component to measure improvements:
1. Add PerformanceBenchmark to any GameObject
2. Configure benchmark agent counts (e.g., 1000, 2000, 5000)
3. Press 'B' to run automated benchmark
4. Check console for FPS results

Target Performance (mid-range hardware):
- 1,000 agents: 120+ FPS
- 5,000 agents: 60+ FPS
- 10,000 agents: 30+ FPS

═══════════════════════════════════════════════════════════════
  QUICK FIXES CHECKLIST
═══════════════════════════════════════════════════════════════

✓ Removed Rigidbody from agent prefabs
✓ Removed or made Colliders triggers
✓ Using only AgentRenderingAuthoring (no AgentPhysicsAuthoring)
✓ Added PhysicsOptimizer to scene (if other physics objects exist)
✓ Disabled shadows if not needed (Rendering > Shadows > None)
✓ Using GPU instancing for rendering (automatic with ECS)
✓ Verified flow field grid size is reasonable (100x100 cells)
✓ Turned off Flow Field Visualization during benchmarks
✓ Reduced flocking visualization max agents (or disabled)

═══════════════════════════════════════════════════════════════
  UNDERSTANDING THE MOVEMENT SYSTEM
═══════════════════════════════════════════════════════════════

AgentMovementSystem handles movement in 3 parallel jobs:
1. UpdateCellIndexJob: Assigns flow field cells, builds spatial hash
2. CalculateVelocityJob: Samples flow field + local avoidance
3. ApplyMovementJob: Updates transform positions directly

Key Point: Positions are updated DIRECTLY via LocalTransform.Position
→ No physics forces or velocities are used
→ Rigidbodies would just add overhead and conflict with this system

═══════════════════════════════════════════════════════════════

For more information, see:
- AgentMovementSystem.cs (movement implementation)
- PhysicsOptimizer.cs (physics settings)
- AgentRigidbodyOptimizer.cs (per-agent optimization)
- Tools > Flow Field > Agent Physics Converter (batch conversion)

═══════════════════════════════════════════════════════════════";

        [ContextMenu("Print Performance Tips")]
        void PrintTips()
        {
            Debug.Log(performanceTips);
        }
    }
}
