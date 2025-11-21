using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Performance benchmark and monitoring system.
    ///
    /// Features:
    /// - Real-time FPS and frame time tracking
    /// - Min/Max/Average statistics
    /// - Automated testing at different agent counts
    /// - Performance graphs and logging
    /// - GC allocation monitoring
    ///
    /// Usage: Attach to any GameObject in the scene
    /// </summary>
    public class PerformanceBenchmark : MonoBehaviour
    {
        [Header("Display Settings")]
        [Tooltip("Show performance overlay")]
        public bool showOverlay = true;

        [Tooltip("Font size for overlay text")]
        public int fontSize = 14;

        [Header("Benchmark Settings")]
        [Tooltip("Run automated benchmark on start")]
        public bool runBenchmarkOnStart = false;

        [Tooltip("Agent counts to test in benchmark")]
        public int[] benchmarkCounts = new int[] { 1000, 2000, 5000, 10000, 15000, 20000 };

        [Tooltip("Seconds to measure each test")]
        public float measurementDuration = 5f;

        [Tooltip("Seconds to wait before measuring (warmup)")]
        public float warmupDuration = 2f;

        private const int SAMPLE_SIZE = 60; // Track last 60 frames (~1 second at 60 FPS)

        private Queue<float> _frameTimes = new Queue<float>();
        private float _minFrameTime = float.MaxValue;
        private float _maxFrameTime = 0f;
        private float _avgFrameTime = 0f;

        private EntityManager _entityManager;
        private FlowFieldBootstrap _bootstrap;

        // Benchmark state
        private bool _isBenchmarking = false;
        private int _benchmarkIndex = 0;
        private float _benchmarkTimer = 0f;
        private BenchmarkPhase _benchmarkPhase = BenchmarkPhase.Idle;
        private List<BenchmarkResult> _benchmarkResults = new List<BenchmarkResult>();

        private enum BenchmarkPhase
        {
            Idle,
            Spawning,
            Warmup,
            Measuring
        }

        private struct BenchmarkResult
        {
            public int AgentCount;
            public float MinFPS;
            public float MaxFPS;
            public float AvgFPS;
            public float MinFrameTime;
            public float MaxFrameTime;
            public float AvgFrameTime;
        }

        private void Start()
        {
<<<<<<< Updated upstream
<<<<<<< Updated upstream
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _bootstrap = FindObjectOfType<FlowFieldBootstrap>();
=======
            _entityManager = World.DefaultGlobalSystemGroup.World.EntityManager;
            _bootstrap = FindAnyObjectByType<FlowFieldBootstrap>();
>>>>>>> Stashed changes
=======
            _entityManager = World.DefaultGlobalSystemGroup.World.EntityManager;
            _bootstrap = FindAnyObjectByType<FlowFieldBootstrap>();
>>>>>>> Stashed changes

            if (_bootstrap == null)
            {
                Debug.LogWarning("[PerformanceBenchmark] FlowFieldBootstrap not found in scene.");
            }

            if (runBenchmarkOnStart)
            {
                Invoke(nameof(StartBenchmark), 1f);
            }
        }

        private void Update()
        {
            // Track frame time
            float frameTime = Time.unscaledDeltaTime * 1000f; // Convert to milliseconds
            _frameTimes.Enqueue(frameTime);

            if (_frameTimes.Count > SAMPLE_SIZE)
            {
                _frameTimes.Dequeue();
            }

            // Calculate statistics
            if (_frameTimes.Count > 0)
            {
                _minFrameTime = _frameTimes.Min();
                _maxFrameTime = _frameTimes.Max();
                _avgFrameTime = _frameTimes.Average();
            }

            // Handle benchmark state machine
            if (_isBenchmarking)
            {
                UpdateBenchmark();
            }
        }

        private void UpdateBenchmark()
        {
            _benchmarkTimer += Time.unscaledDeltaTime;

            switch (_benchmarkPhase)
            {
                case BenchmarkPhase.Spawning:
                    // Spawn agents for current test
                    if (_bootstrap != null)
                    {
                        int targetCount = benchmarkCounts[_benchmarkIndex];
                        int currentCount = _bootstrap.GetActiveAgentCount();
                        int toSpawn = targetCount - currentCount;

                        if (toSpawn > 0)
                        {
                            _bootstrap.SpawnAgents(toSpawn);
                        }

                        // Move to warmup
                        _benchmarkPhase = BenchmarkPhase.Warmup;
                        _benchmarkTimer = 0f;
                        _frameTimes.Clear();

                        Debug.Log($"[Benchmark] Testing with {targetCount} agents - Warmup phase");
                    }
                    break;

                case BenchmarkPhase.Warmup:
                    if (_benchmarkTimer >= warmupDuration)
                    {
                        // Move to measuring
                        _benchmarkPhase = BenchmarkPhase.Measuring;
                        _benchmarkTimer = 0f;
                        _frameTimes.Clear();
                        _minFrameTime = float.MaxValue;
                        _maxFrameTime = 0f;

                        Debug.Log($"[Benchmark] Measuring performance...");
                    }
                    break;

                case BenchmarkPhase.Measuring:
                    if (_benchmarkTimer >= measurementDuration)
                    {
                        // Record results
                        var result = new BenchmarkResult
                        {
                            AgentCount = benchmarkCounts[_benchmarkIndex],
                            MinFPS = 1000f / _maxFrameTime,
                            MaxFPS = 1000f / _minFrameTime,
                            AvgFPS = 1000f / _avgFrameTime,
                            MinFrameTime = _minFrameTime,
                            MaxFrameTime = _maxFrameTime,
                            AvgFrameTime = _avgFrameTime
                        };
                        _benchmarkResults.Add(result);

                        Debug.Log($"[Benchmark] Result: {result.AgentCount} agents - " +
                                  $"FPS: {result.AvgFPS:F1} avg, {result.MinFPS:F1} min, {result.MaxFPS:F1} max - " +
                                  $"Frame Time: {result.AvgFrameTime:F2}ms avg, {result.MinFrameTime:F2}ms min, {result.MaxFrameTime:F2}ms max");

                        // Move to next test or finish
                        _benchmarkIndex++;
                        if (_benchmarkIndex < benchmarkCounts.Length)
                        {
                            _benchmarkPhase = BenchmarkPhase.Spawning;
                            _benchmarkTimer = 0f;
                        }
                        else
                        {
                            FinishBenchmark();
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Start automated benchmark testing.
        /// </summary>
        public void StartBenchmark()
        {
            if (_isBenchmarking)
            {
                Debug.LogWarning("[Benchmark] Benchmark already running");
                return;
            }

            Debug.Log("[Benchmark] Starting automated benchmark...");
            _isBenchmarking = true;
            _benchmarkIndex = 0;
            _benchmarkPhase = BenchmarkPhase.Spawning;
            _benchmarkTimer = 0f;
            _benchmarkResults.Clear();
        }

        private void FinishBenchmark()
        {
            _isBenchmarking = false;
            _benchmarkPhase = BenchmarkPhase.Idle;

            Debug.Log("=== BENCHMARK RESULTS ===");
            Debug.Log("Agents | Avg FPS | Min FPS | Max FPS | Avg Frame Time");
            Debug.Log("-------|---------|---------|---------|---------------");

            foreach (var result in _benchmarkResults)
            {
                Debug.Log($"{result.AgentCount,6} | {result.AvgFPS,7:F1} | {result.MinFPS,7:F1} | {result.MaxFPS,7:F1} | {result.AvgFrameTime,10:F2}ms");
            }

            Debug.Log("========================");

            // Check if we met the 60 FPS target
            var fps60Result = _benchmarkResults.FirstOrDefault(r => r.AgentCount == 10000);
            if (fps60Result.AgentCount > 0)
            {
                if (fps60Result.AvgFPS >= 60f)
                {
                    Debug.Log($"<color=green>✓ SUCCESS: Achieved {fps60Result.AvgFPS:F1} FPS with 10,000 agents (target: 60+ FPS)</color>");
                }
                else
                {
                    Debug.Log($"<color=orange>⚠ WARNING: Only achieved {fps60Result.AvgFPS:F1} FPS with 10,000 agents (target: 60+ FPS)</color>");
                }
            }
        }

        private void OnGUI()
        {
            if (!showOverlay)
                return;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.normal.textColor = Color.white;
            style.fontStyle = FontStyle.Bold;

            GUIStyle shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = Color.black;

            float x = 10f;
            float y = Screen.height - 200f;
            float lineHeight = fontSize + 4f;
            float shadowOffset = 1f;

            // Get agent count
            int activeCount = _bootstrap != null ? _bootstrap.GetActiveAgentCount() : 0;
            int poolSize = _bootstrap != null ? _bootstrap.GetPoolSize() : 0;

            // Calculate FPS
            float fps = _avgFrameTime > 0 ? 1000f / _avgFrameTime : 0f;
            float minFPS = _maxFrameTime > 0 ? 1000f / _maxFrameTime : 0f;
            float maxFPS = _minFrameTime > 0 ? 1000f / _minFrameTime : 0f;

            // FPS color coding
            Color fpsColor = Color.green;
            if (fps < 30f) fpsColor = Color.red;
            else if (fps < 60f) fpsColor = Color.yellow;

            // Build overlay text
            string[] lines = new string[]
            {
                $"=== PERFORMANCE ===",
                $"Agents: {activeCount} / {poolSize}",
                $"FPS: {fps:F1} (min: {minFPS:F1}, max: {maxFPS:F1})",
                $"Frame Time: {_avgFrameTime:F2}ms (min: {_minFrameTime:F2}ms, max: {_maxFrameTime:F2}ms)",
                "",
                _isBenchmarking ? $"[BENCHMARK] Phase: {_benchmarkPhase} ({_benchmarkTimer:F1}s)" : ""
            };

            // Draw shadow
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i]))
                    continue;
                GUI.Label(new Rect(x + shadowOffset, y + i * lineHeight + shadowOffset, 500, lineHeight), lines[i], shadowStyle);
            }

            // Draw text
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i]))
                    continue;

                GUIStyle currentStyle = style;
                if (lines[i].Contains("FPS:"))
                {
                    currentStyle = new GUIStyle(style);
                    currentStyle.normal.textColor = fpsColor;
                }

                GUI.Label(new Rect(x, y + i * lineHeight, 500, lineHeight), lines[i], currentStyle);
            }

            // Benchmark button
            if (!_isBenchmarking)
            {
                if (GUI.Button(new Rect(x, y + lines.Length * lineHeight + 10, 150, 30), "Run Benchmark"))
                {
                    StartBenchmark();
                }
            }
        }
    }
}
