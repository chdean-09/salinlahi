using System.Collections;
using System.Collections.Generic;
using Salinlahi.Runtime.Gameplay;
using UnityEngine;

/// <summary>
/// Orchestrates the onboarding sequence. Iterates the beat order defined on
/// <see cref="OnboardingSequenceSO"/>, yields to each beat's Play coroutine, persists
/// progress to PlayerPrefs (for mid-sequence resume), and gates execution to configured tutorial levels.
///
/// Replaces the legacy Level1InteractiveTutorialController.
/// </summary>
public sealed class Level1OnboardingController : MonoBehaviour
{
    private const string BaCharacterId = "BA";

    [Header("Scene References")]
    [SerializeField] private DialogueController _dialogueController;
    [SerializeField] private TutorialSpotlightOverlay _spotlight;
    [SerializeField] private TutorialIntroPlayer _introPlayer;
    [SerializeField] private DemoHeartSimulator _demoHeartSimulator;
    [SerializeField] private Level1TutorialGuideUI _guideUI;
    [SerializeField] private WaveSpawner _waveSpawner;
    [SerializeField] private PlayerBase _playerBase;
    [SerializeField] private Camera _worldCamera;
    [SerializeField] private ProtagonistManager _protagonistManager;
    [SerializeField] private GameObject[] _hideDuringOnboarding;

    [Header("Sequence Data")]
    [SerializeField] private OnboardingSequenceSO _fallbackSequence;

    [Header("BA GIF Scene Override")]
    [SerializeField] private Texture2D _baGifTexture;
    [SerializeField] private Sprite[] _baGifFrames;
    [SerializeField] private float _baGifFramesPerSecond = 8f;

    [Header("HA GIF Scene Override")]
    [SerializeField] private Texture2D _haGifTexture;
    [SerializeField] private Sprite[] _haGifFrames;
    [SerializeField] private float _haGifFramesPerSecond = 8f;

    [Header("OU/O GIF Scene Override")]
    [SerializeField] private Texture2D _ouGifTexture;
    [SerializeField] private Sprite[] _ouGifFrames;
    [SerializeField] private float _ouGifFramesPerSecond = 8f;

    private readonly List<OnboardingBeat> _beats = new();
    private bool _firstManualSuccess;
    private bool _skipRequested;
    private bool _attemptAborted;
    private bool[] _hiddenOriginalState;
    private bool _onboardingHudHidden;
    private Level1TutorialSequenceSO _runtimeLegacySource;
    private OnboardingSequenceSO _runtimeLegacySequence;
    private OnboardingSequenceSO _runtimeSceneOverrideSource;
    private OnboardingSequenceSO _runtimeSceneOverrideSequence;
    private OnboardingSequenceSO _runtimeNormalizedSource;
    private OnboardingSequenceSO _runtimeNormalizedSequence;

    public bool FirstManualSuccessRecorded => _firstManualSuccess;
    public bool SkipRequested => _skipRequested;
    public bool CanRequestSkip => _firstManualSuccess;

    private void Awake()
    {
        EnsureDefaultBeatComponents();
        EnsureRuntimeHelpers();
        CollectBeats();
        if (_guideUI == null)
            _guideUI = Level1TutorialGuideUI.CreateRuntime();
        if (_guideUI != null)
            _guideUI.Initialize(RequestSkip);
    }

    private void OnEnable()
    {
        EventBus.OnLevelAttemptAborted += HandleLevelAttemptAborted;
    }

    private void OnDisable()
    {
        EventBus.OnLevelAttemptAborted -= HandleLevelAttemptAborted;
        RestoreOnboardingState();
    }

    private void OnDestroy()
    {
        DestroyRuntimeSequence(_runtimeLegacySequence);
        DestroyRuntimeSequence(_runtimeSceneOverrideSequence);
        DestroyRuntimeSequence(_runtimeNormalizedSequence);
        _runtimeLegacySequence = null;
        _runtimeSceneOverrideSequence = null;
        _runtimeNormalizedSequence = null;
    }

    public void RequestSkip()
    {
        if (!CanRequestSkip)
        {
            DebugLogger.LogWarning("Level1OnboardingController: Skip requested before first manual success — ignored.");
            return;
        }
        _skipRequested = true;
    }

