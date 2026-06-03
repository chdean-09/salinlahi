using System;
using UnityEngine;
using UnityEngine.Video;

public enum OnboardingBeatType
{
    ProtagonistIntro = 0,
    BaseIntro = 1,
    SoloTeach = 2,
    ComboTeach = 3,
    HeartLossDemo = 4,
    Release = 5,
    FocusModeTeach = 6,
}

[Serializable]
public struct OnboardingBeatCopy
{
    [Tooltip("Single-string fallback shown via a one-shot message when no DialogueSO is set.")]
    [TextArea(1, 3)]
    public string fallbackText;

    [Tooltip("Optional DialogueSO. If set, plays multi-line typewriter dialogue via DialogueController; the fallbackText is ignored.")]
    public DialogueSO dialogue;
}

[Serializable]
public struct OnboardingVideoTemplate
{
    [Tooltip("Loops while waiting for the player to tap to proceed.")]
    public VideoClip videoClip;

    [Tooltip("Imported GIF texture used by the lightweight GIF player when videoClip is null.")]
    public Texture2D gifTexture;

    [Tooltip("Optional frame sprites sliced from gifTexture. When assigned, frames animate over the intro overlay.")]
    public Sprite[] gifFrames;

    [Min(1f)]
    [Tooltip("Playback speed for gifFrames.")]
    public float gifFramesPerSecond;

    [Tooltip("Sprite-sheet fallback used when videoClip is null. Authored AnimationClip drives the image surface.")]
    public AnimationClip animationClip;

    [TextArea(1, 2)]
    [Tooltip("Tap-anywhere prompt shown while the template loops.")]
    public string tapToProceedText;
}

[CreateAssetMenu(fileName = "Level1OnboardingSequence", menuName = "Salinlahi/Level 1 Onboarding Sequence")]
public sealed class OnboardingSequenceSO : ScriptableObject
{
    [Header("Beat Order")]
    [Tooltip("Beats run in this order. Default: ProtagonistIntro, BaseIntro, SoloTeach, HeartLossDemo, Release.")]
    public OnboardingBeatType[] beatOrder = new[]
    {
        OnboardingBeatType.ProtagonistIntro,
        OnboardingBeatType.BaseIntro,
        OnboardingBeatType.SoloTeach,
        OnboardingBeatType.HeartLossDemo,
        OnboardingBeatType.Release,
    };

    [Header("Beat 1 — Protagonist Intro")]
    public OnboardingBeatCopy protagonistIntro = new OnboardingBeatCopy
    {
        fallbackText = "The ancestors have called me back.",
    };
    [Tooltip("Seconds for the protagonist walk-in animation. Falls back to ProtagonistManager's default if 0.")]
    public float protagonistWalkSeconds = 1.75f;

    [Header("Beat 2 — Base Intro")]
    public OnboardingBeatCopy baseIntro = new OnboardingBeatCopy
    {
        fallbackText = "This is our base. If enemies reach it, we lose strength.",
    };
    [Tooltip("Padding in world units around the base bounds when computing the spotlight rect.")]
    public float baseSpotlightPadding = 0.5f;

    [Header("Beat 3 — Solo Teach (HA)")]
    public Level1TutorialStepSO soloTeachStep;
    [Tooltip("Optional ordered list of basic single-enemy teach steps. When assigned, SoloTeach runs each step in order.")]
    public Level1TutorialStepSO[] basicTeachSteps;
    public OnboardingBeatCopy soloTeachPreVideo = new OnboardingBeatCopy
    {
        fallbackText = "When an enemy approaches, draw its mark to defeat it.",
    };
    public OnboardingVideoTemplate soloTeachVideo = new OnboardingVideoTemplate
    {
        tapToProceedText = "Tap anywhere to continue",
    };
    [Tooltip("Optional per-step media for basicTeachSteps. Empty slots fall back to soloTeachVideo.")]
    public OnboardingVideoTemplate[] basicTeachVideos;
    public OnboardingBeatCopy soloTeachPostSuccess = new OnboardingBeatCopy
    {
        fallbackText = "Well drawn. There will be more.",
    };

    [Header("Beat 4 — Combo Teach (3× BA)")]
    public Level1TutorialStepSO comboTeachStep;
    [Min(2)]
    public int comboEnemyCount = 3;
    public OnboardingBeatCopy comboTeachPreVideo = new OnboardingBeatCopy
    {
        fallbackText = "Sometimes enemies arrive in numbers, all bearing the same mark.",
    };
    public OnboardingVideoTemplate comboTeachVideo = new OnboardingVideoTemplate
    {
        tapToProceedText = "Tap anywhere to continue",
    };
    public OnboardingBeatCopy comboTeachPostSuccess = new OnboardingBeatCopy
    {
        fallbackText = "A true warrior strikes with rhythm.",
    };

    [Header("Level 2 — Focus Mode Teach")]
    [Tooltip("Single-enemy practice step repeated before focus mode is introduced.")]
    public Level1TutorialStepSO focusPracticeStep;
    [Min(1)]
    public int focusPracticeKillCount = 2;
    public OnboardingBeatCopy focusPracticeIntro = new OnboardingBeatCopy
    {
        fallbackText = "Keep your rhythm. Defeat two more enemies.",
    };
    public OnboardingBeatCopy focusModeIntro = new OnboardingBeatCopy
    {
        fallbackText = "Focus mode helps you control heavier combat after building momentum through successful draws.",
    };
    public Level1TutorialStepSO focusChainStep;
    [Min(3)]
    public int focusChainEnemyCount = 3;
    public OnboardingBeatCopy focusChainIntro = new OnboardingBeatCopy
    {
        fallbackText = "Focus is active. Watch how the next group slows down, then draw once to chain them.",
    };
    public OnboardingBeatCopy focusChainPostSuccess = new OnboardingBeatCopy
    {
        fallbackText = "Good. Focus gives you room to control heavier waves.",
    };

    [Header("Beat 5 — Heart-Loss Demo")]
    [Tooltip("Enemy data used by the demo enemy. Wraps in a tutorial-only path so no real heart is lost.")]
    public EnemyDataSO heartLossDemoEnemyData;
    [Tooltip("Optional character carried by the demo enemy (visual only).")]
    public BaybayinCharacterSO heartLossDemoCharacter;
    public OnboardingVideoTemplate heartLossVideo = new OnboardingVideoTemplate
    {
        tapToProceedText = "Tap anywhere to continue",
    };
    public OnboardingBeatCopy heartLossDialogue = new OnboardingBeatCopy
    {
        fallbackText = "When an enemy reaches the base, we lose a heart. Lose them all, and the base falls.",
    };

    [Header("Beat 6 — Release")]
    public OnboardingBeatCopy release = new OnboardingBeatCopy
    {
        fallbackText = "You are ready, anak. Defend our home.",
    };

    [Header("Timing")]
    [Tooltip("Seconds to display each one-shot fallback message before auto-dismiss (used only when no DialogueSO is set).")]
    public float fallbackMessageSeconds = 1.75f;

    [Tooltip("Failed attempts during draw beats before the assist animation auto-plays.")]
    public int failuresBeforeAssist = 3;
}
