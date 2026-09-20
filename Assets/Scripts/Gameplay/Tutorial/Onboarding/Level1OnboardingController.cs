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

    private readonly List<OnboardingBeat> _beats = new();
    private bool _firstManualSuccess;
    private bool _skipRequested;
    private bool[] _hiddenOriginalState;
    private bool _onboardingHudHidden;
    private Level1TutorialSequenceSO _runtimeLegacySource;
    private OnboardingSequenceSO _runtimeLegacySequence;
    private OnboardingSequenceSO _runtimeNormalizedSource;
    private OnboardingSequenceSO _runtimeNormalizedSequence;

    /// <summary>
    /// True between <see cref="TutorialRuntimeState.Begin"/> and the matching clear, so teardown
    /// only ever clears state this controller actually opened.
    /// </summary>
    private bool _ownsTutorialRuntimeState;

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
        // TutorialRuntimeState is static, so it outlives the scene. PlayIfNeeded clears it only
        // at the natural end of the sequence, and a destroyed host never runs a coroutine's
        // remaining statements — so a defeat or exit mid-beat used to strand
        // IsCombatOverrideActive / IsDrawingInputLocked and carry them into the next attempt,
        // where combat or drawing would be dead on arrival. Mirrors
        // ChallengeFlowController.OnDisable -> AbortRuntime -> ChallengeRuntimeState.Clear().
        // OnDestroy rather than OnDisable: the controller may be toggled inside one attempt.
        ReleaseTutorialRuntimeState();

        // Unconditional, and separate from the ownership-gated clear above. The heart-loss demo's
        // window is closed by its own finally, which a destroyed host never runs — and a window
        // left open would disable real base damage for the whole of the next attempt. The one
        // direction this flag must never fail in is "stuck on".
        TutorialRuntimeState.SetHeartLossDemoActive(false);

        DestroyRuntimeSequence(_runtimeLegacySequence);
        DestroyRuntimeSequence(_runtimeNormalizedSequence);
        _runtimeLegacySequence = null;
        _runtimeNormalizedSequence = null;
    }

    /// <summary>
    /// Clears the shared tutorial statics, but only when this controller is the one that opened
    /// them. Idempotent, so the natural end of the sequence and teardown can both call it.
    /// </summary>
    private void ReleaseTutorialRuntimeState()
    {
        if (!_ownsTutorialRuntimeState)
            return;

        _ownsTutorialRuntimeState = false;
        TutorialRuntimeState.Clear();
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
        _ownsTutorialRuntimeState = true;
        HideOnboardingBlockedUI();

        OnboardingContext ctx = BuildContext(sequence, levelConfig.levelNumber);

        int startIndex = OnboardingPersistence.GetResumeStartIndex(levelConfig.levelNumber);
        OnboardingBeatType[] order = sequence.beatOrder;

        // Beats before startIndex never Play, so any world state they would have established is
        // missing for the whole level. ProtagonistIntroBeat is the case that bites: LevelFlowController
        // spawns the protagonist 5 units below the screen whenever protagonistWalksIn is set (Level 1
        // only), and that beat is what walks him up. Resuming past it left Level 1 with no visible
        // protagonist. OnResumeFromHere is the "restore my state without replaying me" hook, and every
        // beat except ProtagonistIntroBeat inherits it as a no-op, so replaying it here is cheap.
        for (int i = 0; i < startIndex && i < order.Length; i++)
            FindBeat(order[i])?.OnResumeFromHere(ctx);

        for (int i = startIndex; i < order.Length; i++)
        {
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

            OnboardingPersistence.SetLastCompletedBeatIndex(levelConfig.levelNumber, i);
            if (_skipRequested && _firstManualSuccess)
                break;
        }

        RestoreOnboardingHiddenUI();
        ReleaseTutorialRuntimeState();
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

        DialogueController inactiveFallback = null;

        for (int i = 0; i < controllers.Length; i++)
        {
            DialogueController controller = controllers[i];
            if (controller == null)
                continue;

            if (controller.gameObject.activeInHierarchy)
                return controller;

            inactiveFallback ??= controller;
        }

        return inactiveFallback;
    }

    private OnboardingSequenceSO ResolveSequence(LevelConfigSO levelConfig)
    {
        if (levelConfig != null && levelConfig.onboardingSequence != null)
            return levelConfig.onboardingSequence;
        if (_fallbackSequence != null)
            return _fallbackSequence;
        if (levelConfig != null && levelConfig.tutorialSequence != null)
            return ResolveLegacyTutorialSequence(levelConfig.tutorialSequence);
        return null;
    }

    /// <summary>
    /// Returns a controller-owned, mutable copy of <paramref name="source"/> so per-level
    /// normalization never writes back to the shared <see cref="OnboardingSequenceSO"/> asset.
    /// Sequences already produced as runtime instances (legacy conversion) are returned as-is.
    /// </summary>
    private OnboardingSequenceSO EnsureMutableSequence(OnboardingSequenceSO source)
    {
        if (source == null)
            return null;

        if (source == _runtimeLegacySequence)
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
        return _runtimeLegacySequence;
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
        sequence.heartLossDialogue = new OnboardingBeatCopy
        {
            fallbackText = legacySequence.baseDamageText,
        };
        sequence.release = new OnboardingBeatCopy
        {
            fallbackText = legacySequence.finalReleaseText,
        };

        // The eight-beat lesson removed SoloTeach: drawing/glyph teaching moved into
        // EnemyIntroductionBeat, one enemy at a time, triggered by a spawn. This legacy
        // converter no longer has a teach beat to populate, so it only still derives the
        // heart-loss demo enemy/character from the old step list (index 2, the demo step in
        // the legacy four-step layout, falling back to index 0 if that step is missing).
        Level1TutorialStepSO demoStep = GetLegacyStep(legacySequence, 2) ?? GetLegacyStep(legacySequence, 0);
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
        if (sequence.heartLossDemoEnemyData != null)
            order.Add(OnboardingBeatType.HeartLossDemo);
        order.Add(OnboardingBeatType.Release);
        sequence.beatOrder = order.ToArray();

        return sequence;
    }

    /// <summary>
    /// Level-specific normalization keeps the runtime copy aligned with the campaign content.
    /// The serialized asset is never mutated.
    ///
    /// Legacy compatibility for a Level 2 sequence if one is authored again. The current Level 2
    /// config intentionally has no sequence, so normal level flow never calls this path.
    /// </summary>
    /// <remarks>
    /// The empty-order fallback remains for that compatibility path so an accidentally authored
    /// sequence cannot inherit Level 1's multi-beat defaults. A non-empty authored order is still
    /// preserved verbatim.
    ///
    /// This mutates a clone, never the on-disk asset -- <c>EnsureMutableSequence</c> instantiates a
    /// copy with <c>HideFlags.HideAndDontSave</c> before this runs.
    /// </remarks>
    internal static void NormalizeSequenceForLevel(OnboardingSequenceSO sequence, int levelNumber)
    {
        if (sequence == null)
            return;

        if (levelNumber != LevelTutorialProgress.Level2TutorialLevelNumber)
            return;

        if (sequence.beatOrder == null || sequence.beatOrder.Length == 0)
            sequence.beatOrder = new[] { OnboardingBeatType.Release };
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
        EnsureBeatComponent<HeartLossDemoBeat>();
        EnsureBeatComponent<ReleaseBeat>();
        // SALIN-241. Level 2's beat, attached alongside the rest. A beat only runs when the
        // sequence's beatOrder schedules it, so attaching it everywhere costs nothing.
        EnsureBeatComponent<MassClearTeachBeat>();
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
                case OnboardingBeatType.HeartLossDemo:
                    EnsureBeatComponent<HeartLossDemoBeat>();
                    break;
                case OnboardingBeatType.Release:
                    EnsureBeatComponent<ReleaseBeat>();
                    break;
                case OnboardingBeatType.MassClearTeach:
                    EnsureBeatComponent<MassClearTeachBeat>();
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
