using UnityEngine;

[CreateAssetMenu(fileName = "Level1TutorialSequence", menuName = "Salinlahi/Level 1 Tutorial Sequence")]
public sealed class Level1TutorialSequenceSO : ScriptableObject
{
    [Header("Steps (in order: Gate -> Release)")]
    [Tooltip("Ordered steps for the interactive tutorial. Typically BA -> SA -> LA -> HA.")]
    public Level1TutorialStepSO[] steps;

    [Header("Fixed Dialogue Lines")]
    [Tooltip("Shown at S01_BaseIntro, before player taps through.")]
    public string baseIntroText = "This is the base.";

    [Tooltip("Shown after baseIntroText, before protagonist walks in.")]
    public string baseDefenseText = "Keep enemies away from it.";

    [Tooltip("Shown when first enemy appears and freezes.")]
    public string drawPurposeText = "Draw its syllable to defeat it.";

    [Tooltip("Shown once if the base takes damage during the Level 1 tutorial.")]
    public string baseDamageText = "The base took damage. Draw before enemies reach it.";

    [Tooltip("Shown after HA is defeated, before releasing to normal waves.")]
    public string finalReleaseText = "You are ready. Defend the base.";

    [Header("Timing")]
    [Tooltip("Seconds to display each fixed message.")]
    public float messageSeconds = 1.25f;

    [Tooltip("Seconds of idle before first hint.")]
    public float idleHintSeconds = 5f;

    [Tooltip("Seconds of idle before stronger hint.")]
    public float strongHintSeconds = 12f;

    [Tooltip("Failed attempts before auto-complete assist.")]
    public int failuresBeforeAssist = 3;

    [Header("Protagonist Walk")]
    public float protagonistWalkSeconds = 1.75f;
}
