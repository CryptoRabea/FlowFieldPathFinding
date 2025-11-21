# Flow Field Pathfinding - Unity DOTS Setup Guide

## Overview
This project implements high-performance crowd pathfinding using Unity DOTS (Data-Oriented Technology Stack) with flow fields. It targets **60 FPS with 10,000+ agents** on mainstream hardware (4-8 core CPUs, mid-range GPUs).

---

## 1. Prerequisites

### Unity Version
- **Unity 6.x LTS** (or 6.x series)
- Tested with **Unity 6000.0.25f1**

### Required Packages
The project already includes these packages (see `Packages/manifest.json`):

```json
{
  "com.unity.entities": "1.4.3",
  "com.unity.entities.graphics": "1.4.16",
  "com.unity.burst": "1.8.25",
  "com.unity.collections": "2.6.3",
  "com.unity.mathematics": "1.3.2"
}
```

**Already installed via feature metapackage:**
- `com.unity.feature.ecs`: "1.0.0" (bundles Entities, Graphics, Physics)

---

## 2. Quick Reference - Authoring Components

**Note:** `FlowFieldBootstrapAuthoring.cs` contains THREE authoring components in one file: `FlowFieldConfigAuthoring`, `AgentSpawnerConfigAuthoring`, and `FlowFieldObstacleAuthoring`. You add these as separate components to different GameObjects, but they're all defined in the same script.

| **Component** | **Defined In** | **Attach To** | **Purpose** | **Creates** |
|--------------|---------------|--------------|------------|-----------|
| `FlowFieldConfigAuthoring` | `FlowFieldBootstrapAuthoring.cs` | Empty GameObject (scene) | Grid settings, target position | `FlowFieldConfig` + `FlowFieldTarget` singletons |
| `AgentSpawnerConfigAuthoring` | `FlowFieldBootstrapAuthoring.cs` | Empty GameObject (scene) | Pool size, spawn settings, agent rendering | `AgentSpawnerConfig` singleton + pooled agent entities |
| `FlowFieldObstacleAuthoring` | `FlowFieldBootstrapAuthoring.cs` | Obstacle GameObject (scene) | Mark as impassable | Entity with `FlowFieldObstacle` |
| `FlowFieldBootstrap` | `FlowFieldBootstrap.cs` | Empty GameObject (optional) | Runtime control & debug UI | N/A (MonoBehaviour) |

---

## 3. Architecture Overview

### ECS Systems
- **FlowFieldGenerationSystem** - Generates flow field when target changes
- **AgentSpawnerSystem** - Manages entity pool with zero-allocation spawning
- **AgentMovementSystem** - Updates agent positions using flow field + avoidance

### Baking Workflow
Unity's new Baking system converts GameObjects into ECS entities:

**Singleton Entities (Scene):**
- `FlowFieldConfigAuthoring` → Bakes into `FlowFieldConfig` + `FlowFieldTarget`
- `AgentSpawnerConfigAuthoring` → Bakes into `AgentSpawnerConfig`
  - `AgentSpawnerSystem` creates pooled agent entities with rendering components programmatically

**Obstacle Entities (Scene):**
- `FlowFieldObstacleAuthoring` → Bakes into entities with `FlowFieldObstacle`

**Runtime Controller (Optional):**
- `FlowFieldBootstrap` - MonoBehaviour for testing/debugging (not baked)

---

## 4. Scene Setup (Step-by-Step)

### 4.1 Create New Scene
1. **File → New Scene → Basic (Built-in) or URP**
2. Save as `FlowFieldScene.unity`

### 4.2 Setup Ground Plane
1. **GameObject → 3D Object → Plane**
2. Scale: `(50, 1, 50)` to create 500x500 unit area
3. Position: `(0, 0, 0)`
4. Optional: Apply material for visual clarity

### 4.3 Setup Lighting
1. **GameObject → Light → Directional Light**
2. Rotation: `(50, -30, 0)` for natural lighting
3. If using URP: Ensure scene has Volume with Global Illumination baked

### 4.4 Setup Camera
1. Select Main Camera
2. Position: `(0, 100, -100)`
3. Rotation: `(45, 0, 0)` to look down at grid
4. Adjust Field of View: `60`

### 4.5 Setup Flow Field Configuration
1. **GameObject → Create Empty**
2. Name: `FlowFieldConfig`
3. **Add Component → FlowFieldConfigAuthoring** (from `FlowFieldBootstrapAuthoring.cs`)
4. Configure in Inspector:
   - **Grid Width**: `100` cells
   - **Grid Height**: `100` cells
   - **Cell Size**: `2.0` units
   - **Grid Origin**: `(-100, 0, -100)`
   - **Target Position**: `(50, 0, 50)`
   - **Obstacle Cost**: `255` (impassable)
   - **Default Cost**: `1` (traversable)

