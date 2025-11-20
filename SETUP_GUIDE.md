# Flow Field Pathfinding - Unity DOTS Setup Guide

## Overview
This project implements high-performance crowd pathfinding using Unity DOTS (Data-Oriented Technology Stack) with flow fields. It targets **60 FPS with 10,000+ agents** on mainstream hardware (4-8 core CPUs, mid-range GPUs).

---

## 1. Prerequisites

### Unity Version
- **Unity 6.2 LTS** (or 6.x series)
- Tested with **Unity 6000.0.25f1**

### Required Packages
The project already includes these packages (see `Packages/manifest.json`):

```json
{
  "com.unity.entities": "1.4.3",
  "com.unity.entities.graphics": "1.4.16",
  "com.unity.burst": "1.8.25",
  "com.unity.collections": "2.6.3",
  "com.unity.jobs": "0.80.0" (included with Entities),
  "com.unity.mathematics": "1.3.2"
}
```

**Already installed via feature metapackage:**
- `com.unity.feature.ecs`: "1.0.0" (bundles Entities, Graphics, Physics)

---

## 2. Project Structure

```
Assets/
├── AgentComponents.cs               # Core ECS component definitions
├── FlowFieldComponents.cs           # Flow field data structures
├── AgentSpawnerSystem.cs            # Entity pooling system
├── FlowFieldGenerationSystem.cs     # Flow field pathfinding
├── AgentMovementSystem.cs           # Movement + spatial hashing avoidance
├── AgentRenderingAuthoring.cs       # Rendering setup (GPU instancing)
├── FlowFieldBootstrap.cs            # MonoBehaviour scene controller
├── FlowFieldBootstrapAuthoring.cs   # ECS authoring components
├── PerformanceBenchmark.cs          # Automated benchmarking
└── Materials/
    └── AgentMaterial.mat            # SRP-compatible material (URP/HDRP)
```

---

## 3. Scene Setup (Step-by-Step)

### 3.1 Create New Scene
1. **File → New Scene → Basic (Built-in) or URP**
2. Save as `FlowFieldScene.unity`

### 3.2 Setup Ground Plane
1. **GameObject → 3D Object → Plane**
2. Scale: `(50, 1, 50)` to create 500x500 unit area
3. Position: `(0, 0, 0)`
4. Optional: Apply material for visual clarity

### 3.3 Setup Lighting
1. **GameObject → Light → Directional Light**
2. Rotation: `(50, -30, 0)` for natural lighting
3. If using URP: Ensure scene has Volume with Global Illumination baked

### 3.4 Setup Camera
1. Select Main Camera
2. Position: `(0, 100, -100)`
3. Rotation: `(45, 0, 0)` to look down at grid
4. Adjust Field of View: `60`

### 3.5 Create Bootstrap GameObject
1. **GameObject → Create Empty**
2. Name: `FlowFieldManager`
3. Add components:
   - `FlowFieldBootstrap.cs`
   - `PerformanceBenchmark.cs` (optional)

### 3.6 Create Agent Prefab
1. **GameObject → 3D Object → Cube**
2. Scale: `(0.5, 1, 0.5)` for character-sized agent
3. Add component: `AgentRenderingAuthoring.cs`
4. Configure in Inspector:
   - **Mesh**: Cube (default)
   - **Material**: Create new material (URP Lit or Standard)
   - **Speed**: `5.0`
   - **Avoidance Weight**: `0.5`
   - **Flow Follow Weight**: `1.0`