    public bool ShouldRunFor(LevelConfigSO levelConfig)
    {
        if (levelConfig == null) return false;
        if (!LevelTutorialProgress.ShouldShowForLevelNumber(levelConfig.levelNumber)) return false;
        if (ResolveSequence(levelConfig) == null) return false;
        return true;
    }

    public bool IsConfigured
    {
        get
        {
            OnboardingSequenceSO seq = ResolveSequence(null);
            return seq != null && seq.beatOrder != null && seq.beatOrder.Length > 0;
        }
    }

    /// <summary>Returns true if a sequence SO can be resolved from either the level config or the fallback field.</summary>
    public bool IsSequenceResolvable(LevelConfigSO levelConfig) => ResolveSequence(levelConfig) != null;

    public IEnumerator PlayIfNeeded(LevelConfigSO levelConfig)
    {
        _attemptAborted = false;
        EnsureDefaultBeatComponents();

        if (!ShouldRunFor(levelConfig)) yield break;

        OnboardingSequenceSO sequence = ResolveSequence(levelConfig);
        if (sequence == null) yield break;

        // Normalize a controller-owned copy so per-level adjustments never mutate the shared asset.
        sequence = EnsureMutableSequence(sequence);
        NormalizeSequenceForLevel(sequence, levelConfig.levelNumber);
        EnsureBeatComponentsForSequence(sequence);
        CollectBeats();

        TutorialRuntimeState.Begin(levelConfig.levelNumber);
        HideOnboardingBlockedUI();

        try
        {
            OnboardingContext ctx = BuildContext(sequence, levelConfig.levelNumber);

            int startIndex = OnboardingPersistence.GetResumeStartIndex(levelConfig.levelNumber);
            OnboardingBeatType[] order = sequence.beatOrder;
            for (int i = startIndex; i < order.Length; i++)
            {
                if (_attemptAborted)
                    yield break;

                OnboardingBeat beat = FindBeat(order[i]);
                if (beat == null)
                {
                    DebugLogger.LogWarning($"Level1OnboardingController: No beat registered for type {order[i]} (index {i}). Skipping.");
                    OnboardingPersistence.SetLastCompletedBeatIndex(levelConfig.levelNumber, i);
                    continue;
                }

                if (i == startIndex && startIndex > 0)
                    beat.OnResumeFromHere(ctx);

                yield return beat.Play(ctx);

                if (_attemptAborted)
                    yield break;

                OnboardingPersistence.SetLastCompletedBeatIndex(levelConfig.levelNumber, i);
                if (_skipRequested && _firstManualSuccess)
                    break;
            }
        }
        finally
        {
            RestoreOnboardingState();
        }
    }

    private void HandleLevelAttemptAborted()
    {
        _attemptAborted = true;
        RestoreOnboardingState();
    }

    private void RestoreOnboardingState()
    {
        RestoreOnboardingHiddenUI();
        TutorialRuntimeState.Clear();
    }

    private OnboardingContext BuildContext(OnboardingSequenceSO sequence, int levelNumber)
    {
        EnsureRuntimeHelpers();
        ProtagonistManager prot = _protagonistManager != null ? _protagonistManager : ProtagonistManager.Instance;
        Camera cam = _worldCamera != null ? _worldCamera : Camera.main;
        PlayerBase playerBase = _playerBase != null ? _playerBase : FindFirstObjectByType<PlayerBase>();
        WaveSpawner spawner = _waveSpawner != null ? _waveSpawner : FindFirstObjectByType<WaveSpawner>();
        DialogueController dialogue = _dialogueController != null
            ? _dialogueController
            : FindActiveDialogueController();
        TutorialSpotlightOverlay spotlight = _spotlight != null ? _spotlight : FindFirstObjectByType<TutorialSpotlightOverlay>(FindObjectsInactive.Include);
        TutorialIntroPlayer introPlayer = _introPlayer != null ? _introPlayer : FindFirstObjectByType<TutorialIntroPlayer>(FindObjectsInactive.Include);
        DemoHeartSimulator demoHearts = _demoHeartSimulator != null ? _demoHeartSimulator : FindFirstObjectByType<DemoHeartSimulator>(FindObjectsInactive.Include);

        return new OnboardingContext(
            sequence,
            levelNumber,
            dialogue,
            spotlight,
            introPlayer,
            demoHearts,
            prot,
            spawner,
            playerBase,
            _guideUI,
            cam,
            setBeatCompleted: i => OnboardingPersistence.SetLastCompletedBeatIndex(levelNumber, i),
            skipRequested: () => _skipRequested,
            markFirstManualSuccess: () => _firstManualSuccess = true);
    }

