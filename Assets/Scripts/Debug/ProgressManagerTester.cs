using UnityEngine;

namespace Salinlahi.Debug
{
    /// <summary>
    /// Test component for SALIN-48: Progress Manager functionality
    /// Add this to an empty GameObject in the Bootstrap scene for testing
    /// </summary>
    public class ProgressManagerTester : MonoBehaviour
    {
        // Reference to ProgressManager instance
        private ProgressManager _progressManager;
        [Header("Test Settings")]
        [Tooltip("Run tests automatically on start")]
        [SerializeField] private bool runTestsOnStart = false;
        
        [Tooltip("Show detailed debug output")]
        [SerializeField] private bool verboseLogging = true;

        private void Start()
        {
            // Get ProgressManager instance (it's a Singleton)
            // Using Start() instead of Awake() to ensure ProgressManager initializes first
            _progressManager = ProgressManager.Instance;
            
            if (_progressManager == null)
            {
                UnityEngine.Debug.LogError("ProgressManagerTester: ProgressManager.Instance is null!");
                UnityEngine.Debug.LogError("SOLUTION: Create a GameObject named 'ProgressManager' and attach the ProgressManager script to it.");
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
                UnityEngine.Debug.LogError("ProgressManagerTester: Cannot run tests - ProgressManager is null!");
                return;
            }
            
            UnityEngine.Debug.Log("=== SALIN-48 TEST START ===");
            
            // Clear
            _progressManager.ClearAllProgress();
            Log("✓ Progress cleared");
            
            // Test Level 1 unlocked
            bool l1 = _progressManager.IsLevelUnlocked(1);
            Log($"✓ Level 1 unlocked: {l1} (expected: True)");
            
            // Test Level 2 locked
            bool l2 = _progressManager.IsLevelUnlocked(2);
            Log($"✓ Level 2 locked: {!l2} (expected: True)");
            
            // Complete Level 1 with 3 stars
            _progressManager.MarkLevelComplete(1, 3);
            Log("✓ Marked Level 1 complete with 3 stars");
            
            // Test Level 2 now unlocked
            l2 = _progressManager.IsLevelUnlocked(2);
            Log($"✓ Level 2 now unlocked: {l2} (expected: True)");
            
            // Test stars saved
            int stars = _progressManager.GetStars(1);
            Log($"✓ Level 1 stars: {stars} (expected: 3)");
            
            // Test star improvement
            _progressManager.MarkLevelComplete(1, 2); // Worse score
            stars = _progressManager.GetStars(1);
            Log($"✓ Stars preserved (not downgraded): {stars} (expected: 3)");
            
            // Test total stars
            int total = _progressManager.GetTotalStars();
            Log($"✓ Total stars: {total} (expected: 3)");
            
            UnityEngine.Debug.Log("=== SALIN-48 TEST COMPLETE ===");
        }
        
        [ContextMenu("Test All Levels")]
        void TestAllLevels()
        {
            if (_progressManager == null)
            {
                UnityEngine.Debug.LogError("ProgressManagerTester: Cannot run tests - ProgressManager is null!");
                return;
            }
            
            UnityEngine.Debug.Log("=== TESTING ALL LEVELS ===");
            
            _progressManager.ClearAllProgress();
            
            // Complete all levels
            for (int i = 1; i <= 5; i++)
            {
                _progressManager.MarkLevelComplete(i, 3);
                Log($"Level {i} completed");
            }
            
            // Check Endless Mode
            bool endless = _progressManager.IsEndlessModeUnlocked();
            Log($"Endless Mode unlocked: {endless} (expected: True)");
            
            int total = _progressManager.GetTotalStars();
            Log($"Total stars: {total} (expected: 15)");
            
            UnityEngine.Debug.Log("=== ALL LEVELS TEST COMPLETE ===");
        }
        
        [ContextMenu("Clear Progress")]
        void ClearProgress() 
        {
            if (_progressManager == null)
            {
                UnityEngine.Debug.LogError("ProgressManagerTester: Cannot clear - ProgressManager is null!");
                return;
            }
            
            _progressManager.ClearAllProgress();
            Log("Progress cleared");
        }
        
        [ContextMenu("Unlock All")]
        void UnlockAll() 
        {
            if (_progressManager == null)
            {
                UnityEngine.Debug.LogError("ProgressManagerTester: Cannot unlock - ProgressManager is null!");
                return;
            }
            
            _progressManager.UnlockAllLevels();
            Log("All levels unlocked");
        }
        
        [ContextMenu("Show Progress")]
        void ShowProgress()
        {
            if (_progressManager == null)
            {
                UnityEngine.Debug.LogError("ProgressManagerTester: Cannot show - ProgressManager is null!");
                return;
            }
            
            UnityEngine.Debug.Log("=== CURRENT PROGRESS ===");
            for (int i = 1; i <= 5; i++)
            {
                bool unlocked = _progressManager.IsLevelUnlocked(i);
                int stars = _progressManager.GetStars(i);
                string status = unlocked ? $"UNLOCKED - {stars} stars" : "LOCKED";
                UnityEngine.Debug.Log($"Level {i}: {status}");
            }
            UnityEngine.Debug.Log($"Total Stars: {_progressManager.GetTotalStars()}");
            UnityEngine.Debug.Log($"Endless Mode: {(_progressManager.IsEndlessModeUnlocked() ? "UNLOCKED" : "LOCKED")}");
            UnityEngine.Debug.Log("=======================");
        }
        
        private void Log(string message)
        {
            if (verboseLogging)
            {
                UnityEngine.Debug.Log(message);
            }
        }
    }
}
