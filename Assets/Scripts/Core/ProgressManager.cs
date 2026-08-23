#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that persists per-level completion state and star ratings (0-3) to PlayerPrefs.
/// Unlocks the next level when a level is completed.
/// Survives app restarts on Android/iOS.
/// </summary>
public class ProgressManager : Singleton<ProgressManager>
{
    public const string SelectedLevelKey = "SelectedLevel";
    public const string EndlessModeKey = "salinlahi.progress.endless_unlocked";
    public const int Level1FtueTutorialLevelNumber = 1;
    public const string Level1FtueSeenKey = "salinlahi.tutorial.level1_ftue_seen";
    public const string Level1FtueBeatIndexKey = "salinlahi.tutorial.level1_ftue_beat_index";
    public const int Level2AdvancedTutorialLevelNumber = 2;
    public const string Level2AdvancedSeenKey = "salinlahi.tutorial.level2_advanced_focus_chain_v3_seen";
    public const string Level2AdvancedBeatIndexKey = "salinlahi.tutorial.level2_advanced_focus_chain_v3_beat_index";
    private const string LegacyLevel2AdvancedSeenKey = "salinlahi.tutorial.level2_advanced_seen";
    private const string LegacyLevel2AdvancedBeatIndexKey = "salinlahi.tutorial.level2_advanced_beat_index";
    private const string LegacyLevel2AdvancedFocusV2SeenKey = "salinlahi.tutorial.level2_advanced_focus_v2_seen";
    private const string LegacyLevel2AdvancedFocusV2BeatIndexKey = "salinlahi.tutorial.level2_advanced_focus_v2_beat_index";

    private const string KeyPrefix = "salinlahi.progress.";
    private const int MaxStars = 3;
    private const int TotalLevels = 15;
    // Track which level we've processed to handle restarts properly
    private int _lastProcessedLevelId = -1;

    // Cached HeartSystem reference — set via RegisterHeartSystem
    private HeartSystem _cachedHeartSystem;

    // Track current level being played for validation
    private int _currentPlayingLevelId = -1;
    private CampaignProgressOutcome _cachedLevelOutcome;
    private LearningEvidenceRecorder _levelEvidence;
    private LevelResults _pendingLevelResults;

    /// <summary>
    /// SALIN-202: the level flow computes LevelResults before committing; the
    /// star calculation consults them so revised outcomes reflect learning
    /// accuracy, not hearts alone. Cleared on scene change with the outcome cache.
    /// </summary>
    public void SetPendingLevelResults(LevelResults results)
    {
        _pendingLevelResults = results;
    }

    protected override void Awake()
    {
        base.Awake();
        DebugLogger.Log("ProgressManager: Initialized");
    }

    private bool UsesRevisedProgress => SaveManager.Instance != null &&
        SaveManager.Instance.Mode == SaveManagerMode.RevisedReady &&
        SaveManager.Instance.Repository != null && SaveManager.Instance.OutcomeCoordinator != null;

    private bool IsRevisedBlocked => SaveManager.Instance != null &&
        SaveManager.Instance.Mode == SaveManagerMode.RevisedBlocked;

    public int GetSelectedLevelNumber()
    {
        if (UsesRevisedProgress)
        {
            string selectedId = SaveManager.Instance.Repository.ActiveLevelId;
            if (SaveManager.Instance.Campaign.TryGetLevel(selectedId, out LevelConfigSO selected))
                return selected.levelNumber;
            return 1;
        }
        return PlayerPrefs.GetInt(SelectedLevelKey, 1);
    }

    public string GetSelectedLevelId()
    {
        if (UsesRevisedProgress)
            return SaveManager.Instance.Repository.ActiveLevelId;
        return ResolveLegacyLevelId(PlayerPrefs.GetInt(SelectedLevelKey, 1));
    }

