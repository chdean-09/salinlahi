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

    public bool FirstManualSuccessRecorded => _firstManualSuccess;
    public bool SkipRequested => _skipRequested;
    public bool CanRequestSkip => _firstManualSuccess;

    private void Awake()
    {
        CollectBeats();
        if (_guideUI == null)
            _guideUI = Level1TutorialGuideUI.CreateRuntime();
        if (_guideUI != null)
            _guideUI.Initialize(RequestSkip);
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
        ProtagonistManager prot = _protagonistManager != null ? _protagonistManager : ProtagonistManager.Instance;
        Camera cam = _worldCamera != null ? _worldCamera : Camera.main;
        PlayerBase playerBase = _playerBase != null ? _playerBase : FindFirstObjectByType<PlayerBase>();
        WaveSpawner spawner = _waveSpawner != null ? _waveSpawner : FindFirstObjectByType<WaveSpawner>();
        DialogueController dialogue = _dialogueController != null ? _dialogueController : FindFirstObjectByType<DialogueController>();
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

    private OnboardingSequenceSO ResolveSequence(LevelConfigSO levelConfig)
    {
        if (levelConfig != null && levelConfig.onboardingSequence != null)
            return levelConfig.onboardingSequence;
        return _fallbackSequence;
    }

    private void CollectBeats()
    {
        _beats.Clear();
        GetComponents<OnboardingBeat>(_beats);
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
