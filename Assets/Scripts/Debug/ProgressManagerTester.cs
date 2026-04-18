using UnityEngine;

namespace Salinlahi.Debug
{
    /// <summary>
    /// Test component for SALIN-48: Progress Manager functionality
    /// Add this to an empty GameObject in the Bootstrap scene for testing
    /// </summary>
    public class ProgressManagerTester : MonoBehaviour
    {
        private ProgressManager _progressManager;

        [Header("Test Settings")]
        [Tooltip("Run tests automatically on start")]
        [SerializeField] private bool runTestsOnStart = false;

        [Tooltip("Show detailed debug output")]
        [SerializeField] private bool verboseLogging = true;

        private void Start()
        {
            _progressManager = ProgressManager.Instance;

            if (_progressManager == null)
            {
                DebugLogger.LogError("ProgressManagerTester: ProgressManager.Instance is null! "
                    + "Create a GameObject named 'ProgressManager' and attach the ProgressManager script to it.");
                return;
            }

            if (runTestsOnStart)
            {
                TestFullFlow();
            }
        }

        [ContextMenu("Test Full Flow")]
        void TestFullFlow()
        {
            if (_progressManager == null)
            {
                DebugLogger.LogError("ProgressManagerTester: Cannot run tests - ProgressManager is null!");
                return;
            }

            DebugLogger.Log("=== SALIN-48 TEST START ===");

            _progressManager.ClearAllProgress();
            if (verboseLogging) DebugLogger.Log("✓ Progress cleared");

            bool l1 = _progressManager.IsLevelUnlocked(1);
            if (verboseLogging) DebugLogger.Log($"✓ Level 1 unlocked: {l1} (expected: True)");

            bool l2 = _progressManager.IsLevelUnlocked(2);
            if (verboseLogging) DebugLogger.Log($"✓ Level 2 locked: {!l2} (expected: True)");

            _progressManager.MarkLevelComplete(1, 3);
            if (verboseLogging) DebugLogger.Log("✓ Marked Level 1 complete with 3 stars");

            l2 = _progressManager.IsLevelUnlocked(2);
            if (verboseLogging) DebugLogger.Log($"✓ Level 2 now unlocked: {l2} (expected: True)");

            int stars = _progressManager.GetStars(1);
            if (verboseLogging) DebugLogger.Log($"✓ Level 1 stars: {stars} (expected: 3)");

            _progressManager.MarkLevelComplete(1, 2);
            stars = _progressManager.GetStars(1);
            if (verboseLogging) DebugLogger.Log($"✓ Stars preserved (not downgraded): {stars} (expected: 3)");

            int total = _progressManager.GetTotalStars();
            if (verboseLogging) DebugLogger.Log($"✓ Total stars: {total} (expected: 3)");

            DebugLogger.Log("=== SALIN-48 TEST COMPLETE ===");
        }

        [ContextMenu("Test All Levels")]
        void TestAllLevels()
        {
            if (_progressManager == null)
            {
                DebugLogger.LogError("ProgressManagerTester: Cannot run tests - ProgressManager is null!");
                return;
            }

            DebugLogger.Log("=== TESTING ALL LEVELS ===");

            _progressManager.ClearAllProgress();

            for (int i = 1; i <= 5; i++)
            {
                _progressManager.MarkLevelComplete(i, 3);
                if (verboseLogging) DebugLogger.Log($"Level {i} completed");
            }

            bool endless = _progressManager.IsEndlessModeUnlocked();
            if (verboseLogging) DebugLogger.Log($"Endless Mode unlocked: {endless} (expected: True)");

            int total = _progressManager.GetTotalStars();
            if (verboseLogging) DebugLogger.Log($"Total stars: {total} (expected: 15)");

            DebugLogger.Log("=== ALL LEVELS TEST COMPLETE ===");
        }

        [ContextMenu("Clear Progress")]
        void ClearProgress()
        {
            if (_progressManager == null)
            {
                DebugLogger.LogError("ProgressManagerTester: Cannot clear - ProgressManager is null!");
                return;
            }

            _progressManager.ClearAllProgress();
            if (verboseLogging) DebugLogger.Log("Progress cleared");
        }

        [ContextMenu("Unlock All")]
        void UnlockAll()
        {
            if (_progressManager == null)
            {
                DebugLogger.LogError("ProgressManagerTester: Cannot unlock - ProgressManager is null!");
                return;
            }

            _progressManager.UnlockAllLevels();
            if (verboseLogging) DebugLogger.Log("All levels unlocked");
        }

        [ContextMenu("Show Progress")]
        void ShowProgress()
        {
            if (_progressManager == null)
            {
                DebugLogger.LogError("ProgressManagerTester: Cannot show - ProgressManager is null!");
                return;
            }

            DebugLogger.Log("=== CURRENT PROGRESS ===");
            for (int i = 1; i <= 5; i++)
            {
                bool unlocked = _progressManager.IsLevelUnlocked(i);
                int stars = _progressManager.GetStars(i);
                string status = unlocked ? $"UNLOCKED - {stars} stars" : "LOCKED";
                DebugLogger.Log($"Level {i}: {status}");
            }
            DebugLogger.Log($"Total Stars: {_progressManager.GetTotalStars()}");
            DebugLogger.Log($"Endless Mode: {(_progressManager.IsEndlessModeUnlocked() ? "UNLOCKED" : "LOCKED")}");
            DebugLogger.Log("=======================");
        }
    }
}