# Unity DOTS Profiling Guide - Flow Field Pathfinding

## Overview
This guide explains how to measure, profile, and optimize the flow field system using Unity's Profiler and custom metrics.

**Performance Target:** 60 FPS (16.6ms frame time) with 10,000 agents

---

## 1. Unity Profiler Setup

### 1.1 Open Profiler
1. **Window → Analysis → Profiler** (or `Ctrl+7`)
2. **Dock it** next to Scene/Game view for real-time monitoring

### 1.2 Configure Profiler
**Top toolbar:**
- **Play mode:** Click **Record** before entering Play mode
- **Deep Profile:** **✗ OFF** (causes overhead; use only for debugging)
- **Call Stacks:** **✗ OFF** (unless debugging specific system)

**Module selection (left panel):**
- ✓ **CPU Usage**
- ✓ **GPU Usage**
- ✓ **Memory**
- ✓ **Rendering**
- ✓ **Jobs** (critical for DOTS)

### 1.3 Frame Selection
- **Click and drag** on timeline to select frames
- **Selected Frame:** Shows detailed breakdown
- **Target:** Select frames during stable gameplay (skip startup spikes)

---

## 2. Key Metrics to Monitor

### 2.1 CPU Usage Module

**Target Breakdown (10k agents @ 60 FPS):**

| **Category** | **Target Time** | **Budget** | **Notes** |
|--------------|----------------|-----------|-----------|
| **Total Frame** | < 16.6ms | 100% | Overall frame time |
| **Main Thread** | < 12ms | 70% | Non-parallel systems |
| **Job System** | < 8ms | Parallel | Worker threads (overlaps main) |
| **Rendering** | < 3ms | 20% | GPU submission |
| **GC Allocations** | 0 bytes | 0% | Per-frame allocations (critical!) |

**How to measure:**
1. Select **CPU Usage** module
2. In **Timeline view:**
   - **Green bar:** Main thread
   - **Blue bars:** Worker threads (Jobs)
   - **Red spikes:** GC collections (BAD!)
3. Click on frame → **Hierarchy view** shows system breakdown

### 2.2 Jobs Module

**Target Jobs (should appear):**
- `UpdateCellIndexJob` → ~1-2ms (10k agents)
- `CalculateVelocityJob` → ~3-5ms (spatial hashing + flow sampling)
- `ApplyMovementJob` → ~0.5-1ms (simple integration)
- `BuildFlowDirectionFieldJob` → ~0.5-1ms (parallel flow field build)

**What to check:**
- **Job count:** Should see multiple worker threads (blue bars)
- **Job time vs. Main thread:** Jobs should run concurrently with main thread
- **Wait times:** Long purple bars = job starvation (not enough work)

**Steps:**
1. **Profiler → Jobs module**
2. Look at **Job Scheduling** timeline
3. Verify jobs are **parallel** (overlapping blue bars)

### 2.3 Memory Module

**Targets:**
- **Total Allocated:** < 100 MB (for 10k agents)
- **GC Allocated:** 0 bytes/frame (critical!)
- **Native Allocated:** ~10-20 MB (NativeArrays, spatial hash)

**How to check:**
1. **Profiler → Memory module**
2. **Detailed view → GC Alloc in Frame**
3. **If > 0 bytes:**
   - Click on spike → **Sample → Call stack**
   - Identify source (likely managed code in MonoBehaviour)

**Common culprits:**
- LINQ queries (`Where`, `Select`)
- `foreach` on non-NativeContainer
- `Debug.Log` in hot path
- Temporary array/list creation

### 2.4 Rendering Module

**Targets (10k agents):**
- **SetPass calls:** < 10 (ideally 1-2 with GPU instancing)
- **Draw calls:** < 50 (instanced batches)
- **Triangles:** ~20k (2 tris/quad × 6 faces × 10k cubes = 120k, but culled)
- **Batches:** < 100

**Steps:**
1. **Profiler → Rendering module**
2. Check **Batches count:**
   - High batches = instancing broken
   - Should see "GPU Instanced" in Frame Debugger
3. **Window → Analysis → Frame Debugger** → Step through draw calls
   - Look for **"Draw Mesh (instanced)"** entries
   - Each should render hundreds of agents

---

## 3. System-Level Profiling

### 3.1 Identify Slow Systems

**In Profiler CPU Hierarchy:**
1. Expand **PlayerLoop**
2. Expand **SimulationSystemGroup**
3. Look for systems taking >2ms:
   - `AgentMovementSystem.OnUpdate`
   - `FlowFieldGenerationSystem.OnUpdate`

