using Salinlahi.Debug.Sandbox;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that persists per-level completion state and star ratings (0-3) to PlayerPrefs.
/// Unlocks the next level when a level is completed.
/// Survives app restarts on Android/iOS.
/// </summary>
public class ProgressManager : Singleton<ProgressManager>
{
    private const string KeyPrefix = "salinlahi.progress.";
    private const int MaxStars = 3;
    private const int TotalLevels = 5;
    private const string EndlessModeKey = "salinlahi.progress.endless_unlocked";

    // Track which level we've processed to handle restarts properly
    private int _lastProcessedLevelId = -1;

    // Cached HeartSystem reference for performance
    private HeartSystem _cachedHeartSystem;

    // Track current level being played for validation
    private int _currentPlayingLevelId = -1;

    protected override void Awake()
    {
        base.Awake();
        DebugLogger.Log("ProgressManager: Initialized");
    }

    private void OnEnable()
    {
        EventBus.OnLevelComplete += HandleLevelComplete;
        EventBus.OnWaveStarted += HandleWaveStarted;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        EventBus.OnLevelComplete -= HandleLevelComplete;
        EventBus.OnWaveStarted -= HandleWaveStarted;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Called when a new scene is loaded. Cache HeartSystem reference if in gameplay.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Clear cached HeartSystem when entering a new scene
        _cachedHeartSystem = null;

        // Try to find HeartSystem if we're in the gameplay scene
        if (scene.name.Contains("Gameplay") || scene.name.Contains("Game"))
        {
            _cachedHeartSystem = FindFirstObjectByType<HeartSystem>();
            if (_cachedHeartSystem != null)
            {
                DebugLogger.Log("ProgressManager: Cached HeartSystem reference.");
            }

            // Read the selected level when entering gameplay
            _currentPlayingLevelId = PlayerPrefs.GetInt("SelectedLevel", 1);
            DebugLogger.Log($"ProgressManager: Starting Level {_currentPlayingLevelId}");
        }
        else
        {
            // Reset current level when leaving gameplay
            _currentPlayingLevelId = -1;
        }
    }

    private void HandleWaveStarted(int waveIndex)
    {
        if (SandboxMode.IsActive)
            return;

        // Wave 0 indicates start of a new level attempt
        if (waveIndex == 0)
        {
            // Refresh HeartSystem cache at level start
            if (_cachedHeartSystem == null)
            {
                _cachedHeartSystem = FindFirstObjectByType<HeartSystem>();
            }

            // Update current level ID from PlayerPrefs (in case it changed)
            int levelId = PlayerPrefs.GetInt("SelectedLevel", 1);
            if (levelId != _currentPlayingLevelId)
            {
                _currentPlayingLevelId = levelId;
                DebugLogger.Log($"ProgressManager: Level changed to {_currentPlayingLevelId}");
            }

            DebugLogger.Log($"ProgressManager: Wave 0 started for Level {_currentPlayingLevelId}");
        }
    }

    private void HandleLevelComplete()
    {
        if (SandboxMode.IsActive)
        {
            DebugLogger.Log("ProgressManager: Ignored LevelComplete while sandbox mode is active.");
            return;
        }

        // Get current level ID from tracking or PlayerPrefs
        int currentLevelId = _currentPlayingLevelId > 0 ? _currentPlayingLevelId : PlayerPrefs.GetInt("SelectedLevel", 1);

        // Validate level ID
        if (currentLevelId < 1 || currentLevelId > TotalLevels)
        {
            DebugLogger.LogWarning($"ProgressManager: Invalid SelectedLevel {currentLevelId}. Defaulting to 1.");
            currentLevelId = 1;
        }

        // Idempotency guard: skip if already processed this level instance
        // This handles the case where LevelComplete might be raised multiple times
        if (_lastProcessedLevelId == currentLevelId)
        {
            DebugLogger.Log($"ProgressManager: Level {currentLevelId} already processed, skipping.");
            return;
        }

        // Calculate stars based on remaining hearts BEFORE any scene transition
        int stars = CalculateStars();

        // Mark level complete (this also unlocks next level and calls PlayerPrefs.Save())
        MarkLevelComplete(currentLevelId, stars);

        // Track that we've processed this level
        _lastProcessedLevelId = currentLevelId;

        DebugLogger.Log($"ProgressManager: Level {currentLevelId} completed with {stars} stars.");
    }

    /// <summary>
    /// Calculates stars based on remaining hearts.
    /// 3 stars = 100% hearts, 2 stars = >= 50%, 1 star = completed at all
    /// </summary>
    private int CalculateStars()
    {
        // Use cached HeartSystem if available, otherwise find it
        HeartSystem heartSystem = _cachedHeartSystem;
        if (heartSystem == null)
        {
            heartSystem = FindFirstObjectByType<HeartSystem>();
            _cachedHeartSystem = heartSystem;
        }

        if (heartSystem == null)
        {
            DebugLogger.LogWarning("ProgressManager: HeartSystem not found, defaulting to 1 star.");
            return 1; // Default to 1 star if we can't determine hearts
        }

        int currentHearts = heartSystem.GetCurrentHearts();
        int maxHearts = heartSystem.GetMaxHearts();

        if (maxHearts <= 0)
        {
            return 1;
        }

        float ratio = (float)currentHearts / maxHearts;

        // Star formula: 3 stars = 100%, 2 stars = >= 50%, 1 star = < 50%
        int stars = ratio >= 0.99f ? 3 : ratio >= 0.5f ? 2 : 1;

        DebugLogger.Log($"ProgressManager: Star calculation - {currentHearts}/{maxHearts} hearts = {stars} stars");
        return stars;
    }

