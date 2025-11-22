using UnityEngine;
using Unity.Entities;

namespace FlowFieldPathfinding
{
    /// <summary>
    /// Runtime performance monitor that detects common performance issues
    /// and provides optimization suggestions.
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        [Header("Monitoring Settings")]
        [Tooltip("Show performance warnings in console")]
        public bool enableWarnings = true;

        [Tooltip("Show performance overlay")]
        public bool showOverlay = true;

        [Tooltip("FPS threshold for performance warnings")]
        public float lowFpsThreshold = 30f;

        [Header("Detection Settings")]
        [Tooltip("Check for rigidbodies every N seconds")]
        public float checkInterval = 5f;

        private float nextCheckTime;
        private int lastRigidbodyCount;
        private int lastAgentCount;
        private float currentFps;
        private bool hasShownPhysicsWarning;

        // FPS tracking
        private float[] fpsBuffer = new float[60];
        private int fpsBufferIndex = 0;

        void Update()
        {
            UpdateFPS();

            if (Time.time >= nextCheckTime)
            {
                PerformChecks();
                nextCheckTime = Time.time + checkInterval;
            }
        }

        void UpdateFPS()
        {
            float fps = 1f / Time.unscaledDeltaTime;
            fpsBuffer[fpsBufferIndex] = fps;
            fpsBufferIndex = (fpsBufferIndex + 1) % fpsBuffer.Length;

            // Calculate average FPS
            float sum = 0;
            foreach (float f in fpsBuffer)
                sum += f;
            currentFps = sum / fpsBuffer.Length;
        }

        void PerformChecks()
        {
            CheckRigidbodies();
            CheckAgentCount();
        }

        void CheckRigidbodies()
        {
            var rigidbodies = FindObjectsOfType<Rigidbody>();
            int count = rigidbodies.Length;

            if (count > 0 && !hasShownPhysicsWarning)
            {
                int dynamicCount = 0;
                int kinematicCount = 0;

                foreach (var rb in rigidbodies)
                {
                    if (rb.isKinematic)
                        kinematicCount++;
                    else
                        dynamicCount++;
                }

                if (dynamicCount > 100 && enableWarnings)
                {
                    Debug.LogWarning($"[PerformanceMonitor] Found {dynamicCount} dynamic Rigidbodies!\n" +
                        $"This may cause severe performance issues with flow field agents.\n" +
                        $"Consider using: Tools > Flow Field > Agent Physics Converter\n" +
                        $"Or add AgentRigidbodyOptimizer components to make them kinematic.");
                    hasShownPhysicsWarning = true;
                }
                else if (kinematicCount > 500 && enableWarnings && !hasShownPhysicsWarning)
                {
                    Debug.LogWarning($"[PerformanceMonitor] Found {kinematicCount} kinematic Rigidbodies.\n" +
                        $"While better than dynamic, removing them entirely would improve performance.\n" +
                        $"Flow field agents don't need physics components.");
                    hasShownPhysicsWarning = true;
                }
            }

            lastRigidbodyCount = count;
        }

