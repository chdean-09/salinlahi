using Salinlahi.Runtime.Gameplay;
using UnityEngine;

/// <summary>
/// Service bag passed to each <see cref="OnboardingBeat"/> when its Play coroutine runs.
/// Holds references to scene-level services and the active sequence data.
///
/// Constructed by <see cref="Level1OnboardingController"/> after resolving scene references.
/// </summary>
public sealed class OnboardingContext
{
    public OnboardingSequenceSO Sequence { get; }
    public int LevelNumber { get; }
    public DialogueController Dialogue { get; }
    public TutorialSpotlightOverlay Spotlight { get; }
    public TutorialIntroPlayer IntroPlayer { get; }
    public DemoHeartSimulator DemoHearts { get; }
    public ProtagonistManager Protagonist { get; }
    public WaveSpawner WaveSpawner { get; }
    public PlayerBase PlayerBase { get; }
    public Level1TutorialGuideUI GuideUI { get; }
    public Camera WorldCamera { get; }

    public System.Action<int> SetBeatCompleted { get; }
    public System.Func<bool> SkipRequested { get; }
    public System.Action MarkFirstManualSuccess { get; }

    public OnboardingContext(
        OnboardingSequenceSO sequence,
        int levelNumber,
        DialogueController dialogue,
        TutorialSpotlightOverlay spotlight,
        TutorialIntroPlayer introPlayer,
        DemoHeartSimulator demoHearts,
        ProtagonistManager protagonist,
        WaveSpawner waveSpawner,
        PlayerBase playerBase,
        Level1TutorialGuideUI guideUI,
        Camera worldCamera,
        System.Action<int> setBeatCompleted,
        System.Func<bool> skipRequested,
        System.Action markFirstManualSuccess)
    {
        Sequence = sequence;
        LevelNumber = levelNumber;
        Dialogue = dialogue;
        Spotlight = spotlight;
        IntroPlayer = introPlayer;
        DemoHearts = demoHearts;
        Protagonist = protagonist;
        WaveSpawner = waveSpawner;
        PlayerBase = playerBase;
        GuideUI = guideUI;
        WorldCamera = worldCamera;
        SetBeatCompleted = setBeatCompleted;
        SkipRequested = skipRequested;
        MarkFirstManualSuccess = markFirstManualSuccess;
    }
}
