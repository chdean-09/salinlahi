using UnityEngine;

// Per-boss audio bank. Holds all clip references and audio-tuning fields
// for one boss encounter. Referenced by BossConfigSO.audioBank and consumed
// by BossAudio (on the boss prefab) at runtime. Designers can build a new
// boss with a different sonic identity by creating a new asset of this type
// and assigning it on the boss's BossConfigSO.
//
// Per-category volume sliders (0..1) let designers balance the bank without
// touching clip assets or the global AudioManager sliders. They stack
// multiplicatively on top of master/BGM/SFX user sliders.
[CreateAssetMenu(fileName = "BossAudioBank", menuName = "Salinlahi/Audio/Boss Audio Bank")]
public class BossAudioBankSO : ScriptableObject
{
    [Header("BGM")]
    [Tooltip("Looping BGM played for the duration of the boss encounter.")]
    public AudioClip bgm;

    [Tooltip("Volume scale for this boss's BGM. Multiplies with Master & BGM user sliders.")]
    [Range(0f, 1f)] public float bgmVolume = 1f;

    [Header("One-Shots")]
    [Tooltip("Plays once on OnBossStarted.")]
    public AudioClip introGrowl;
    [Range(0f, 1f)] public float introGrowlVolume = 1f;

    [Tooltip("Plays each time the boss spawns a minion (OnBossSummonTick).")]
    public AudioClip summonTick;
    [Range(0f, 1f)] public float summonTickVolume = 1f;

    [Tooltip("Plays on OnBossExhausted (winding-down state).")]
    public AudioClip bodyFall;
    [Range(0f, 1f)] public float bodyFallVolume = 1f;

    [Tooltip("Plays on OnBossVulnerabilityExpired (player failed to break the boss in time).")]
    public AudioClip vulnerabilityExpiredLaugh;
    [Range(0f, 1f)] public float vulnerabilityExpiredLaughVolume = 1f;

    [Tooltip("Plays on OnBossDefeated (outro start).")]
    public AudioClip defeat;
    [Range(0f, 1f)] public float defeatVolume = 1f;

    [Header("Variant Pools (no-immediate-repeat random)")]
    [Tooltip("Short growls cycled on OnBossDrawHit (correct glyph during vulnerable window).")]
    public AudioClip[] hitGrowls;
    [Range(0f, 1f)] public float hitGrowlsVolume = 1f;

    [Tooltip("Long growls cycled on OnBossDamaged (HP lost).")]
    public AudioClip[] damagedGrowls;
    [Range(0f, 1f)] public float damagedGrowlsVolume = 1f;

    [Tooltip("Footstep variants played at footstepInterval during Pace-pattern phases.")]
    public AudioClip[] footsteps;
    [Range(0f, 1f)] public float footstepsVolume = 1f;

    [Tooltip("Teleport variants played on OnBossTeleport (Teleport-pattern snap).")]
    public AudioClip[] teleports;
    [Range(0f, 1f)] public float teleportsVolume = 1f;

    [Header("Footstep Cadence")]
    [Tooltip("Seconds between footstep SFX while the boss is in a Pace movement phase.")]
    [Min(0.05f)] public float footstepInterval = 0.45f;

    [Header("BGM Fade")]
    [Tooltip("Seconds to fade BGM in on OnBossStarted.")]
    [Min(0f)] public float bgmFadeInSeconds = 1f;
    [Tooltip("Seconds to fade BGM out on OnBossDefeated.")]
    [Min(0f)] public float bgmFadeOutSeconds = 1.5f;
}