    public bool TrySetSelectedLevel(LevelConfigSO level)
    {
        if (level == null) return false;
        if (UsesRevisedProgress)
            return SaveManager.Instance.Repository.TrySetActiveLevel(level.stableId);
        if (IsRevisedBlocked) return false;
        PlayerPrefs.SetInt(SelectedLevelKey, level.levelNumber);
        PlayerPrefs.Save();
        return true;
    }

    public bool TrySetSelectedLevelNumber(int levelNumber)
    {
        if (UsesRevisedProgress)
        {
            if (!TryResolveRevisedLevel(levelNumber, out LevelConfigSO level)) return false;
            return TrySetSelectedLevel(level);
        }
        if (IsRevisedBlocked || levelNumber < 1 || levelNumber > TotalLevels) return false;
        PlayerPrefs.SetInt(SelectedLevelKey, levelNumber);
        PlayerPrefs.Save();
        return true;
    }

    public bool TryGetSelectedLevel(out LevelConfigSO level)
    {
        level = null;
        if (UsesRevisedProgress)
            return SaveManager.Instance.Campaign.TryGetLevel(GetSelectedLevelId(), out level);
        return false;
    }

    private void OnEnable()
    {
        EventBus.OnLevelComplete += HandleLevelComplete;
        EventBus.OnWaveStarted += HandleWaveStarted;
        EventBus.OnPronunciationRequested += HandlePronunciationRequested;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        EventBus.OnLevelComplete -= HandleLevelComplete;
        EventBus.OnWaveStarted -= HandleWaveStarted;
        EventBus.OnPronunciationRequested -= HandlePronunciationRequested;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// SALIN-202: Sound-dimension evidence flows through this defined event — an
    /// audible pronunciation records one exposure on the symbol. Exposure is not
    /// recall, so the answer counts as visible.
    /// </summary>
    private void HandlePronunciationRequested(BaybayinCharacterSO character)
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        if (SandboxMode.IsActive)
            return;
#endif
        if (character == null || character.pronunciationClip == null ||
            string.IsNullOrEmpty(character.stableId))
            return;

        LevelEvidence.RecordAttempt(
            character.stableId,
            LearningContentKind.Symbol,
            MasteryDimension.Sound,
            success: true,
            answerWasVisible: true);
    }

    /// <summary>
    /// Called by HeartSystem.OnEnable() to register itself.
    /// Replaces FindFirstObjectByType scene search.
    /// </summary>
    public void RegisterHeartSystem(HeartSystem heartSystem)
    {
        _cachedHeartSystem = heartSystem;
        DebugLogger.Log("ProgressManager: HeartSystem registered.");
    }

    /// <summary>
    /// Called by HeartSystem.OnDisable() to deregister.
    /// </summary>
    public void DeregisterHeartSystem(HeartSystem heartSystem)
    {
        if (_cachedHeartSystem == heartSystem)
        {
            _cachedHeartSystem = null;
            DebugLogger.Log("ProgressManager: HeartSystem deregistered.");
        }
    }

    /// <summary>
    /// Called when a new scene is loaded.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Clear cached HeartSystem when entering a new scene;
        // HeartSystem will re-register via its OnEnable.
        _cachedHeartSystem = null;
        _cachedLevelOutcome = null;
        _levelEvidence = null;
        _pendingLevelResults = null;

        if (scene.name.Contains("Gameplay") || scene.name.Contains("Game"))
        {
            // Read the selected level when entering gameplay
            _currentPlayingLevelId = GetSelectedLevelNumber();
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
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        if (SandboxMode.IsActive)
            return;
#endif

        // Wave 0 indicates start of a new level attempt
        if (waveIndex == 0)
        {
            // HeartSystem should have already registered via OnEnable
            if (_cachedHeartSystem == null)
            {
                DebugLogger.LogWarning("ProgressManager: HeartSystem not registered at wave 0 start.");
            }

            // Update current level ID from PlayerPrefs (in case it changed)
            int levelId = GetSelectedLevelNumber();
            if (levelId != _currentPlayingLevelId)
            {
                _currentPlayingLevelId = levelId;
                _cachedLevelOutcome = null;
                DebugLogger.Log($"ProgressManager: Level changed to {_currentPlayingLevelId}");
            }

            DebugLogger.Log($"ProgressManager: Wave 0 started for Level {_currentPlayingLevelId}");
        }
    }

