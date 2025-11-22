# Performance Optimization Guide

## 🚨 Problem: 10 FPS with 1000 Agents

If you're experiencing severe performance issues (10 FPS with only 1000 cubes), the issue is almost certainly **Unity's built-in Physics system**.

### Root Cause

When agents have **Rigidbody** and **BoxCollider/SphereCollider** components:
- Unity Physics runs expensive simulation every FixedUpdate
- Collision detection becomes O(n²) in worst case
- Contact solving requires iterative constraint solver
- Auto-sync transforms adds CPU overhead

**Meanwhile**, the `AgentMovementSystem` handles ALL movement using:
- Flow field pathfinding (grid-based, efficient)
- Spatial hashing for avoidance (O(n) neighbor queries)
- Burst-compiled parallel jobs
- Direct transform position updates

**Result**: Physics simulation is completely **unnecessary** and **wasteful**!

---

## ✅ Solution 1: Remove Physics Components (RECOMMENDED)

**Best Performance**: 60+ FPS with 10,000+ agents

### For Agent Prefabs:

1. **Remove** `Rigidbody` component
2. **Remove** `BoxCollider`/`SphereCollider` (or make them triggers if needed)
3. **Keep** only `AgentRenderingAuthoring` component
4. **Do NOT add** `AgentPhysicsAuthoring` component

### Using the Converter Tool:

1. Open Unity Editor
2. Go to **Tools > Flow Field > Agent Physics Converter**
3. Select **"Remove Rigidbodies"** option
4. Click **"Convert All in Scene"**

**Expected Result**:
- 1,000 agents: 120+ FPS
- 5,000 agents: 60+ FPS
- 10,000 agents: 30+ FPS

---

## ✅ Solution 2: Optimize Physics Settings

If you **must** keep physics (for triggers, collision detection with obstacles, etc):

### A) Add `PhysicsOptimizer` Component

1. Add `PhysicsOptimizer` component to any GameObject in scene
2. Configure settings:
   - **Fixed Timestep**: 0.04 (reduces physics updates from 50fps to 25fps)
   - **Solver Iterations**: 4 (default is 6)
   - **Disable Auto Sync Transforms**: ✓

**Expected Gain**: 2-3x performance improvement

### B) Make Rigidbodies Kinematic

**Kinematic bodies**: Skip physics simulation but keep collision detection

**Option 1 - Per Prefab:**
1. Add `AgentRigidbodyOptimizer` component to agent prefab
2. Set `makeKinematic = true`
3. Component automatically optimizes settings

**Option 2 - Batch Convert:**
1. **Tools > Flow Field > Agent Physics Converter**
2. Uncheck "Remove Rigidbodies"
3. Check "Make Kinematic"
4. Click "Convert All in Scene"

**Expected Gain**: 5-10x performance improvement

### C) Use Layer Collision Matrix

1. **Edit > Project Settings > Physics**
2. Create separate layers: "Agents", "Obstacles", "Triggers"
3. Disable unnecessary collision pairs (e.g., Agent-Agent if using spatial hash avoidance)

**Expected Gain**: 2-5x performance improvement

---

## ✅ Solution 3: ECS Physics (Advanced)

For collision detection with ECS entities:

1. Remove built-in `Rigidbody`/`Collider` components
2. Add `AgentPhysicsAuthoring` component to prefab
3. Configure collision layers appropriately

**Performance**: Unity Physics (ECS) is ~5x faster than built-in physics

**Note**: `AgentPhysicsAuthoring` creates **kinematic bodies** by default, meaning agents are moved by flow field code and don't participate in dynamic physics simulation.

---

## 📊 Benchmarking

Use the `PerformanceBenchmark` component to measure improvements:

1. Add `PerformanceBenchmark` to any GameObject
2. Configure benchmark counts: [1000, 2000, 5000, 10000]
3. Press **B** key to run automated benchmark
4. Check Console for FPS results

---

## 🔍 Quick Diagnosis Checklist

**If FPS < 30 with 1000 agents:**