    /// <summary>
    /// Stores stars (clamped 0-3) and unlocks levelID + 1.
    /// </summary>
    /// <param name="levelID">The level that was completed (1-based)</param>
    /// <param name="stars">Star count (0-3), will be clamped</param>
    public void MarkLevelComplete(int levelID, int stars)
    {
        // Validate level ID
        if (levelID < 1 || levelID > TotalLevels)
        {
            DebugLogger.LogError($"ProgressManager: Invalid levelID {levelID}. Must be between 1 and {TotalLevels}.");
            return;
        }

        // Clamp stars to valid range
        stars = Mathf.Clamp(stars, 0, MaxStars);

        // Only update if new star count is higher (idempotent per level)
        int existingStars = GetStars(levelID);
        if (stars > existingStars)
        {
            PlayerPrefs.SetInt(StarsKey(levelID), stars);
            DebugLogger.Log($"ProgressManager: Updated Level {levelID} stars: {existingStars} -> {stars}");
        }

        // Mark this level as completed (unlock key)
        PlayerPrefs.SetInt(UnlockedKey(levelID), 1);

        // Unlock next level (if not the last one)
        int nextLevelID = levelID + 1;
        if (nextLevelID <= TotalLevels)
        {
            PlayerPrefs.SetInt(UnlockedKey(nextLevelID), 1);
            DebugLogger.Log($"ProgressManager: Unlocked Level {nextLevelID}");
        }
        else if (levelID == TotalLevels)
        {
            // All levels completed - unlock endless mode
            UnlockEndlessMode();
        }

        // Save immediately to ensure persistence before any scene transition
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Returns true if the level is unlocked.
    /// Level 1 is always unlocked by default.
    /// </summary>
    /// <param name="levelID">Level number (1-based)</param>
    public bool IsLevelUnlocked(int levelID)
    {
        // Validate level ID
        if (levelID < 1 || levelID > TotalLevels)
        {
            DebugLogger.LogWarning($"ProgressManager: Invalid levelID {levelID}.");
            return false;
        }

        // Level 1 is always unlocked by default
        if (levelID == 1)
        {
            return true;
        }

        // Check if the level has been unlocked
        return PlayerPrefs.GetInt(UnlockedKey(levelID), 0) == 1;
    }

    /// <summary>
    /// Returns true if the level has been completed (has at least 1 star).
    /// </summary>
    /// <param name="levelID">Level number (1-based)</param>
    public bool IsLevelCompleted(int levelID) => GetStars(levelID) > 0;

    /// <summary>
    /// Returns the stored star count for a level.
    /// Returns 0 if never completed.
    /// </summary>
    /// <param name="levelID">Level number (1-based)</param>
    public int GetStars(int levelID)
    {
        // Validate level ID
        if (levelID < 1 || levelID > TotalLevels)
        {
            DebugLogger.LogWarning($"ProgressManager: Invalid levelID {levelID}.");
            return 0;
        }

        return PlayerPrefs.GetInt(StarsKey(levelID), 0);
    }

    /// <summary>
    /// Returns the total stars earned across all levels.
    /// </summary>
    public int GetTotalStars()
    {
        int total = 0;
        for (int i = 1; i <= TotalLevels; i++)
        {
            total += GetStars(i);
        }
        return total;
    }

    /// <summary>
    /// Returns true if endless mode is unlocked.
    /// Endless mode unlocks when all levels are completed.
    /// </summary>
    public bool IsEndlessModeUnlocked()
    {
        return PlayerPrefs.GetInt(EndlessModeKey, 0) == 1;
    }

    /// <summary>
    /// Unlocks endless mode.
    /// </summary>
    public void UnlockEndlessMode()
    {
        if (!IsEndlessModeUnlocked())
        {
            PlayerPrefs.SetInt(EndlessModeKey, 1);
            DebugLogger.Log("ProgressManager: Endless mode unlocked!");
        }
    }

    /// <summary>
    /// Clears all progress data (only removes namespaced keys).
    /// Other PlayerPrefs (audio volume, etc.) are untouched.
    /// </summary>
    public void ClearAllProgress()
    {
        for (int i = 1; i <= TotalLevels; i++)
        {
            PlayerPrefs.DeleteKey(UnlockedKey(i));
            PlayerPrefs.DeleteKey(StarsKey(i));
        }
        PlayerPrefs.DeleteKey(EndlessModeKey);

        // Reset tracking
        _lastProcessedLevelId = -1;
        _currentPlayingLevelId = -1;

        PlayerPrefs.Save();
        DebugLogger.Log("ProgressManager: All progress cleared.");
    }

    /// <summary>
    /// Unlocks all levels (dev/debug utility).
    /// </summary>
    public void UnlockAllLevels()
    {
        for (int i = 1; i <= TotalLevels; i++)
        {
            PlayerPrefs.SetInt(UnlockedKey(i), 1);
        }

        PlayerPrefs.Save();
        DebugLogger.Log("ProgressManager: All levels unlocked.");
    }

    /// <summary>
    /// Gets the currently playing level ID (if in gameplay scene, -1 otherwise).
    /// </summary>
    public int GetCurrentPlayingLevelId() => _currentPlayingLevelId;

    #region Key Helpers

    private static string UnlockedKey(int id) => $"{KeyPrefix}unlocked.{id}";
    private static string StarsKey(int id) => $"{KeyPrefix}stars.{id}";

    #endregion
}
