using System;
using UnityEngine;
using UnityEngine.Video;

public enum OnboardingBeatType
{
    // SALIN-225 removed ComboTeach = 3 and FocusModeTeach = 6 with the mechanics they taught.
    // The survivors keep their explicit values so serialized beatOrder blobs stay valid.
    // SALIN-241 adds MassClearTeach = 7 rather than reusing the freed 3 or 6: a stale serialized
    // beatOrder blob still carrying 3 or 6 would otherwise bind silently to the new beat.
    ProtagonistIntro = 0,
    BaseIntro = 1,
    SoloTeach = 2,
    HeartLossDemo = 4,
    Release = 5,
    MassClearTeach = 7,
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

    [Header("Beat 7 — Mass-Clear Teach (Level 2)")]
    [Tooltip("SALIN-241. Level 2 is the first level with multiKillChainEnabled on, so it is where the AOE mass-clear is introduced. Explanatory only — the beat never gates on a successful draw (D-004 forbids a pre-combat practice gate).")]
    public OnboardingBeatCopy massClearTeach = new OnboardingBeatCopy
    {
        fallbackText = "Three or more enemies can share one mark. Draw it once to clear them all.",
    };
    public OnboardingVideoTemplate massClearTeachVideo = new OnboardingVideoTemplate
    {
        tapToProceedText = "Tap anywhere to continue",
    };

    [Header("Timing")]
    [Tooltip("Seconds to display each one-shot fallback message before auto-dismiss (used only when no DialogueSO is set).")]
    public float fallbackMessageSeconds = 1.75f;

    [Tooltip("Failed attempts during draw beats before the assist animation auto-plays.")]
    public int failuresBeforeAssist = 3;
}