    private void EnsureRuntimeHelpers()
    {
        _dialogueController ??= FindActiveDialogueController();
        if (_dialogueController == null)
            _dialogueController = DialogueController.CreateRuntime();
        _spotlight ??= FindFirstObjectByType<TutorialSpotlightOverlay>(FindObjectsInactive.Include);
        if (_spotlight == null)
            _spotlight = TutorialSpotlightOverlay.CreateRuntime();

        _introPlayer ??= FindFirstObjectByType<TutorialIntroPlayer>(FindObjectsInactive.Include);
        if (_introPlayer == null)
            _introPlayer = TutorialIntroPlayer.CreateRuntime();

        _demoHeartSimulator ??= FindFirstObjectByType<DemoHeartSimulator>(FindObjectsInactive.Include);
        if (_demoHeartSimulator == null)
        {
            GameObject demoObject = new("[Runtime] DemoHeartSimulator");
            demoObject.transform.SetParent(transform, false);
            _demoHeartSimulator = demoObject.AddComponent<DemoHeartSimulator>();
        }
    }

    private static DialogueController FindActiveDialogueController()
    {
        DialogueController[] controllers = FindObjectsByType<DialogueController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            DialogueController controller = controllers[i];
            if (controller != null && controller.gameObject.activeInHierarchy)
                return controller;
        }

