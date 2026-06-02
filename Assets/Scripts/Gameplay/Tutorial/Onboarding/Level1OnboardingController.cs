using System.Collections;
using System.Collections.Generic;
using Salinlahi.Runtime.Gameplay;
using UnityEngine;

/// <summary>
/// Orchestrates the Level 1 onboarding sequence. Iterates the beat order defined on
/// <see cref="OnboardingSequenceSO"/>, yields to each beat's Play coroutine, persists
/// progress to PlayerPrefs (for mid-sequence resume), and gates execution to Level 1.
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

    private readonly List<OnboardingBeat> _beats = new();
    private bool _firstManualSuccess;
    private bool _skipRequested;
    private bool[] _hiddenOriginalState;
    private bool _onboardingHudHidden;
    private Level1TutorialSequenceSO _runtimeLegacySource;
    private OnboardingSequenceSO _runtimeLegacySequence;
    private OnboardingSequenceSO _runtimeSceneOverrideSource;
    private OnboardingSequenceSO _runtimeSceneOverrideSequence;

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

    private void OnDestroy()
    {
        DestroyRuntimeSequence(_runtimeLegacySequence);
        DestroyRuntimeSequence(_runtimeSceneOverrideSequence);
        _runtimeLegacySequence = null;
        _runtimeSceneOverrideSequence = null;
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
        if (levelConfig.levelNumber != LevelTutorialProgress.TutorialLevelNumber) return false;
        if (ResolveSequence(levelConfig) == null) return false;
        return !LevelTutorialProgress.HasSeenLevel1Tutorial();
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
        EnsureDefaultBeatComponents();
        CollectBeats();

        if (!ShouldRunFor(levelConfig)) yield break;

        OnboardingSequenceSO sequence = ResolveSequence(levelConfig);
        if (sequence == null) yield break;

        TutorialRuntimeState.Begin(levelConfig.levelNumber);
        HideOnboardingBlockedUI();

        OnboardingContext ctx = BuildContext(sequence);

        int startIndex = OnboardingPersistence.GetResumeStartIndex();
        OnboardingBeatType[] order = sequence.beatOrder;
        for (int i = startIndex; i < order.Length; i++)
        {
            OnboardingBeat beat = FindBeat(order[i]);
            if (beat == null)
            {
                DebugLogger.LogWarning($"Level1OnboardingController: No beat registered for type {order[i]} (index {i}). Skipping.");
                OnboardingPersistence.SetLastCompletedBeatIndex(i);
                continue;
            }

            if (i == startIndex && startIndex > 0)
                beat.OnResumeFromHere(ctx);

            yield return beat.Play(ctx);

            OnboardingPersistence.SetLastCompletedBeatIndex(i);
            if (_skipRequested && _firstManualSuccess)
                break;
        }

        RestoreOnboardingHiddenUI();
        TutorialRuntimeState.Clear();
    }

    private OnboardingContext BuildContext(OnboardingSequenceSO sequence)
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
            dialogue,
            spotlight,
            introPlayer,
            demoHearts,
            prot,
            spawner,
            playerBase,
            _guideUI,
            cam,
            setBeatCompleted: OnboardingPersistence.SetLastCompletedBeatIndex,
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
        if (IsStepForCharacter(sequence.soloTeachStep, BaCharacterId))
        {
            sequence.soloTeachVideo = WithSceneGifFallback(sequence.soloTeachVideo);
            applied = true;
        }

        if (IsStepForCharacter(sequence.comboTeachStep, BaCharacterId))
        {
            sequence.comboTeachVideo = WithSceneGifFallback(sequence.comboTeachVideo);
            applied = true;
        }

        if (!applied)
            DebugLogger.LogWarning("Level1OnboardingController: BA GIF scene override was assigned, but no BA teach step was found. Override skipped.");
    }

    private bool HasSceneGifFallbacks()
        => _baGifTexture != null || (_baGifFrames != null && _baGifFrames.Length > 0);

    private OnboardingVideoTemplate WithSceneGifFallback(OnboardingVideoTemplate template)
    {
        template.videoClip = null;
        template.gifTexture = _baGifTexture;
        template.gifFrames = _baGifFrames;
        template.gifFramesPerSecond = Mathf.Max(1f, _baGifFramesPerSecond);
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

        sequence.soloTeachStep = GetLegacyStep(legacySequence, 0);
        sequence.comboTeachStep = GetLegacyStep(legacySequence, 1) ?? sequence.soloTeachStep;
        Level1TutorialStepSO demoStep = GetLegacyStep(legacySequence, 2) ?? sequence.comboTeachStep ?? sequence.soloTeachStep;
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
        if (sequence.comboTeachStep != null)
            order.Add(OnboardingBeatType.ComboTeach);
        if (sequence.heartLossDemoEnemyData != null)
            order.Add(OnboardingBeatType.HeartLossDemo);
        order.Add(OnboardingBeatType.Release);
        sequence.beatOrder = order.ToArray();

        return sequence;
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
        EnsureBeatComponent<ComboTeachBeat>();
        EnsureBeatComponent<HeartLossDemoBeat>();
        EnsureBeatComponent<ReleaseBeat>();
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