**Expected times (10k agents):**
- `AgentSpawnerSystem`: < 0.1ms (only runs once)
- `FlowFieldGenerationSystem`: 1-3ms (only when target changes)
- `AgentMovementSystem`: 5-8ms (every frame, most expensive)

### 3.2 Job-Level Profiling

Add custom profiler markers in code:

```csharp
using Unity.Profiling;

public partial struct AgentMovementSystem : ISystem
{
    static ProfilerMarker s_UpdateCellMarker = new ProfilerMarker("UpdateCellIndex");
    static ProfilerMarker s_VelocityMarker = new ProfilerMarker("CalculateVelocity");

    public void OnUpdate(ref SystemState state)
    {
        s_UpdateCellMarker.Begin();
        // ... job scheduling
        s_UpdateCellMarker.End();
    }
}
```

**Benefits:**
- Shows up in Profiler with custom name
- Can nest markers for detailed breakdown

### 3.3 Burst Inspector

**Check Burst compilation:**
1. **Jobs → Burst → Burst Inspector**
2. Select a job (e.g., `CalculateVelocityJob`)
3. **Assembly tab** shows generated SIMD code
4. Look for:
   - ✓ **Vectorized loops** (e.g., `vmovaps`, `vaddps`)
   - ✗ **Managed calls** (function calls to C# = not Bursted)

**If Burst not working:**
- Check `[BurstCompile]` attribute on job/system
- Check for unsupported types (classes, managed collections)
- Check console for Burst warnings

---

## 4. Profiler Workflow (Step-by-Step)

### 4.1 Baseline Measurement
1. **Set spawn count to 10,000**
2. **Enter Play mode** with Profiler recording
3. **Let simulation run for 10 seconds** (skip startup)
4. **Pause game** (`Ctrl+Shift+P`)
5. **In Profiler:**
   - Select stable frame range (e.g., frames 300-600)
   - Note **average frame time** in Timeline
6. **Take screenshot** of Profiler for reference

### 4.2 Identify Bottleneck
**CPU-bound (common):**
- Frame time stays high even when standing still
- Jobs module shows long job times
- **Solution:** Optimize jobs, reduce agent count, coarsen grid

**GPU-bound (rare with cubes):**
- Frame time increases when looking at agents
- Rendering module shows high draw time
- **Solution:** LOD, frustum culling, simpler meshes

**Memory-bound (critical if present):**
- Red GC spikes in timeline
- Frame hitches every few seconds
- **Solution:** Eliminate per-frame allocations

**How to confirm:**
1. **GPU test:** Look away from agents (camera at sky)
   - If FPS increases → GPU-bound
   - If FPS same → CPU-bound
2. **CPU test:** Reduce agent count by 50%
   - If FPS doubles → CPU-bound (scales linearly)

### 4.3 Optimization Iteration
1. **Make change** (e.g., disable avoidance)
2. **Re-profile** with same scenario
3. **Compare frame times:**
   - Use **Profiler Compare** feature (Profiler → Load → Compare)
4. **Measure improvement:**
   - Example: 12ms → 8ms = **33% faster**

---

## 5. Benchmark Script Usage

### 5.1 Running Automated Benchmark
1. Ensure `PerformanceBenchmark.cs` attached to FlowFieldManager
2. **Set Auto Run:** ✓ enabled (or click "Run Benchmark" in GUI)
3. **Wait:** ~2 minutes (tests 1k, 5k, 10k, 20k agents)
4. **Results:** Saved to `Application.persistentDataPath/benchmark_results.csv`

**Output location:**
- **Windows:** `C:\Users\<User>\AppData\LocalLow\<Company>\<Project>\benchmark_results.csv`
- **Mac:** `~/Library/Application Support/<Company>/<Project>/benchmark_results.csv`
- **Linux:** `~/.config/unity3d/<Company>/<Project>/benchmark_results.csv`

### 5.2 Analyzing CSV Results
Open in Excel/Google Sheets:

| AgentCount | FrameTimeMs | FPS | GCAllocMB | MainThreadMs |
|------------|-------------|-----|-----------|--------------|
| 1000       | 2.5         | 400 | 0.00      | 2.3          |
| 5000       | 8.2         | 122 | 0.00      | 7.9          |
| 10000      | 14.5        | 69  | 0.00      | 13.8         |
| 20000      | 28.3        | 35  | 0.01      | 27.1         |

**Create graphs:**
- **X-axis:** AgentCount
- **Y-axis 1:** FrameTimeMs (target line at 16.6ms)
- **Y-axis 2:** FPS (target line at 60)

**Interpretation:**
- **Linear scaling:** Frame time doubles when agents double = good scaling
- **Quadratic scaling:** Frame time quadruples = bad (e.g., O(n²) collision)
- **GC > 0:** Investigate allocations

### 5.3 Comparing Builds

**Before optimization:**
```
10k agents: 18.5ms (54 FPS)
```

**After optimization (e.g., spatial hash cell size tuned):**
```
10k agents: 14.2ms (70 FPS)
```

**Improvement:** 23% faster

---

## 6. Common Performance Issues

### Issue 1: Low FPS with Few Agents (<1000)
**Symptoms:** Even 500 agents struggle to hit 60 FPS

**Causes:**
- Burst not enabled → Check Player Settings → Allow unsafe code
- Safety checks enabled in Burst → Disable for release builds
- Deep Profiling enabled → Disable

**Fix:**
1. **Jobs → Burst → Burst AOT Settings → Safety Checks: Off**
2. **Player Settings → Scripting Backend: IL2CPP**

### Issue 2: GC Allocations Every Frame
**Symptoms:** Red spikes in Profiler, periodic stuttering

**Causes:**
- `Debug.Log` in update loop
- LINQ in MonoBehaviour
- Temporary List/Array creation

**Fix:**
1. Remove all `Debug.Log` from hot paths
2. Use Profiler memory module → Identify allocating code
3. Replace with NativeArrays or cache collections

### Issue 3: Jobs Not Parallelizing
**Symptoms:** Jobs run sequentially (blue bars stacked, not overlapping)

**Causes:**
- Job dependencies too strict
- Not enough work per job (overhead dominates)
- Using `Schedule()` instead of `ScheduleParallel()`

**Fix:**
1. Use `ScheduleParallel` for `IJobEntity` / `IJobFor`
2. Increase batch size in `ScheduleParallel(state.Dependency, batchSize: 128)`
3. Check `state.Dependency` chains aren't forcing sequential execution

### Issue 4: Flow Field Generation Slow (>5ms)
**Symptoms:** Lag when moving target

**Causes:**
- Grid too large (>150x150 cells)
- Integration field algorithm inefficient

**Fix:**
1. Reduce grid size or increase cell size
2. Cache flow field, only rebuild when target moves >10 units
3. Use hierarchical flow field (coarse + fine grid)

---

## 7. Profiler Checklist (Print This!)

Before claiming "optimization complete," verify:

- [ ] **Frame time** < 16.6ms (60 FPS) with target agent count
- [ ] **GC allocations** = 0 bytes per frame
- [ ] **Job system** shows parallel blue bars (not stacked)
- [ ] **Rendering** uses GPU instancing (< 10 SetPass calls)
- [ ] **Burst** Inspector shows vectorized assembly
- [ ] **Benchmark CSV** shows linear scaling (not quadratic)
- [ ] **No red spikes** (GC) in 60-second test run
- [ ] **Profiler Compare** shows improvement vs. baseline

---

## 8. Advanced: Frame Pacing Analysis

### 8.1 Detect Microstuttering
**Symptoms:** FPS appears high (58-60) but feels choppy

**Cause:** Frame time variance (some frames 10ms, others 20ms)

**Measure:**
1. Export Profiler data → **Profiler → Save**
2. Open in Excel, calculate **standard deviation** of frame times
3. **Target:** StdDev < 2ms for smooth gameplay

**Fix:**
- Spread work across frames (LOD updates every Nth frame)
- Use `Time.deltaTime` clamping

### 8.2 1% / 0.1% Lows
**Definition:** Worst 1% of frames (frame drops)

**Measure:**
- Benchmark script calculates P95 (95th percentile)
- **Target:** P95 < 20ms (no frames worse than 50 FPS)

**If P95 > 30ms:**
- Likely GC spike → Fix allocations
- Likely one-time cost (flow field rebuild) → Amortize over frames

---

## 9. Profiling on Target Hardware

### 9.1 Build and Profile
**Development build:**
1. **File → Build Settings → Development Build: ✓**
2. **Autoconnect Profiler: ✓**
3. **Deep Profiling Support: ✗** (too slow)
4. **Build and Run**

**Profiler connection:**
1. **Profiler → Attach to Player → Select build**
2. Profile as normal (lower FPS expected than Editor)

### 9.2 Release Build Test
**Final validation:**
1. Uncheck Development Build
2. **Build Settings → IL2CPP + Burst** enabled
3. Test on minimum spec hardware (4-core CPU, GTX 1050)
4. **Target:** Still hit 60 FPS with target agent count

---

## 10. Next Steps

- See **OPTIMIZATION_ROADMAP.md** for prioritized fixes
- See **TROUBLESHOOTING.md** for common errors
- Experiment with LOD, culling, and multi-grid approaches

**Happy profiling!**
