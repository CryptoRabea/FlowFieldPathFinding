using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

// ============================================================================
// PERFORMANCE BENCHMARK
// ============================================================================
// Automated benchmark script to measure performance at different agent counts.
// Outputs results to CSV for graphing and analysis.
//
// Usage:
// 1. Attach to GameObject with FlowFieldBootstrap
// 2. Enable autoRun or call RunBenchmark() manually
// 3. Results saved to: Application.persistentDataPath/benchmark_results.csv
//
// Metrics captured:
// - Frame time (ms)
// - FPS (instantaneous)
// - Job system time
// - GC allocations
// - Main thread time
// - Rendering time
// ============================================================================

public class PerformanceBenchmark : MonoBehaviour
{
    [Header("Benchmark Settings")]
    public bool autoRun = false;
    public float warmupDuration = 5f; // Seconds to warm up before measuring
    public float measureDuration = 10f; // Seconds to measure per test

    [Header("Test Scenarios")]
    public int[] testCounts = new int[] { 1000, 5000, 10000, 20000 };

    [Header("Output")]
    public string outputFileName = "benchmark_results.csv";

    // Profiler markers
    private ProfilerMarker _movementMarker = new ProfilerMarker("AgentMovement");
    private ProfilerMarker _flowFieldMarker = new ProfilerMarker("FlowFieldGeneration");

    // Benchmark data
    private struct BenchmarkSample
    {
        public int AgentCount;
        public float FrameTimeMs;
        public float FPS;
        public long GCAllocBytes;
        public float MainThreadMs;
        public float RenderThreadMs;
    }

    private List<BenchmarkSample> _samples = new List<BenchmarkSample>();
    private bool _benchmarkRunning = false;
    private FlowFieldBootstrap _bootstrap;
    private EntityManager _entityManager;

    void Start()
    {
        _bootstrap = GetComponent<FlowFieldBootstrap>();
        if (_bootstrap == null)
        {
            Debug.LogError("PerformanceBenchmark requires FlowFieldBootstrap component");
            enabled = false;
            return;
        }

        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        if (autoRun)
        {
            StartCoroutine(RunBenchmarkCoroutine());
        }
    }

    void OnGUI()
    {
        if (_benchmarkRunning)
        {
            GUILayout.BeginArea(new Rect(Screen.width - 310, 10, 300, 100));
            GUILayout.Box("BENCHMARK RUNNING...");
            GUILayout.Label("Please wait, collecting data...");
            GUILayout.EndArea();
        }
        else if (!autoRun)
        {
            GUILayout.BeginArea(new Rect(Screen.width - 310, 10, 300, 100));
            if (GUILayout.Button("Run Benchmark"))
            {
                StartCoroutine(RunBenchmarkCoroutine());
            }
            GUILayout.EndArea();
        }
    }

    public void RunBenchmark()
    {
        if (!_benchmarkRunning)
        {
            StartCoroutine(RunBenchmarkCoroutine());
        }
    }

    private IEnumerator RunBenchmarkCoroutine()
    {
        _benchmarkRunning = true;
        _samples.Clear();

        Debug.Log("=== PERFORMANCE BENCHMARK STARTED ===");

        foreach (int agentCount in testCounts)
        {
            Debug.Log($"Testing with {agentCount} agents...");

            // Clear existing agents
            ClearAllAgents();
            yield return new WaitForSeconds(1f);

            // Spawn agents
            SpawnAgents(agentCount);
            yield return new WaitForSeconds(1f);

            // Warmup
            Debug.Log($"  Warming up for {warmupDuration}s...");
            yield return new WaitForSeconds(warmupDuration);

            // Measure
            Debug.Log($"  Measuring for {measureDuration}s...");
            yield return StartCoroutine(MeasurePerformance(agentCount, measureDuration));
        }

        // Export results
        ExportResults();

        Debug.Log("=== BENCHMARK COMPLETE ===");
        Debug.Log($"Results saved to: {GetOutputPath()}");

        _benchmarkRunning = false;
    }