        return null;
    }

    private OnboardingSequenceSO ResolveSequence(LevelConfigSO levelConfig)
    {
        if (levelConfig != null && levelConfig.onboardingSequence != null)
            return ResolveSceneOverrideSequence(levelConfig.onboardingSequence);
        if (_fallbackSequence != null)
            return ResolveSceneOverrideSequence(_fallbackSequence);
        if (levelConfig != null && levelConfig.tutorialSequence != null)
            return ResolveLegacyTutorialSequence(levelConfig.tutorialSequence);
        return null;
    }

    private OnboardingSequenceSO ResolveSceneOverrideSequence(OnboardingSequenceSO source)
    {
        if (source == null)
            return null;

        if (!HasSceneGifFallbacks())
            return source;

        if (_runtimeSceneOverrideSequence != null && _runtimeSceneOverrideSource == source)
            return _runtimeSceneOverrideSequence;

        DestroyRuntimeSequence(_runtimeSceneOverrideSequence);
        _runtimeSceneOverrideSource = source;
        _runtimeSceneOverrideSequence = Instantiate(source);
        _runtimeSceneOverrideSequence.name = $"{source.name}_RuntimeSceneOverrides";
        _runtimeSceneOverrideSequence.hideFlags = HideFlags.HideAndDontSave;
        ApplySceneGifFallbacks(_runtimeSceneOverrideSequence);
        return _runtimeSceneOverrideSequence;
    }

    /// <summary>
    /// Returns a controller-owned, mutable copy of <paramref name="source"/> so per-level
    /// normalization never writes back to the shared <see cref="OnboardingSequenceSO"/> asset.
    /// Sequences already produced as runtime instances (scene-override / legacy) are returned as-is.
    /// </summary>
    private OnboardingSequenceSO EnsureMutableSequence(OnboardingSequenceSO source)
    {
        if (source == null)
            return null;

        if (source == _runtimeSceneOverrideSequence || source == _runtimeLegacySequence)
            return source;

        if (_runtimeNormalizedSequence != null && _runtimeNormalizedSource == source)
            return _runtimeNormalizedSequence;

        DestroyRuntimeSequence(_runtimeNormalizedSequence);
        _runtimeNormalizedSource = source;
        _runtimeNormalizedSequence = Instantiate(source);
        _runtimeNormalizedSequence.name = $"{source.name}_RuntimeNormalized";
        _runtimeNormalizedSequence.hideFlags = HideFlags.HideAndDontSave;
        return _runtimeNormalizedSequence;
    }

    private OnboardingSequenceSO ResolveLegacyTutorialSequence(Level1TutorialSequenceSO legacySequence)
    {
        if (legacySequence == null)
            return null;

        if (_runtimeLegacySequence != null && _runtimeLegacySource == legacySequence)
            return _runtimeLegacySequence;

        if (_runtimeLegacySequence != null)
        {
            if (Application.isPlaying)
                Destroy(_runtimeLegacySequence);
            else
                DestroyImmediate(_runtimeLegacySequence);
        }

        _runtimeLegacySource = legacySequence;
        _runtimeLegacySequence = CreateRuntimeSequenceFromLegacy(legacySequence);
        ApplySceneGifFallbacks(_runtimeLegacySequence);
        return _runtimeLegacySequence;
    }

    private void ApplySceneGifFallbacks(OnboardingSequenceSO sequence)
    {
        if (sequence == null || !HasSceneGifFallbacks())
            return;

        bool applied = false;
        bool hasBasicTeachSteps = sequence.basicTeachSteps != null && sequence.basicTeachSteps.Length > 0;
        if (!hasBasicTeachSteps)
            applied |= ApplySceneGifFallback(sequence.soloTeachStep, ref sequence.soloTeachVideo);

        applied |= ApplySceneGifFallback(sequence.comboTeachStep, ref sequence.comboTeachVideo);
        applied |= ApplyBasicTeachSceneGifFallbacks(sequence);

        if (!applied)
            DebugLogger.LogWarning("Level1OnboardingController: GIF scene overrides were assigned, but no matching teach step was found. Override skipped.");
    }

    private bool HasSceneGifFallbacks()
        => _baGifTexture != null
            || (_baGifFrames != null && _baGifFrames.Length > 0)
            || _haGifTexture != null
            || (_haGifFrames != null && _haGifFrames.Length > 0)
            || _ouGifTexture != null
            || (_ouGifFrames != null && _ouGifFrames.Length > 0);

    private bool ApplyBasicTeachSceneGifFallbacks(OnboardingSequenceSO sequence)
    {
        if (sequence.basicTeachSteps == null || sequence.basicTeachSteps.Length == 0)
            return false;

        if (sequence.basicTeachVideos == null || sequence.basicTeachVideos.Length != sequence.basicTeachSteps.Length)
            sequence.basicTeachVideos = new OnboardingVideoTemplate[sequence.basicTeachSteps.Length];

        bool applied = false;
        for (int i = 0; i < sequence.basicTeachSteps.Length; i++)
        {
            OnboardingVideoTemplate template = sequence.basicTeachVideos[i];
            if (ApplySceneGifFallback(sequence.basicTeachSteps[i], ref template))
            {
                sequence.basicTeachVideos[i] = template;
                applied = true;
            }
        }

        return applied;
    }

    private bool ApplySceneGifFallback(Level1TutorialStepSO step, ref OnboardingVideoTemplate template)
    {
        if (step == null)
            return false;

        if (IsStepForCharacter(step, BaCharacterId) && HasGifFallback(_baGifTexture, _baGifFrames))
        {
            template = WithSceneGifFallback(template, _baGifTexture, _baGifFrames, _baGifFramesPerSecond);
            return true;
        }

        if (IsStepForCharacter(step, "HA") && HasGifFallback(_haGifTexture, _haGifFrames))
        {
            template = WithSceneGifFallback(template, _haGifTexture, _haGifFrames, _haGifFramesPerSecond);
            return true;
        }

        if ((IsStepForCharacter(step, "OU") || IsStepForCharacter(step, "O"))
            && HasGifFallback(_ouGifTexture, _ouGifFrames))
        {
            template = WithSceneGifFallback(template, _ouGifTexture, _ouGifFrames, _ouGifFramesPerSecond);
            return true;
        }

        return false;
    }

    private static bool HasGifFallback(Texture2D texture, Sprite[] frames)
        => texture != null || (frames != null && frames.Length > 0);

    private static OnboardingVideoTemplate WithSceneGifFallback(
        OnboardingVideoTemplate template,
        Texture2D gifTexture,
        Sprite[] gifFrames,
        float framesPerSecond)
    {
        template.videoClip = null;
        template.gifTexture = gifTexture;
        template.gifFrames = gifFrames;
        template.gifFramesPerSecond = Mathf.Max(1f, framesPerSecond);
        if (string.IsNullOrWhiteSpace(template.tapToProceedText))
            template.tapToProceedText = "Tap anywhere to continue";
        return template;
    }

    private static bool IsStepForCharacter(Level1TutorialStepSO step, string characterId)
    {
        if (step == null || string.IsNullOrWhiteSpace(characterId))
            return false;

        if (step.targetCharacter != null
            && string.Equals(step.targetCharacter.characterID, characterId, System.StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(step.promptId, characterId, System.StringComparison.OrdinalIgnoreCase);
    }

    private static void DestroyRuntimeSequence(OnboardingSequenceSO sequence)
    {
        if (sequence == null)
            return;

        if (Application.isPlaying)
            Destroy(sequence);
        else
            DestroyImmediate(sequence);
    }

    private static OnboardingSequenceSO CreateRuntimeSequenceFromLegacy(Level1TutorialSequenceSO legacySequence)
    {
        OnboardingSequenceSO sequence = ScriptableObject.CreateInstance<OnboardingSequenceSO>();
        sequence.name = $"{legacySequence.name}_RuntimeOnboarding";
        sequence.hideFlags = HideFlags.HideAndDontSave;

        sequence.protagonistWalkSeconds = legacySequence.protagonistWalkSeconds;
        sequence.failuresBeforeAssist = legacySequence.failuresBeforeAssist;
        sequence.baseIntro = new OnboardingBeatCopy
        {
            fallbackText = CombineLines(legacySequence.baseIntroText, legacySequence.baseDefenseText),
        };
        sequence.soloTeachPreVideo = new OnboardingBeatCopy
        {
            fallbackText = legacySequence.drawPurposeText,
        };
        sequence.heartLossDialogue = new OnboardingBeatCopy
        {
            fallbackText = legacySequence.baseDamageText,
        };
        sequence.release = new OnboardingBeatCopy
        {
            fallbackText = legacySequence.finalReleaseText,
        };

        sequence.basicTeachSteps = CopyLegacySteps(legacySequence);
        sequence.soloTeachStep = GetLegacyStep(legacySequence, 0);
        Level1TutorialStepSO demoStep = GetLegacyStep(legacySequence, 2) ?? sequence.soloTeachStep;
        if (demoStep != null)
        {
            sequence.heartLossDemoEnemyData = demoStep.enemyData;
            sequence.heartLossDemoCharacter = demoStep.targetCharacter;
        }

        List<OnboardingBeatType> order = new()
        {
            OnboardingBeatType.ProtagonistIntro,
            OnboardingBeatType.BaseIntro,
        };
        if (sequence.soloTeachStep != null)
            order.Add(OnboardingBeatType.SoloTeach);
        if (sequence.heartLossDemoEnemyData != null)
            order.Add(OnboardingBeatType.HeartLossDemo);
        order.Add(OnboardingBeatType.Release);
        sequence.beatOrder = order.ToArray();

        return sequence;
    }

    internal static void NormalizeSequenceForLevel(OnboardingSequenceSO sequence, int levelNumber)
    {
        if (sequence == null || levelNumber != LevelTutorialProgress.Level2TutorialLevelNumber)
            return;

        sequence.beatOrder = new[]
        {
            OnboardingBeatType.ComboTeach,
            OnboardingBeatType.FocusModeTeach,
            OnboardingBeatType.Release,
        };

        if (sequence.focusPracticeStep == null)
            sequence.focusPracticeStep = sequence.soloTeachStep != null
                ? sequence.soloTeachStep
                : sequence.comboTeachStep;

        sequence.focusPracticeKillCount = Mathf.Max(2, sequence.focusPracticeKillCount);

        if (sequence.focusChainStep == null)
            sequence.focusChainStep = sequence.comboTeachStep != null
                ? sequence.comboTeachStep
                : sequence.focusPracticeStep;

        sequence.focusChainEnemyCount = Mathf.Max(3, sequence.focusChainEnemyCount);

        if (string.IsNullOrWhiteSpace(sequence.focusPracticeIntro.fallbackText)
            && sequence.focusPracticeIntro.dialogue == null)
        {
            sequence.focusPracticeIntro = new OnboardingBeatCopy
            {
                fallbackText = "Keep your rhythm. Defeat two more enemies.",
            };
        }

        if (string.IsNullOrWhiteSpace(sequence.focusModeIntro.fallbackText)
            && sequence.focusModeIntro.dialogue == null)
        {
            sequence.focusModeIntro = new OnboardingBeatCopy
            {
                fallbackText = "Focus mode helps you handle heavier combat after building momentum through successful draws.",
            };
        }

        if (string.IsNullOrWhiteSpace(sequence.focusChainIntro.fallbackText)
            && sequence.focusChainIntro.dialogue == null)
        {
            sequence.focusChainIntro = new OnboardingBeatCopy
            {
                fallbackText = "Focus is active. Watch how the next group slows down, then draw once to chain them.",
            };
        }

        if (string.IsNullOrWhiteSpace(sequence.focusChainPostSuccess.fallbackText)
            && sequence.focusChainPostSuccess.dialogue == null)
        {
            sequence.focusChainPostSuccess = new OnboardingBeatCopy
            {
                fallbackText = "Good. Focus gives you room to control heavier waves.",
            };
        }
    }

    private static Level1TutorialStepSO[] CopyLegacySteps(Level1TutorialSequenceSO sequence)
    {
        if (sequence.steps == null || sequence.steps.Length == 0)
            return System.Array.Empty<Level1TutorialStepSO>();

        Level1TutorialStepSO[] copy = new Level1TutorialStepSO[sequence.steps.Length];
        for (int i = 0; i < sequence.steps.Length; i++)
            copy[i] = sequence.steps[i];
        return copy;
    }

    private static Level1TutorialStepSO GetLegacyStep(Level1TutorialSequenceSO sequence, int index)
    {
        if (sequence.steps == null || index < 0 || index >= sequence.steps.Length)
            return null;
        return sequence.steps[index];
    }

    private static string CombineLines(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
            return second ?? "";
        if (string.IsNullOrWhiteSpace(second))
            return first;
        return $"{first}\n{second}";
    }

    private void CollectBeats()
    {
        _beats.Clear();
        GetComponents<OnboardingBeat>(_beats);
    }

    internal void EnsureDefaultBeatComponents()
    {
        EnsureBeatComponent<ProtagonistIntroBeat>();
        EnsureBeatComponent<BaseIntroBeat>();
        EnsureBeatComponent<SoloTeachBeat>();
        EnsureBeatComponent<HeartLossDemoBeat>();
        EnsureBeatComponent<ReleaseBeat>();
    }

    private void EnsureBeatComponentsForSequence(OnboardingSequenceSO sequence)
    {
        if (sequence == null || sequence.beatOrder == null)
            return;

        for (int i = 0; i < sequence.beatOrder.Length; i++)
        {
            switch (sequence.beatOrder[i])
            {
                case OnboardingBeatType.ProtagonistIntro:
                    EnsureBeatComponent<ProtagonistIntroBeat>();
                    break;
                case OnboardingBeatType.BaseIntro:
                    EnsureBeatComponent<BaseIntroBeat>();
                    break;
                case OnboardingBeatType.SoloTeach:
                    EnsureBeatComponent<SoloTeachBeat>();
                    break;
                case OnboardingBeatType.ComboTeach:
                    EnsureBeatComponent<ComboTeachBeat>();
                    break;
                case OnboardingBeatType.HeartLossDemo:
                    EnsureBeatComponent<HeartLossDemoBeat>();
                    break;
                case OnboardingBeatType.Release:
                    EnsureBeatComponent<ReleaseBeat>();
                    break;
                case OnboardingBeatType.FocusModeTeach:
                    EnsureBeatComponent<FocusModeTeachBeat>();
                    break;
            }
        }

        CollectBeats();
    }

    private void EnsureBeatComponent<T>() where T : OnboardingBeat
    {
        if (GetComponent<T>() == null)
            gameObject.AddComponent<T>();
    }

    private OnboardingBeat FindBeat(OnboardingBeatType type)
    {
        for (int i = 0; i < _beats.Count; i++)
            if (_beats[i] != null && _beats[i].BeatType == type)
                return _beats[i];
        return null;
    }

    private void HideOnboardingBlockedUI()
    {
        if (_hideDuringOnboarding == null) return;
        _hiddenOriginalState = new bool[_hideDuringOnboarding.Length];
        for (int i = 0; i < _hideDuringOnboarding.Length; i++)
        {
            GameObject go = _hideDuringOnboarding[i];
            if (go == null) continue;
            _hiddenOriginalState[i] = go.activeSelf;
            go.SetActive(false);
        }
        _onboardingHudHidden = true;
    }

    private void RestoreOnboardingHiddenUI()
    {
        if (!_onboardingHudHidden || _hideDuringOnboarding == null) return;
        for (int i = 0; i < _hideDuringOnboarding.Length; i++)
        {
            GameObject go = _hideDuringOnboarding[i];
            if (go == null || _hiddenOriginalState == null || i >= _hiddenOriginalState.Length) continue;
            go.SetActive(_hiddenOriginalState[i]);
        }
        _onboardingHudHidden = false;
    }
}