    private void HandleLevelComplete()
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        if (SandboxMode.IsActive)
        {
            DebugLogger.Log("ProgressManager: Ignored LevelComplete while sandbox mode is active.");
            return;
        }
#endif

        if (UsesRevisedProgress)
            return;

        // Get current level ID from tracking or PlayerPrefs
        int currentLevelId = _currentPlayingLevelId > 0 ? _currentPlayingLevelId : GetSelectedLevelNumber();

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
        // SALIN-202: on revised saves the documented accuracy-aware formula wins
        // when the flow computed results for this completion; the legacy
        // PlayerPrefs path stays hearts-only.
        if (UsesRevisedProgress && _pendingLevelResults != null)
            return Mathf.Clamp(_pendingLevelResults.Stars, 1, MaxStars);

        // HeartSystem should have already registered via OnEnable
        HeartSystem heartSystem = _cachedHeartSystem;

        if (heartSystem == null)
        {
            DebugLogger.LogWarning("ProgressManager: HeartSystem not registered, defaulting to 1 star.");
            return 1;
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
        if (UsesRevisedProgress)
        {
            if (TryResolveRevisedLevel(levelID, out LevelConfigSO revisedLevel))
            {
                _cachedLevelOutcome = BuildOutcome(
                    revisedLevel, Mathf.Clamp(stars, 1, MaxStars), null, null, null);
                SaveManager.Instance.OutcomeCoordinator.TryCommit(_cachedLevelOutcome);
            }
            return;
        }
        if (IsRevisedBlocked)
            return;

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

        if (UsesRevisedProgress)
            return SaveManager.Instance.Repository.IsLevelUnlocked(GetRevisedLevelId(levelID));
        if (IsRevisedBlocked) return false;

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

        if (UsesRevisedProgress)
            return SaveManager.Instance.Repository.GetBestStars(GetRevisedLevelId(levelID));
        if (IsRevisedBlocked) return 0;
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
        if (UsesRevisedProgress)
            return SaveManager.Instance.Repository.IsEndlessModeUnlocked;
        if (IsRevisedBlocked) return false;
        return PlayerPrefs.GetInt(EndlessModeKey, 0) == 1;
    }

    /// <summary>
    /// Unlocks endless mode.
    /// </summary>
    public void UnlockEndlessMode()
    {
        if (UsesRevisedProgress)
        {
            SaveManager.Instance.Repository.TryUnlockEndlessMode();
            return;
        }
        if (IsRevisedBlocked) return;
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
        if (UsesRevisedProgress)
        {
            CampaignOutcomeCommitResult resetResult = SaveManager.Instance.ResetJourneyAtomically();
            if (resetResult.IsAccepted)
            {
                CharacterUnlockProgress.ClearAllUnlocked();
                EnemyDiscoveryProgress.ClearAllDiscovered();
                BossDiscoveryProgress.ClearAllDiscovered();
                PlayerPrefs.Save();
            }
            _lastProcessedLevelId = -1;
            _currentPlayingLevelId = -1;
            _cachedLevelOutcome = null;
            return;
        }
        if (IsRevisedBlocked) return;
        for (int i = 1; i <= TotalLevels; i++)
        {
            PlayerPrefs.DeleteKey(UnlockedKey(i));
            PlayerPrefs.DeleteKey(StarsKey(i));
        }
        PlayerPrefs.DeleteKey(EndlessModeKey);
        PlayerPrefs.DeleteKey(Level1FtueSeenKey);
        PlayerPrefs.DeleteKey(Level1FtueBeatIndexKey);
        PlayerPrefs.DeleteKey(Level2AdvancedSeenKey);
        PlayerPrefs.DeleteKey(Level2AdvancedBeatIndexKey);
        PlayerPrefs.DeleteKey(LegacyLevel2AdvancedSeenKey);
        PlayerPrefs.DeleteKey(LegacyLevel2AdvancedBeatIndexKey);
        PlayerPrefs.DeleteKey(LegacyLevel2AdvancedFocusV2SeenKey);
        PlayerPrefs.DeleteKey(LegacyLevel2AdvancedFocusV2BeatIndexKey);
        CharacterUnlockProgress.ClearAllUnlocked();
        EnemyDiscoveryProgress.ClearAllDiscovered();
        BossDiscoveryProgress.ClearAllDiscovered();

        // Reset tracking
        _lastProcessedLevelId = -1;
        _currentPlayingLevelId = -1;

        PlayerPrefs.Save();
        DebugLogger.Log("ProgressManager: All progress cleared.");
    }