5. **Drag to Project → Prefabs/** to create prefab
6. Delete from scene

### 3.7 Configure Bootstrap
Select `FlowFieldManager` GameObject and configure `FlowFieldBootstrap`:

**Agent Settings:**
- Agent Prefab: *Drag your cube prefab here*
- Pool Size: `20000`
- Initial Spawn Count: `5000`
- Agent Speed: `5.0`

**Flow Field Settings:**
- Grid Width: `100` cells
- Grid Height: `100` cells
- Cell Size: `2.0` units
- Grid Origin: `(-100, 0, -100)`

**Spawn Settings:**
- Spawn Center: `(0, 0, 0)`
- Spawn Radius: `50`

**Target:**
- Target Position: `(50, 0, 50)`
- (Optional) Target Transform: Assign a GameObject to dynamically update target

**Debug Visualization:**
- Show Flow Field: `false` (enable for debugging)
- Show Grid: `false`

---

## 4. Build Settings (Critical for Performance)

### 4.1 Player Settings
1. **Edit → Project Settings → Player**
2. **Other Settings:**
   - Scripting Backend: **IL2CPP** (required for Burst)
   - API Compatibility: **.NET Standard 2.1**
   - Allow 'unsafe' Code: **✓ Enabled**

### 4.2 Burst Settings
1. **Jobs → Burst → Burst AOT Settings**
2. **Enable Burst Compilation: ✓**
3. **Optimizations:**
   - Target Platform: Auto (or specific platform)
   - Safety Checks: **Off** (for release builds)
   - Debug Info: **Off** (for release)

### 4.3 Quality Settings
1. **Edit → Project Settings → Quality**
2. Set active quality level to **Medium** or **High**
3. **V-Sync Count:** `Don't Sync` (for accurate FPS measurement)
4. **Anti-Aliasing:** FXAA or SMAA (lower cost)

### 4.4 Graphics Settings (URP)
If using URP:
1. **Edit → Project Settings → Graphics**
2. Ensure **UniversalRenderPipelineAsset** is assigned
3. In URP asset:
   - **SRP Batcher:** **✓ Enabled** (critical for instancing)
   - **Dynamic Batching:** **✗ Disabled** (incompatible with DOTS)
   - **GPU Instancing:** **✓ Enabled**

---

## 5. Running the Scene

### 5.1 First Run
1. Press **Play**
2. You should see:
   - 5000 agents spawn randomly within 50-unit radius
   - Agents move toward target position using flow field
   - GUI overlay showing FPS and agent count

### 5.2 Controls
**GUI Controls (Top-left):**
- **Spawn slider:** Adjust spawn count (100-5000)
- **Spawn button:** Spawn additional agents
- **Target sliders:** Move target position (X/Z)
- **Checkboxes:** Toggle debug visualization

**Manual Target Movement:**
- Assign a Transform to `Target Transform` field
- Move the GameObject in Scene view during Play mode

---

## 6. Verification Checklist

### ✓ Systems Running
Open **Window → Entities → Systems** and verify:
- `FlowFieldGenerationSystem` (InitializationSystemGroup)
- `AgentSpawnerSystem` (InitializationSystemGroup)
- `AgentMovementSystem` (SimulationSystemGroup)

### ✓ Entities Created
Open **Window → Entities → Hierarchy** and verify:
- Entity count matches active agents
- Entities have components: `Agent`, `AgentVelocity`, `LocalTransform`

### ✓ Rendering Working
- Agents should be visible as cubes
- Check **Window → Analysis → Frame Debugger** to see GPU instancing

### ✓ Performance Baseline
With **5000 agents**:
- **FPS:** 60+ (on 4-core CPU, GTX 1060 equivalent)
- **Frame Time:** <16.6ms
- Open **Window → Analysis → Profiler** and check:
  - CPU time: <12ms
  - Rendering time: <3ms

---

## 7. Scaling the Simulation

### Increase Agent Count
1. In `FlowFieldBootstrap` Inspector:
   - Increase `Pool Size` to `50000` or `100000`
   - Adjust `Initial Spawn Count` as needed
2. Use GUI to spawn more agents dynamically

### Adjust Grid Resolution
**Coarser Grid (Better Performance):**
- Grid Width/Height: `50x50`
- Cell Size: `4.0`
- Fewer cells = less pathfinding cost

**Finer Grid (Better Paths):**
- Grid Width/Height: `200x200`
- Cell Size: `1.0`
- More cells = higher CPU cost in flow field generation

**Tradeoff:**
- `100x100` @ 2.0 cell size = **10,000 cells** (balanced)
- Flow field rebuild: ~1-2ms on 4-core CPU

---

## 8. Adding Obstacles

### Static Obstacles
1. **GameObject → 3D Object → Cube** (or any mesh)
2. Add component: `FlowFieldObstacleAuthoring.cs`
3. Set **Radius:** `2.0` (cells within radius marked as obstacles)
4. Position obstacle in grid bounds
5. Flow field will regenerate when target changes

### Dynamic Obstacles
- Currently: Obstacles are sampled once during flow field generation
- **To support moving obstacles:** Modify `FlowFieldGenerationSystem` to rebuild periodically (e.g., every 0.5s)

---

## 9. Package Version Notes

### Entities 1.4.3 (Current)
- **Pros:** Stable LTS, well-documented
- **Cons:** Slightly older Burst optimizations
- **Hybrid Renderer:** Now called `Entities.Graphics` in 1.x

### Future Migration (Entities 1.5+)
- Improved Burst performance (+10-15%)
- Better prefab workflow
- **Migration:** Mostly API-compatible, test thoroughly

### Downgrading to Entities 1.2/1.3
- Replace `IJobEntity` with `IJobChunk` manually
- `SystemAPI` may not be available; use `EntityQuery` directly

---

## 10. Troubleshooting Quick Fixes

| **Issue** | **Solution** |
|-----------|-------------|
| No agents spawn | Check `Pool Size > 0`, verify prefab has `AgentRenderingAuthoring` |
| Agents invisible | Ensure material is SRP Batcher compatible, check mesh assigned |
| Low FPS (<30) | Disable Safety Checks in Burst, enable IL2CPP, check Profiler |
| Burst errors | Enable "Allow 'unsafe' Code" in Player Settings |
| Flow field doesn't update | Ensure `FlowFieldTarget.HasChanged = true` when changing target |

---

## 11. Next Steps

- **Read:** `PROFILING_GUIDE.md` for optimization techniques
- **Read:** `OPTIMIZATION_ROADMAP.md` for performance tuning
- **Run:** Benchmark script to establish baseline metrics
- **Experiment:** Add LOD, frustum culling, or custom avoidance

---

**Ready to profile and optimize? See `PROFILING_GUIDE.md`**