**What this does:** Creates singleton entities (`FlowFieldConfig` and `FlowFieldTarget`) that control the pathfinding grid.

### 4.6 Setup Agent Spawner Configuration
1. **GameObject → Create Empty**
2. Name: `AgentSpawnerConfig`
3. **Add Component → AgentSpawnerConfigAuthoring** (from `FlowFieldBootstrapAuthoring.cs`)
4. Configure in Inspector:
   - **Pool Size**: `20000` (max agents)
   - **Initial Spawn Count**: `5000` (spawn on start)
   - **Spawn Center**: `(0, 0, 0)`
   - **Spawn Radius**: `20`
   - **Default Speed**: `5.0`
   - **Default Avoidance Weight**: `0.5`
   - **Default Flow Follow Weight**: `1.0`

**What this does:** Creates a singleton entity (`AgentSpawnerConfig`) that controls the agent pool. The spawner creates agents with rendering components programmatically (no prefab needed).

### 4.7 Setup Runtime Controller (Optional)
1. **GameObject → Create Empty**
2. Name: `FlowFieldManager`
3. **Add Component → FlowFieldBootstrap**
4. Configure in Inspector:
   - **Target Position**: `(50, 0, 50)`
   - **Spawn Count**: `1000` (for manual spawning)
   - **Show Flow Field**: `false` (toggle for debugging)

**What this does:** Provides runtime control and visualization. Not required for the system to work, but useful for testing and debugging.

---

## 5. Build Settings (Critical for Performance)

### 5.1 Player Settings
1. **Edit → Project Settings → Player**
2. **Other Settings:**
   - Scripting Backend: **IL2CPP** (required for Burst)
   - API Compatibility: **.NET Standard 2.1**
   - Allow 'unsafe' Code: **✓ Enabled**

### 5.2 Burst Settings
1. **Jobs → Burst → Burst AOT Settings**
2. **Enable Burst Compilation: ✓**
3. **Optimizations:**
   - Target Platform: Auto (or specific platform)
   - Safety Checks: **Off** (for release builds)
   - Debug Info: **Off** (for release)

### 5.3 Quality Settings
1. **Edit → Project Settings → Quality**
2. Set active quality level to **Medium** or **High**
3. **V-Sync Count:** `Don't Sync` (for accurate FPS measurement)
4. **Anti-Aliasing:** FXAA or SMAA (lower cost)

### 5.4 Graphics Settings (URP)
If using URP:
1. **Edit → Project Settings → Graphics**
2. Ensure **UniversalRenderPipelineAsset** is assigned
3. In URP asset:
   - **SRP Batcher:** **✓ Enabled** (critical for instancing)
   - **Dynamic Batching:** **✗ Disabled** (incompatible with DOTS)
   - **GPU Instancing:** **✓ Enabled**

---

## 6. Understanding the Authoring Workflow

Unity's new **Baking system** (introduced in Entities 1.0+) replaces the old conversion system:

### Traditional GameObject → ECS Entity
1. Add an Authoring component (MonoBehaviour) to a GameObject
2. Add a **Baker class** inside the authoring component
3. Baker runs automatically during baking (in Editor and at build time)
4. Baker creates ECS entities and components from GameObject data

### In This Project

**Singleton Entities (Scene Config):**
- `FlowFieldConfigAuthoring` → Bakes into `FlowFieldConfig` + `FlowFieldTarget` singletons
- `AgentSpawnerConfigAuthoring` → Bakes into `AgentSpawnerConfig` singleton
  - At runtime, `AgentSpawnerSystem` creates pooled entities with all agent components + rendering
- `FlowFieldObstacleAuthoring` → Bakes into entities with `FlowFieldObstacle` component

### When Baking Happens
- **Automatically:** When you modify authoring components in Editor
- **On Play:** Before entering Play mode
- **At Build Time:** When building the project
- **SubScene Baking:** For large scenes (not used in this project)

### Viewing Baked Entities
1. **Enter Play Mode**
2. **Window → Entities → Hierarchy** to see baked entities
3. Select an entity to see its ECS components in Inspector

---

## 7. Running the Scene

### 7.1 First Run
1. Press **Play**
2. You should see:
   - Agents spawn automatically (based on `Initial Spawn Count`)
   - Agents move toward target position using flow field
   - If `FlowFieldBootstrap` is present: GUI overlay with controls

### 7.2 Controls (if using FlowFieldBootstrap)
**GUI Controls (Top-left):**
- **Active Agents count**
- **Keyboard shortcuts:**
  - **[Space]** - Spawn more agents
  - **[T]** - Set target to mouse position (raycast required)
  - **[F]** - Toggle flow field visualization

**Manual Control:**
- Modify `Target Position` in `FlowFieldBootstrap` Inspector during Play mode
- Change spawner settings in `AgentSpawnerConfig` Inspector

