using System.Collections;
using UnityEngine;

/// <summary>
/// Beat 7 — Explain the AOE mass-clear, the mechanic Level 2 switches on.
/// </summary>
/// <remarks>
/// SALIN-241. <c>multiKillChainEnabled</c> is off on Level 1 and on from Level 2 onward, so Level 2
/// is the first and only moment the game can introduce it. SALIN-225 deleted ComboTeachBeat, which
/// was named for combo powers but actually taught this draw, leaving the mechanic live in combat
/// (<c>CombatResolver</c> gate, <c>MassClearBadge</c> HUD feedback) and untaught. This beat is the
/// replacement, under the vocabulary the player actually sees on screen.
///
/// Deliberately a NON-BLOCKING explanatory beat: it plays optional media and one line of copy, then
/// returns. It does not spawn enemies, does not require a successful draw, and has no failure or
/// assist loop, because D-004 (LOCKED) removes the pre-combat practice gate — "draw it correctly to
/// proceed" would reinstate that gate under another name. Shaped on <see cref="BaseIntroBeat"/>
/// rather than the since-deleted SoloTeachBeat (which required a successful draw) for exactly that reason. It holds no scene state, so it
/// needs no <c>OnResumeFromHere</c> override.
/// </remarks>
public sealed class MassClearTeachBeat : OnboardingBeat
{
    public override OnboardingBeatType BeatType => OnboardingBeatType.MassClearTeach;

    public override IEnumerator Play(OnboardingContext ctx)
    {
        if (ctx == null || ctx.Sequence == null) yield break;

        // Teach the mechanic only where it is actually switched on. The flag is authored per level
        // and was turned off across Levels 1-5, which would otherwise leave this beat explaining a
        // draw the player then cannot perform - worse than never mentioning it. Reading the live
        // config rather than the level number means re-enabling the flag brings the lesson back on
        // its own, with no second edit here.
        LevelConfigSO level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
        if (level != null && !level.multiKillChainEnabled)
            yield break;

        // Optional media. TutorialIntroPlayer.Play yields straight back when every media slot on the
        // template is empty, which is how the Level 2 asset ships, so this costs nothing today.
        if (ctx.IntroPlayer != null)
            yield return ctx.IntroPlayer.Play(ctx.Sequence.massClearTeachVideo);

        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.massClearTeach);
    }
}
