/// <summary>
/// A signature enemy ability the eight-beat enemy lesson can watch.
///
/// <para>
/// <b>Why this exists.</b> Lesson beat 2 must hold until the introduced enemy has actually
/// <i>used</i> its ability, not merely until a fixed number of seconds have passed. The beat drops
/// <c>Time.timeScale</c> while every one of its own waits is realtime, so a fixed hold buys almost
/// no scaled time and the ability lands later — during beats 5-6, with the card already up. That
/// inverts the premise the lesson rests on, which is that the ability fires UNPROMPTED, before the
/// enemy is named or explained.
/// </para>
///
/// <para>
/// <b>Why an interface and not a concrete type.</b> <c>EnemyIntroductionBeat</c> used to reach for
/// <see cref="AshFirstSlotController"/> by name. That silently degraded to the fixed hold for any
/// other lesson enemy — Iligaw's lesson waits on <see cref="MirrorDecoyController"/> instead — and
/// reintroduced the exact defect the wait was added to remove. Every signature ability that a
/// lesson may be authored against implements this, so the beat asks the question rather than
/// knowing the answer.
/// </para>
///
/// <para>
/// <b>Every member is per SPAWN, never per shell.</b> Enemies are pooled: a recycled shell must
/// come back reporting false from <see cref="HasFiredThisSpawn"/>, or the next lesson would see a
/// previous occupant's ability as already fired and skip its wait entirely. Implementations clear
/// their per-spawn state in <c>OnEnable</c>.
/// </para>
/// </summary>
public interface IIntroducibleAbility
{
    /// <summary>
    /// True once this spawn's ability has actually fired and its consequence is on screen — the
    /// ash has armed, the mirror copy is standing beside its source. False before that, and false
    /// again on the next spawn of a recycled shell.
    /// </summary>
    bool HasFiredThisSpawn { get; }

    /// <summary>
    /// True while this spawn is its type's introduction spawn and the ability is deliberately
    /// inert. <see cref="HasFiredThisSpawn"/> can never become true while this holds, so a caller
    /// waiting on the ability must not wait at all.
    /// </summary>
    bool IsSuppressedForIntroductionSpawn { get; }

    /// <summary>
    /// True when every collaborator this ability needs in order to fire is actually present.
    ///
    /// <para>
    /// Asked ONCE, BEFORE the wait begins, and it answers a different question from
    /// <see cref="HasFiredThisSpawn"/>: not "has it fired yet" but "could it ever". A missing
    /// dependency does not arrive part-way through a beat, so waiting on one spends the caller's
    /// entire arm timeout — fifteen seconds of a dimmed, halted field with no card up — and then
    /// continues anyway. Answering false lets the caller fall back to its fixed hold immediately
    /// and warn, which turns a slow, silent failure into a fast, diagnosable one.
    /// </para>
    ///
    /// <para>
    /// Only structural prerequisites belong here. An ability's own firing conditions — a delay not
    /// yet elapsed, a trigger not yet met — are re-evaluated every frame and are exactly what the
    /// wait is for, so they must NOT make this false.
    /// </para>
    /// </summary>
    bool CanFireThisSpawn { get; }
}