---

## 8. Verification Checklist

### ✓ Authoring Setup
In Edit mode, verify:
- `FlowFieldConfig` GameObject has `FlowFieldConfigAuthoring` component
- `AgentSpawnerConfig` GameObject has `AgentSpawnerConfigAuthoring` component

### ✓ Systems Running
In Play mode, open **Window → Entities → Systems** and verify:
- `FlowFieldGenerationSystem` (InitializationSystemGroup)
- `AgentSpawnerSystem` (InitializationSystemGroup)
- `AgentMovementSystem` (SimulationSystemGroup)

### ✓ Entities Created
In Play mode, open **Window → Entities → Hierarchy** and verify:
- Singleton entities: `FlowFieldConfig`, `AgentSpawnerConfig`, `FlowFieldTarget`
- Agent entities with components: `Agent`, `AgentActive`, `AgentVelocity`, `AgentCellIndex`, `LocalTransform`
- Entity count matches pool size (not active count - inactive agents are disabled)

### ✓ Rendering Working
- Agents should be visible as cubes
- Check **Window → Analysis → Frame Debugger** to see GPU instancing (1 draw call per unique mesh/material)

### ✓ Performance Baseline
With **5000 agents**:
- **FPS:** 60+ (on 4-core CPU, GTX 1060 equivalent)
- **Frame Time:** <16.6ms
- Open **Window → Analysis → Profiler** and check:
  - CPU time: <12ms
  - Rendering time: <3ms

---

## 9. Scaling the Simulation

### Increase Agent Count
1. In `AgentSpawnerConfigAuthoring` Inspector:
   - Increase `Pool Size` to `50000` or `100000`
   - Adjust `Initial Spawn Count` as needed
2. If using `FlowFieldBootstrap`: Use GUI/keyboard to spawn more agents dynamically

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

## 10. Adding Obstacles

### Static Obstacles
1. **GameObject → 3D Object → Cube** (or any mesh)
2. **Add Component → FlowFieldObstacleAuthoring** (from `FlowFieldBootstrapAuthoring.cs`)
3. Set **Radius:** `5.0` (world units - cells within radius marked as impassable)
4. Position obstacle within grid bounds
5. **Baking:** Obstacle is converted to ECS entity with `FlowFieldObstacle` component
6. **Flow field updates:** Obstacles are sampled during flow field generation (when target changes)

**Example Setup:**
```
Cube GameObject
├── Transform: Position (50, 0, 50)
├── FlowFieldObstacleAuthoring
│   └── Radius: 5.0
└── (Optional) MeshRenderer for visualization
```

### Dynamic Obstacles
- **Current behavior:** Obstacles are sampled once during flow field generation
- **For moving obstacles:**
  - Option 1: Trigger flow field rebuild by setting `FlowFieldTarget.HasChanged = true`
  - Option 2: Modify `FlowFieldGenerationSystem` to rebuild periodically (e.g., every 0.5s)

---

## 11. Package Version Notes

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

## 12. Troubleshooting Quick Fixes

| **Issue** | **Solution** |
|-----------|-------------|
| No agents spawn | Verify `AgentSpawnerConfigAuthoring` exists, `Pool Size > 0`, `Initial Spawn Count > 0` |
| Agents invisible | Check URP/HDRP pipeline is set up, SRP Batcher enabled, shader exists |
| Baking errors | Check all authoring components are attached, no missing references |
| No flow field entity | Verify `FlowFieldConfigAuthoring` exists in scene |
| Low FPS (<30) | Disable Safety Checks in Burst, enable IL2CPP, check Profiler |
| Burst errors | Enable "Allow 'unsafe' Code" in Player Settings |
| Flow field doesn't update | Set `FlowFieldTarget.HasChanged = true` when changing target |
| Systems not running | Open Window → Entities → Systems to verify systems are in world |

### Common Baking Issues

**"Entity not found" errors:**
- Ensure authoring GameObjects are in the scene hierarchy (not disabled)
- Check that baking completed (no console errors during baking)

**"Singleton not found" errors:**
- Verify exactly ONE instance of each config authoring component in scene
- Check Window → Entities → Hierarchy to see baked singletons

**Agents spawned but invisible:**
- AgentSpawnerSystem creates rendering components programmatically
- Ensure URP/HDRP is set up with "Universal Render Pipeline/Lit" shader
- Check GPU Instancing is enabled in Graphics settings

---

## 13. Next Steps

- **Read:** `PROFILING_GUIDE.md` for optimization techniques
- **Read:** `OPTIMIZATION_ROADMAP.md` for performance tuning
- **Read:** `README.md` for architecture deep-dive
- **Experiment:** Add LOD, frustum culling, or custom avoidance

---

**Ready to profile and optimize? See `PROFILING_GUIDE.md`**