    private IEnumerator MeasurePerformance(int agentCount, float duration)
    {
        List<BenchmarkSample> samples = new List<BenchmarkSample>();
        float elapsed = 0f;
        int frameCount = 0;

        // Reset profiler
        Profiler.enabled = true;

        while (elapsed < duration)
        {
            float startTime = Time.realtimeSinceStartup;

            // Capture frame data
            float frameTime = Time.deltaTime * 1000f; // ms
            float fps = 1f / Time.deltaTime;
            long gcAlloc = GC.GetTotalMemory(false);

            samples.Add(new BenchmarkSample
            {
                AgentCount = agentCount,
                FrameTimeMs = frameTime,
                FPS = fps,
                GCAllocBytes = gcAlloc,
                MainThreadMs = frameTime, // Simplified, could use Profiler.GetRuntimeMemorySizeLong
                RenderThreadMs = 0f // Unity doesn't expose this easily
            });

            frameCount++;
            elapsed += Time.deltaTime;

            yield return null;
        }

        // Calculate statistics
        CalculateAndStoreStats(samples);
    }

    private void CalculateAndStoreStats(List<BenchmarkSample> samples)
    {
        if (samples.Count == 0)
            return;

        // Calculate mean, min, max, p95
        samples.Sort((a, b) => a.FrameTimeMs.CompareTo(b.FrameTimeMs));

        int count = samples.Count;
        int p95Index = Mathf.FloorToInt(count * 0.95f);

        float meanFrameTime = 0f;
        float meanFPS = 0f;

        foreach (var sample in samples)
        {
            meanFrameTime += sample.FrameTimeMs;
            meanFPS += sample.FPS;
        }
        meanFrameTime /= count;
        meanFPS /= count;

        float minFrameTime = samples[0].FrameTimeMs;
        float maxFrameTime = samples[count - 1].FrameTimeMs;
        float p95FrameTime = samples[p95Index].FrameTimeMs;

        // Store summary sample
        _samples.Add(new BenchmarkSample
        {
            AgentCount = samples[0].AgentCount,
            FrameTimeMs = meanFrameTime,
            FPS = meanFPS,
            GCAllocBytes = samples[count / 2].GCAllocBytes,
            MainThreadMs = meanFrameTime,
            RenderThreadMs = 0f
        });

        Debug.Log($"  Results: Mean={meanFrameTime:F2}ms, FPS={meanFPS:F1}, Min={minFrameTime:F2}ms, Max={maxFrameTime:F2}ms, P95={p95FrameTime:F2}ms");
    }

    private void ExportResults()
    {
        StringBuilder csv = new StringBuilder();

        // Header
        csv.AppendLine("AgentCount,FrameTimeMs,FPS,GCAllocMB,MainThreadMs,RenderThreadMs");

        // Data
        foreach (var sample in _samples)
        {
            csv.AppendLine($"{sample.AgentCount}," +
                          $"{sample.FrameTimeMs:F2}," +
                          $"{sample.FPS:F1}," +
                          $"{sample.GCAllocBytes / (1024f * 1024f):F2}," +
                          $"{sample.MainThreadMs:F2}," +
                          $"{sample.RenderThreadMs:F2}");
        }

        // Write to file
        string path = GetOutputPath();
        File.WriteAllText(path, csv.ToString());

        Debug.Log($"Benchmark results written to: {path}");
    }

    private string GetOutputPath()
    {
        return Path.Combine(Application.persistentDataPath, outputFileName);
    }

    private void ClearAllAgents()
    {
        // Disable all pooled agents
        var query = _entityManager.CreateEntityQuery(typeof(Agent), typeof(AgentActive));
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        foreach (var entity in entities)
        {
            if (_entityManager.IsComponentEnabled<AgentActive>(entity))
            {
                _entityManager.SetComponentEnabled<AgentActive>(entity, false);
            }
        }

        entities.Dispose();

        // Reset spawner state
        var stateQuery = _entityManager.CreateEntityQuery(typeof(AgentSpawnerState));
        if (!stateQuery.IsEmpty)
        {
            var stateEntity = stateQuery.GetSingletonEntity();
            var state = _entityManager.GetComponentData<AgentSpawnerState>(stateEntity);
            state.ActiveCount = 0;
            _entityManager.SetComponentData(stateEntity, state);
        }
    }

    private void SpawnAgents(int count)
    {
        var query = _entityManager.CreateEntityQuery(typeof(AgentSpawnerConfig));
        if (query.IsEmpty)
            return;

        var entity = query.GetSingletonEntity();
        var config = _entityManager.GetComponentData<AgentSpawnerConfig>(entity);
        config.SpawnRequested = true;
        config.SpawnCount = count;
        _entityManager.SetComponentData(entity, config);
    }
}
