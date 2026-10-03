/// <summary>
/// A signature ability whose firing can be POSTPONED until the enemy lesson is ready to show it.
///
/// <para>
/// <b>Why this is separate from <see cref="IIntroducibleAbility"/>.</b> That interface answers
/// questions — has it fired, could it fire, is it suppressed. This one issues an instruction, and
/// only some abilities can meaningfully obey it. An ability whose effect is a change to things
/// already on screen (ash settling on a glyph slot) is watchable whenever it lands; an ability that
/// puts a SECOND BODY on the field is not, because the body arrives with the spawn. Iligaw's mirror
/// copy used to be placed on its source's first <c>Update</c> — one frame after the spawn, above
/// the top of the camera, some ten seconds before the beat written to show it — so "one becomes
/// two" was never a moment anybody could see. Splitting the instruction out keeps every other
/// ability free of a member it would only ever no-op.
/// </para>
///
/// <para>
/// <b>The hold is per spawn and must never outlive the beat.</b> <c>EnemyIntroductionBeat</c> sets
/// it synchronously at claim time — inside <c>Enemy.Initialize</c>, before the spawn has had a
/// single frame — and clears it on every exit path, including an abort. An implementation must also
/// clear it in <c>OnEnable</c>, because a pooled shell that came back still holding would be inert
/// for a whole life with nothing coming to free it.
/// </para>
///
/// <para>
/// A hold is not suppression. Suppression means "not on this spawn at all"; a hold means "not
/// yet", and the ability fires normally the moment it is lifted.
/// </para>
/// </summary>
public interface IIntroductionHoldable
{
    /// <summary>Postpones this spawn's ability while true, and releases it when set false.</summary>
    void SetIntroductionHold(bool held);
}
