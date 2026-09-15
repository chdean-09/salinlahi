using UnityEngine;

/// <summary>
/// A level-scoped extension of <c>EnemyIntroductionBeat</c>'s four-step card into the eight-beat
/// lesson. Level 1 authors exactly one (Iligaw); every other level authors none and keeps
/// the four-step card unchanged.
///
/// <para>
/// <b>Level-scoped, not enemy-scoped.</b> This lives on <see cref="LevelConfigSO"/> rather than on
/// <see cref="EnemyDataSO"/> because ruling C2 fixes an enemy's asset and abilities across every
/// level it appears in. A lesson hung on the enemy would re-teach on Level 7.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "EnemyLesson", menuName = "Salinlahi/Enemy Lesson")]
public sealed class EnemyLessonSO : ScriptableObject
{
    [Header("Trigger")]
    [Tooltip("The enemy whose first spawn on this level runs the lesson.")]
    public EnemyDataSO enemy;

    [Tooltip("The lesson will not start until this many focus-word slots are restored. Iligaw needs "
        + "0: a mirror copy reads cold — one enemy visibly becomes two — with nothing on the clue "
        + "to compare against. Abo needed 1, because his ash masks a word's FIRST slot, which is "
        + "already masked while the needed slot is that same first slot, so the ash is a guaranteed "
        + "no-op at level start. See spec section 4.2.")]
    [Min(0)]
    public int requiredRestoredSlots = 1;

    [Header("Beat 2 — the glyph rule, once per campaign")]
    [Tooltip("Played BEFORE the ability fires, right after the halt and vignette. States the "
        + "UNIVERSAL rule — every enemy carries a mark — while this enemy's own mark is still "
        + "hidden by revealGlyphLate. Rule first, instance later: beat 7 is the payoff. Shares the "
        + "once-per-campaign latch with reactLine/ruleLine, so a later level introducing a new "
        + "type does not re-teach it. Blank copy no-ops, exactly like the other lines.")]
    public OnboardingBeatCopy glyphRuleLine;

    [Header("Beat 3 — Ability")]
    [Tooltip("Arms the ability on the introduction spawn instead of suppressing it. This INVERTS "
        + "the default rule and is correct only for an enemy whose lesson reveals the ability "
        + "before naming it.")]
    public bool armAbilityOnIntroduction;

    [Tooltip("Wall-clock seconds held after the ability fires, before the react line.")]
    [Min(0f)]
    public float abilityBeatSeconds = 2.5f;

    [Header("Beats 4 and 5 — once per campaign")]
    [Tooltip("Beat 4. The reaction to the ability the player just watched.")]
    public OnboardingBeatCopy reactLine;

    [Tooltip("Beat 5. The rule: every enemy has its own ability. Shown once per campaign.")]
    public OnboardingBeatCopy ruleLine;

    [Header("Beats 7 and 8 — glyph")]
    [Tooltip("Hides the enemy's glyph badge through beats 1-6 so beat 7 is a reveal.")]
    public bool revealGlyphLate;

    [Tooltip("Beat 8's guided draw. Null skips the draw gate and ends the lesson after beat 7.")]
    public Level1TutorialStepSO drawStep;

    [Header("Beats 9 and 10 — Restoration")]
    [Tooltip("Played once the beat-8 draw lands: what the kill just did to the word. This is "
        + "mechanics explanation, not story, so it is authored in English under ruling Q16. "
        + "Optional — left blank, the lesson simply ends at beat 8.")]
    public OnboardingBeatCopy restorationLine;
}
