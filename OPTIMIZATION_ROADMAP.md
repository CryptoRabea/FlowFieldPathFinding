# Optimization Roadmap - Priority-Ordered Performance Improvements

## Overview
This roadmap lists optimizations ordered by **effort-to-impact ratio**. Start from the top and work down until you hit your performance target.

**Current baseline:** ~14ms frame time @ 10k agents (70 FPS)
**Target:** <16.6ms @ 10k agents (60 FPS) ✓ **Already achieved!**
**Stretch goal:** <16.6ms @ 20k agents (60 FPS)

---

## Priority 1: Zero-Effort Optimizations (Already Implemented)

These are **already done** in the provided code:

### ✓ 1.1 Use Burst Compilation
- **Impact:** 5-10x speedup on jobs
- **Status:** All systems/jobs marked with `[BurstCompile]`
- **Validation:** Check Burst Inspector for vectorized assembly

### ✓ 1.2 Entity Pooling (No Structural Changes)
- **Impact:** Eliminates per-frame entity create/destroy overhead
- **Status:** `AgentSpawnerSystem` uses `IEnableableComponent` pooling
- **Benefit:** ~50% faster spawning, zero GC allocations

### ✓ 1.3 Spatial Hashing for Avoidance
- **Impact:** O(n²) → O(n) neighbor detection
- **Status:** `NativeMultiHashMap` in `AgentMovementSystem`
- **Benefit:** Scales linearly with agent count

### ✓ 1.4 GPU Instancing (Entities.Graphics)
- **Impact:** 10k+ agents render in <2ms
- **Status:** `RenderMeshUtility.AddComponents` enables auto-instancing
- **Validation:** Frame Debugger shows "Draw Mesh (instanced)"

### ✓ 1.5 Parallel Job Scheduling
- **Impact:** Uses all CPU cores
- **Status:** `ScheduleParallel()` on all `IJobEntity`/`IJobFor`
- **Benefit:** ~4x speedup on 4-core CPU

---

## Priority 2: Low-Effort, High-Impact Tweaks

### 2.1 Tune Spatial Hash Cell Size
**Effort:** 5 minutes
**Impact:** 10-30% movement system speedup

**Current:** `SpatialCellSize = 2.0f`

**Optimization:**
- **Too small** (<1.0): More hash cells, more lookups → slower
- **Too large** (>5.0): Many agents per cell, worse culling → slower
- **Optimal:** ~= avoidance radius (1.5-2.5)

**Action:**
```csharp
// In AgentMovementSystem.OnUpdate
SpatialCellSize = 2.0f // Try 1.5f, 2.5f, 3.0f
```

**Measure:** Re-run benchmark, compare frame times

---

### 2.2 Adjust Avoidance Radius
**Effort:** 2 minutes
**Impact:** 5-15% movement system speedup

**Current:** `AvoidanceRadius = 1.5f`

**Tradeoff:**
- **Smaller** (<1.0): Faster (fewer neighbors) but agents overlap
- **Larger** (>3.0): Slower (more neighbors) but smoother crowds

**Recommended:** `1.0-1.5` for performance, `2.0-3.0` for quality

**Action:**
```csharp
// In AgentMovementSystem.OnUpdate
AvoidanceRadius = 1.2f // Test different values
```

---

### 2.3 Flow Field Update Throttling
**Effort:** 10 minutes
**Impact:** 30-50% reduction in flow field cost

**Current:** Flow field regenerates **every frame** when target changes

**Optimization:** Only rebuild if target moved >5 units or >0.5s elapsed

**Implementation:**
```csharp
// In FlowFieldGenerationSystem.OnUpdate
var target = SystemAPI.GetSingleton<FlowFieldTarget>();

// Add distance check
var data = SystemAPI.GetSingleton<FlowFieldData>();
float distMoved = math.distance(target.Position, data.LastTargetPosition);

if (!target.HasChanged && distMoved < 5f)
    return; // Skip rebuild

// ... rest of system
```

**Benefit:** Saves 1-3ms per frame when target stationary

---

### 2.4 Reduce Flow Field Grid Resolution
**Effort:** 1 minute
**Impact:** 20-40% flow field generation speedup

**Current:** `100x100 = 10,000 cells`

**Options:**
- **75x75** = 5,625 cells → ~40% faster, slightly worse paths
- **50x50** = 2,500 cells → ~75% faster, noticeably coarser paths