    public CampaignOutcomeCommitResult CommitCurrentLevelOutcome(
        IReadOnlyList<string> unlockedSymbolIds = null,
        IReadOnlyList<string> unlockedMemoryIds = null,
        IReadOnlyList<string> claimedRewardIds = null)
    {
        if (UsesRevisedProgress)
        {
            if (_cachedLevelOutcome == null)
            {
                if (!TryGetSelectedLevel(out LevelConfigSO level))
                    return CampaignOutcomeCommitResult.Rejected(
                        null, CampaignSaveFailureCode.InvalidStructure, "active-level-missing");
                _cachedLevelOutcome = BuildOutcome(
                    level, CalculateStars(), unlockedSymbolIds, unlockedMemoryIds, claimedRewardIds);
            }
            return SaveManager.Instance.OutcomeCoordinator.TryCommit(_cachedLevelOutcome);
        }

        if (IsRevisedBlocked)
            return CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.InvalidStructure, "revised-save-blocked");

        int currentLevelId = _currentPlayingLevelId > 0 ? _currentPlayingLevelId : GetSelectedLevelNumber();
        if (currentLevelId < 1 || currentLevelId > TotalLevels)
            currentLevelId = 1;
        MarkLevelComplete(currentLevelId, CalculateStars());
        return CampaignOutcomeCommitResult.Committed(null);
    }

    public CampaignOutcomeCommitResult RetryPendingLevelOutcome()
    {
        if (SaveManager.Instance == null)
            return CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.InvalidStructure, "save-manager-missing");
        return SaveManager.Instance.RetryPendingOutcome();
    }

    private CampaignProgressOutcome BuildOutcome(
        LevelConfigSO level,
        int stars,
        IReadOnlyList<string> unlockedSymbolIds,
        IReadOnlyList<string> unlockedMemoryIds,
        IReadOnlyList<string> claimedRewardIds)
    {
        return new CampaignProgressOutcome
        {
            outcomeSchemaVersion = CampaignProgressOutcome.CurrentOutcomeSchemaVersion,
            outcomeId = "outcome." + Guid.NewGuid().ToString("N"),
            journeyGenerationId = SaveManager.Instance.Repository.CurrentJourneyGenerationId,
            campaignId = SaveManager.Instance.Campaign.manifest.campaignId,
            contentSchemaVersion = SaveManager.Instance.Campaign.manifest.contentSchemaVersion,
            levelId = level.stableId,
            stars = Mathf.Clamp(stars, 1, MaxStars),
            unlockedSymbolIds = CopyAndSort(unlockedSymbolIds),
            unlockedMemoryIds = CopyAndSort(unlockedMemoryIds),
            claimedRewardIds = CopyAndSort(claimedRewardIds),
            completedAtUtc = DateTime.UtcNow.ToString("O"),
            sessionKind = LearningSessionKind.LevelAttempt,
            evidence = _levelEvidence?.Build() ?? new LearningEvidenceBatch
            {
                levelId = level.stableId,
                sessionKind = LearningSessionKind.LevelAttempt,
            },
        };
    }

    /// <summary>
    /// Session-scoped evidence recorder for the level currently being played. Created on demand so
    /// callers never have to null-check, and discarded in OnSceneLoaded when the level is left.
    /// </summary>
    public LearningEvidenceRecorder LevelEvidence
    {
        get
        {
            if (_levelEvidence == null)
                _levelEvidence = new LearningEvidenceRecorder(
                    GetSelectedLevelId(), LearningSessionKind.LevelAttempt);
            return _levelEvidence;
        }
    }

    /// <summary>
    /// Commits a free-practice or scheduled-review batch. The result is returned rather than
    /// surfaced: per spec 11 a practice commit failure must not raise the blocking save panel.
    /// </summary>
    public CampaignOutcomeCommitResult CommitPracticeSession(LearningEvidenceBatch batch)
    {
        if (batch == null)
            return CampaignOutcomeCommitResult.Rejected(
                null, CampaignSaveFailureCode.InvalidStructure, "evidence-batch-missing");
        if (!UsesRevisedProgress)
            return CampaignOutcomeCommitResult.Rejected(
                null, CampaignSaveFailureCode.InvalidStructure, "revised-progress-unavailable");
        if (!TryGetSelectedLevel(out LevelConfigSO level))
            return CampaignOutcomeCommitResult.Rejected(
                null, CampaignSaveFailureCode.InvalidStructure, "active-level-missing");

        batch.levelId = level.stableId;
        CampaignProgressOutcome outcome = new CampaignProgressOutcome
        {
            outcomeSchemaVersion = CampaignProgressOutcome.CurrentOutcomeSchemaVersion,
            outcomeId = "outcome." + Guid.NewGuid().ToString("N"),
            journeyGenerationId = SaveManager.Instance.Repository.CurrentJourneyGenerationId,
            campaignId = SaveManager.Instance.Campaign.manifest.campaignId,
            contentSchemaVersion = SaveManager.Instance.Campaign.manifest.contentSchemaVersion,
            levelId = level.stableId,
            stars = 0,
            unlockedSymbolIds = new List<string>(),
            unlockedMemoryIds = new List<string>(),
            claimedRewardIds = new List<string>(),
            completedAtUtc = DateTime.UtcNow.ToString("O"),
            sessionKind = batch.sessionKind == LearningSessionKind.LevelAttempt
                ? LearningSessionKind.FreePractice
                : batch.sessionKind,
            evidence = batch,
        };

        return SaveManager.Instance.OutcomeCoordinator.TryCommit(outcome);
    }

    private static List<string> CopyAndSort(IReadOnlyList<string> values)
    {
        List<string> copy = new List<string>();
        if (values != null)
            for (int i = 0; i < values.Count; i++)
                copy.Add(values[i]);
        copy.Sort(StringComparer.Ordinal);
        return copy;
    }

    /// <summary>
    /// Unlocks all levels (dev/debug utility).
    /// </summary>
    public void UnlockAllLevels()
    {
        if (UsesRevisedProgress || IsRevisedBlocked)
            return;
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

    private string GetRevisedLevelId(int levelNumber)
    {
        return TryResolveRevisedLevel(levelNumber, out LevelConfigSO level) ? level.stableId : string.Empty;
    }

    private bool TryResolveRevisedLevel(int levelNumber, out LevelConfigSO level)
    {
        level = null;
        if (SaveManager.Instance?.Campaign == null)
            return false;
        IReadOnlyList<string> levelIds = ContentIdentity.RevisedLevelIds;
        if (levelNumber < 1 || levelNumber > levelIds.Count)
            return false;
        return SaveManager.Instance.Campaign.TryGetLevel(levelIds[levelNumber - 1], out level);
    }

    private string ResolveLegacyLevelId(int levelNumber)
    {
        if (levelNumber < 1 || levelNumber > ContentIdentity.RevisedLevelIds.Count)
            return ContentIdentity.RevisedLevelIds[0];
        return ContentIdentity.RevisedLevelIds[levelNumber - 1];
    }

    #endregion
}