- [ ] ❌ Agent prefabs have `Rigidbody` components → **REMOVE**
- [ ] ❌ Agent prefabs have `BoxCollider`/`SphereCollider` → **REMOVE or make trigger**
- [ ] ❌ Using `AgentPhysicsAuthoring` unnecessarily → **REMOVE**
- [ ] ✅ Using only `AgentRenderingAuthoring` → **CORRECT**

**If FPS still low after removing physics:**

- [ ] Check if shadows are enabled (costly for many objects)
- [ ] Verify GPU instancing is working (check draw calls, should be ~10-20)
- [ ] Disable Flow Field Visualization during benchmarks
- [ ] Reduce flocking visualization max agents or disable
- [ ] Check grid size is reasonable (100x100 cells default)

---

## 🎯 Performance Targets (Mid-Range Hardware)

| Agent Count | Target FPS | Physics Disabled | Physics Optimized | Physics Unoptimized |
|-------------|------------|------------------|-------------------|---------------------|
| 1,000       | 120+ FPS   | ✅ 150+ FPS      | ⚠️ 80 FPS        | ❌ 10 FPS          |
| 5,000       | 60+ FPS    | ✅ 80+ FPS       | ⚠️ 40 FPS        | ❌ 2 FPS           |
| 10,000      | 30+ FPS    | ✅ 40+ FPS       | ⚠️ 15 FPS        | ❌ <1 FPS          |

---

## 📚 Understanding the Movement System

`AgentMovementSystem` processes movement in **3 parallel Burst-compiled jobs**:

1. **UpdateCellIndexJob**: Assigns flow field cells, builds spatial hash
2. **CalculateVelocityJob**: Samples flow field + local avoidance
3. **ApplyMovementJob**: Updates `LocalTransform.Position` **directly**

**Key Point**: Positions are updated **directly** via transform updates.
- No physics forces or velocities
- No collision response
- Rigidbodies would add overhead and conflict with this system

---

## 🛠️ Tools & Components

### Performance Optimization
- **PhysicsOptimizer.cs** - Optimizes global Unity Physics settings
- **AgentRigidbodyOptimizer.cs** - Optimizes individual Rigidbody components
- **PerformanceGuide.cs** - In-engine documentation and tips

### Editor Tools
- **Tools > Flow Field > Agent Physics Converter** - Batch convert agents
  - Remove physics components
  - Make rigidbodies kinematic
  - Add optimizer components

### Benchmarking
- **PerformanceBenchmark.cs** - Automated performance testing
  - FPS tracking with min/max/average
  - Multi-count benchmarks
  - Press 'B' to start

---

## ❓ FAQ

**Q: I need collision detection between agents and obstacles. What should I do?**

A: Use **triggers** instead of rigidbodies:
1. Add `BoxCollider` with `isTrigger = true`
2. Implement `OnTriggerEnter` detection
3. No Rigidbody needed for trigger-only collisions

**Q: Can I use physics for some agents but not others?**

A: Yes!
- Non-physics agents: Only `AgentRenderingAuthoring`
- Physics agents: `AgentRenderingAuthoring` + `AgentPhysicsAuthoring`

**Q: What if I need agents to push objects?**

A: Use `AgentRigidbodyOptimizer` with:
- `makeKinematic = false` (allow physics forces)
- Reduce mass and increase drag for better control
- Consider layer collision matrix to reduce agent-agent collisions

**Q: Why does the built-in Rigidbody perform so poorly?**

A: Unity's built-in physics was designed for moderate numbers of dynamic objects (tens to hundreds). It uses:
- Continuous collision detection (expensive)
- Complex constraint solver
- Auto-sync transforms (copies data between physics and transform systems)
- Not optimized for massive agent simulations

ECS Physics and custom movement systems (like flow fields) are 10-100x more efficient for large numbers of agents.

---

## 📖 Related Files

- `AgentMovementSystem.cs` - Movement implementation
- `AgentPhysicsAuthoring.cs` - ECS physics setup (with performance warnings)
- `AgentRenderingAuthoring.cs` - Rendering setup (no physics)
- `FlowFieldPathfinding/` - All related scripts

---

**For immediate fix**: Use **Tools > Flow Field > Agent Physics Converter** and select "Remove Rigidbodies" → Click "Convert All in Scene" → Enjoy 60+ FPS! 🚀