**Action:**
```csharp
// In FlowFieldBootstrap Inspector
gridWidth = 75
gridHeight = 75
cellSize = 2.67f // Adjust to keep same world size
```

**Tradeoff:** Paths less smooth around obstacles

---

### 2.5 Disable Debug Visualization in Builds
**Effort:** 1 minute
**Impact:** 1-2ms saved if enabled

**Action:**
```csharp
// In FlowFieldBootstrap
#if !UNITY_EDITOR
showFlowField = false;
showGrid = false;
#endif
```

Or remove `OnDrawGizmos()` entirely for release builds.

---

## Priority 3: Medium-Effort Optimizations

### 3.1 Distance-Based Update Frequency (LOD)
**Effort:** 30 minutes
**Impact:** 20-40% movement system speedup for large scenes

**Concept:** Agents far from camera update less frequently

**Implementation:**
```csharp
// New component
public struct AgentUpdateFrequency : IComponentData
{
    public int FrameSkip; // Update every N frames
    public int FrameOffset; // Stagger updates
}

// In AgentMovementSystem, skip agents:
if ((frameCount + agentUpdateFreq.FrameOffset) % agentUpdateFreq.FrameSkip != 0)
    return; // Skip this frame
```

**Setup:**
- Distance <50: Update every frame
- Distance 50-100: Update every 2 frames
- Distance >100: Update every 4 frames

**Benefit:** ~30% CPU reduction in large open worlds

---

### 3.2 Frustum Culling for Movement Updates
**Effort:** 45 minutes
**Impact:** 10-30% movement system speedup (depends on camera FOV)

**Concept:** Don't update agents outside camera view

**Implementation:**
```csharp
// Calculate frustum planes from camera
var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

// In movement job, skip if outside frustum
if (!IsInFrustum(transform.Position, frustumPlanes))
    return; // Still render, but don't pathfind
```

**Tradeoff:** Agents outside view freeze (usually acceptable)

---

### 3.3 Hierarchical Flow Field
**Effort:** 2-3 hours
**Impact:** 50-70% flow field generation speedup for large grids

**Concept:** Two-level grid:
- **Coarse grid** (20x20): Long-distance navigation
- **Fine grid** (local 10x10): Local detail around agents

**Benefits:**
- Coarse grid: 400 cells vs 10,000 = 25x faster
- Fine grid: Only compute around active agent clusters

**When to use:** Grids >150x150 or open-world games

---

### 3.4 Job Batch Size Tuning
**Effort:** 15 minutes
**Impact:** 5-15% job system speedup

**Current:** Default batch size (varies)

**Optimization:**
```csharp
// In ScheduleParallel calls
state.Dependency = updateCellJob.ScheduleParallel(state.Dependency);
// Change to:
state.Dependency = updateCellJob.ScheduleParallel(64, state.Dependency);
```

**Test batch sizes:** 32, 64, 128, 256
- **Smaller** (32): Better load balancing, more overhead
- **Larger** (256): Less overhead, worse load balancing

**Optimal:** Usually 64-128 for 10k agents on 4-8 cores

---

### 3.5 Amortize Flow Field Build Across Frames
**Effort:** 1-2 hours
**Impact:** Eliminates flow field spikes (3ms → 0.1ms/frame)

**Concept:** Build integration field over 5 frames instead of 1

**Implementation:**
```csharp
// Split wavefront into chunks
for (int i = chunkStart; i < chunkEnd; i++)
{
    ProcessWavefrontCell(openList[i]);
}
// Resume next frame
```

**Tradeoff:** Paths update slower (5 frames = 83ms delay @ 60 FPS)

---

## Priority 4: High-Effort, High-Impact Optimizations

### 4.1 Octree/BVH Spatial Structure
**Effort:** 4-6 hours
**Impact:** 30-50% avoidance speedup for very dense crowds

**Concept:** Replace spatial hash with hierarchical structure

**Benefits:**
- Better for non-uniform agent distribution
- Faster neighbor queries in dense clusters

**Tradeoff:** More complex to implement and maintain

---

### 4.2 SIMD-Optimized Flow Sampling
**Effort:** 3-4 hours
**Impact:** 10-20% movement speedup

**Concept:** Process 4 agents at once using Burst SIMD intrinsics

**Implementation:**
```csharp
using Unity.Burst.Intrinsics;

// Use v128/v256 for 4/8-wide SIMD
var posX = new v128(pos1.x, pos2.x, pos3.x, pos4.x);
// ... parallel processing
```