        void CheckAgentCount()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
                return;

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(Agent));
            int agentCount = query.CalculateEntityCount();
            query.Dispose();

            if (agentCount != lastAgentCount && enableWarnings)
            {
                if (currentFps < lowFpsThreshold && agentCount > 500)
                {
                    Debug.LogWarning($"[PerformanceMonitor] Low FPS ({currentFps:F0}) with {agentCount} agents.\n" +
                        $"Performance Tips:\n" +
                        $"1. Remove Rigidbody components from agents\n" +
                        $"2. Use Tools > Flow Field > Agent Physics Converter\n" +
                        $"3. Add PhysicsOptimizer component to scene\n" +
                        $"4. Disable shadows if not needed\n" +
                        $"5. See PERFORMANCE_OPTIMIZATION.md for details");
                }
            }

            lastAgentCount = agentCount;
        }

        void OnGUI()
        {
            if (!showOverlay)
                return;

            int boxWidth = 350;
            int boxHeight = 140;
            int padding = 10;

            GUI.Box(new Rect(Screen.width - boxWidth - padding, padding, boxWidth, boxHeight), "");

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;
            style.normal.textColor = Color.white;

            int yOffset = padding + 10;
            int lineHeight = 20;

            // FPS with color coding
            GUIStyle fpsStyle = new GUIStyle(style);
            if (currentFps >= 60)
                fpsStyle.normal.textColor = Color.green;
            else if (currentFps >= 30)
                fpsStyle.normal.textColor = Color.yellow;
            else
                fpsStyle.normal.textColor = Color.red;

            GUI.Label(new Rect(Screen.width - boxWidth - padding + 10, yOffset, boxWidth - 20, lineHeight),
                $"FPS: {currentFps:F0}", fpsStyle);
            yOffset += lineHeight;

            // Agent count
            GUI.Label(new Rect(Screen.width - boxWidth - padding + 10, yOffset, boxWidth - 20, lineHeight),
                $"Agents: {lastAgentCount}", style);
            yOffset += lineHeight;

            // Rigidbody count with warning
            GUIStyle rbStyle = new GUIStyle(style);
            if (lastRigidbodyCount > 100)
                rbStyle.normal.textColor = Color.yellow;
            if (lastRigidbodyCount > 500)
                rbStyle.normal.textColor = Color.red;

            GUI.Label(new Rect(Screen.width - boxWidth - padding + 10, yOffset, boxWidth - 20, lineHeight),
                $"Rigidbodies: {lastRigidbodyCount}", rbStyle);
            yOffset += lineHeight;

            // Performance status
            GUIStyle statusStyle = new GUIStyle(style);
            statusStyle.fontSize = 12;
            string status = "";

            if (lastRigidbodyCount > 500)
            {
                statusStyle.normal.textColor = Color.red;
                status = "⚠️ Too many Rigidbodies! See console.";
            }
            else if (currentFps < lowFpsThreshold && lastAgentCount > 500)
            {
                statusStyle.normal.textColor = Color.yellow;
                status = "⚠️ Low FPS - Check optimization guide";
            }
            else if (currentFps >= 60)
            {
                statusStyle.normal.textColor = Color.green;
                status = "✓ Performance OK";
            }
            else
            {
                statusStyle.normal.textColor = Color.yellow;
                status = "Performance could be better";
            }

            GUI.Label(new Rect(Screen.width - boxWidth - padding + 10, yOffset, boxWidth - 20, lineHeight * 2),
                status, statusStyle);
        }

        [ContextMenu("Print Performance Report")]
        void PrintReport()
        {
            var rigidbodies = FindObjectsOfType<Rigidbody>();
            int dynamicCount = 0;
            int kinematicCount = 0;

            foreach (var rb in rigidbodies)
            {
                if (rb.isKinematic)
                    kinematicCount++;
                else
                    dynamicCount++;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            int agentCount = 0;
            if (world != null)
            {
                var entityManager = world.EntityManager;
                var query = entityManager.CreateEntityQuery(typeof(Agent));
                agentCount = query.CalculateEntityCount();
                query.Dispose();
            }

            Debug.Log($"═══════════════════════════════════════════════\n" +
                     $"  PERFORMANCE REPORT\n" +
                     $"═══════════════════════════════════════════════\n" +
                     $"FPS: {currentFps:F1}\n" +
                     $"Agents: {agentCount}\n" +
                     $"Dynamic Rigidbodies: {dynamicCount}\n" +
                     $"Kinematic Rigidbodies: {kinematicCount}\n" +
                     $"Total Rigidbodies: {rigidbodies.Length}\n" +
                     $"Fixed Timestep: {Time.fixedDeltaTime:F3}s ({1f/Time.fixedDeltaTime:F0} physics fps)\n" +
                     $"Auto Sync Transforms: {Physics.autoSyncTransforms}\n" +
                     $"\n" +
                     $"Recommendations:\n" +
                     (dynamicCount > 0 ? $"⚠️ Remove {dynamicCount} dynamic Rigidbodies\n" : "") +
                     (kinematicCount > 100 ? $"⚠️ Consider removing {kinematicCount} kinematic Rigidbodies\n" : "") +
                     (currentFps < lowFpsThreshold ? $"⚠️ FPS below target ({lowFpsThreshold})\n" : "") +
                     (Time.fixedDeltaTime < 0.03f ? $"⚠️ Consider increasing Fixed Timestep to 0.04\n" : "") +
                     (Physics.autoSyncTransforms ? $"⚠️ Consider disabling Auto Sync Transforms\n" : "") +
                     $"\n" +
                     $"See PERFORMANCE_OPTIMIZATION.md for detailed guide.\n" +
                     $"═══════════════════════════════════════════════");
        }
    }
}
