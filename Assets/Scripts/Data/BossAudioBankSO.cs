using UnityEngine;

// Per-boss audio bank. Holds all clip references and audio-tuning fields
// for one boss encounter. Referenced by BossConfigSO.audioBank and consumed
// by BossAudio (on the boss prefab) at runtime. Designers can build a new
// boss with a different sonic identity by creating a new asset of this type
// and assigning it on the boss's BossConfigSO.
[CreateAssetMenu(fileName = "BossAudioBank", menuName = "Salinlahi/Audio/Boss Audio Bank")]
public class BossAudioBankSO : ScriptableObject
{
    [Header("BGM")]
    [Tooltip("Looping BGM played for the duration of the boss encounter.")]
    public AudioClip bgm;

    [Header("One-Shots")]
    [Tooltip("Plays once on OnBossStarted.")]
    public AudioClip introGrowl;

    [Tooltip("Plays each time the boss spawns a minion (OnBossSummonTick).")]
    public AudioClip summonTick;

    [Tooltip("Plays on OnBossExhausted (winding-down state).")]
    public AudioClip bodyFall;

    [Tooltip("Plays on OnBossVulnerabilityExpired (player failed to break the boss in time).")]
    public AudioClip vulnerabilityExpiredLaugh;

    [Tooltip("Plays on OnBossDefeated (outro start).")]
    public AudioClip defeat;

    [Header("Variant Pools (no-immediate-repeat random)")]
    [Tooltip("Short growls cycled on OnBossDrawHit (correct glyph during vulnerable window).")]
    public AudioClip[] hitGrowls;

    [Tooltip("Long growls cycled on OnBossDamaged (HP lost).")]
    public AudioClip[] damagedGrowls;

    [Tooltip("Footstep variants played at footstepInterval during Pace-pattern phases.")]
    public AudioClip[] footsteps;

    [Tooltip("Teleport variants played on OnBossTeleport (Teleport-pattern snap).")]
    public AudioClip[] teleports;

    [Header("Footstep Cadence")]
    [Tooltip("Seconds between footstep SFX while the boss is in a Pace movement phase.")]
    [Min(0.05f)] public float footstepInterval = 0.45f;

    [Header("BGM Fade")]
    [Tooltip("Seconds to fade BGM in on OnBossStarted.")]
    [Min(0f)] public float bgmFadeInSeconds = 1f;
    [Tooltip("Seconds to fade BGM out on OnBossDefeated.")]
    [Min(0f)] public float bgmFadeOutSeconds = 1.5f;
}