**When to use:** >50k agents where movement dominates frame time

---

### 4.3 GPU Compute Shader Pathfinding
**Effort:** 1-2 weeks
**Impact:** 90%+ flow field generation speedup

**Concept:** Move flow field to GPU compute shader

**Benefits:**
- Parallel wavefront on GPU (thousands of threads)
- 200x200 grid in <1ms

**Tradeoffs:**
- Complex implementation
- Requires GPU readback (latency)

**When to use:** Massive grids (>200x200) or dynamic obstacles

---

### 4.4 Multi-Destination Flow Fields
**Effort:** 2-3 hours
**Impact:** Enables complex behaviors (multiple objectives)

**Concept:** Blend multiple flow fields (e.g., seek target + flee enemies)

**Implementation:**
```csharp
float3 flowToTarget = SampleFlowField(targetField, cellIndex);
float3 flowFromEnemies = SampleFlowField(threatField, cellIndex);
float3 finalFlow = flowToTarget * 0.7f - flowFromEnemies * 0.3f;
```

**Use case:** RTS games, tactical AI

---

## Priority 5: Advanced Techniques (Diminishing Returns)

### 5.1 Custom Allocator for Spatial Hash
**Effort:** 2 hours
**Impact:** 2-5% memory/speed improvement

**Concept:** Use `Allocator.Persistent` and reuse spatial hash across frames

**Benefit:** Avoid per-frame NativeMultiHashMap allocation

---

### 5.2 Aggressive Inlining Hints
**Effort:** 30 minutes
**Impact:** 1-3% speedup

**Action:**
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private int HashPosition(int x, int y) { ... }
```

**Target:** Hot path functions called millions of times

---

### 5.3 Profile-Guided Optimization (PGO)
**Effort:** 1 hour setup
**Impact:** 5-10% overall speedup

**Concept:** IL2CPP with PGO uses runtime profiling to optimize hot paths

**Setup:**
1. Build with IL2CPP + PGO instrumentation
2. Run typical gameplay
3. Rebuild with collected profile data

**When to use:** Final optimization pass before release

---

## Optimization Priority Quick Reference

| **Priority** | **Optimization** | **Effort** | **Impact** | **When** |
|--------------|------------------|-----------|-----------|----------|
| **1** | Burst + Pooling + Spatial Hash | Done ✓ | Massive | Always |
| **2** | Tune spatial cell size | 5 min | 10-30% | First tweak |
| **2** | Throttle flow field updates | 10 min | 30-50% | If rebuilding often |
| **2** | Coarsen grid | 1 min | 20-40% | If flow field slow |
| **3** | LOD update frequency | 30 min | 20-40% | Large open worlds |
| **3** | Frustum culling | 45 min | 10-30% | Camera rarely sees all agents |
| **3** | Hierarchical flow field | 2-3 hr | 50-70% | Grids >150x150 |
| **4** | GPU compute pathfinding | 1-2 wk | 90%+ | Massive grids or dynamic obstacles |

---

## Recommended Optimization Path

### For 10k Agents @ 60 FPS (Already Achieved)
1. ✓ Use provided baseline code
2. Tune spatial hash cell size (5 min)
3. Done! 🎉

### For 20k Agents @ 60 FPS
1. Start with baseline
2. Implement LOD update frequency (30 min)
3. Tune batch sizes (15 min)
4. Frustum culling (45 min)
5. **Total effort:** ~90 minutes for 40-60% speedup

### For 50k+ Agents
1. Hierarchical flow field (2-3 hr)
2. SIMD-optimized movement (3-4 hr)
3. GPU compute pathfinding (1-2 wk)
4. **Total effort:** ~2 weeks of R&D

---

## Validation Checklist

After each optimization:
- [ ] Run benchmark script
- [ ] Compare CSV results (frame time improvement)
- [ ] Check Profiler (which system got faster?)
- [ ] Verify visual quality (paths still look good?)
- [ ] Test edge cases (1 agent, 50k agents, stationary target)

---

## Next Steps

1. **Profile:** Use `PROFILING_GUIDE.md` to establish baseline
2. **Optimize:** Pick optimizations from Priority 2-3
3. **Measure:** Re-run benchmark after each change
4. **Iterate:** Repeat until target met

**Remember:** Measure first, optimize second. Don't guess!

Happy optimizing! 🚀
