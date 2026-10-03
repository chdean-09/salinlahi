using System.Collections;
using UnityEngine;

/// <summary>
/// Final dialogue, mark the active tutorial as seen, clear resume progress, hand control
/// back to the LevelFlowController so normal waves can begin.
/// </summary>
public sealed class ReleaseBeat : OnboardingBeat
{
    public override OnboardingBeatType BeatType => OnboardingBeatType.Release;

    public override IEnumerator Play(OnboardingContext ctx)
    {
        if (ctx == null || ctx.Sequence == null) yield break;

        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.release);

        LevelTutorialProgress.MarkTutorialSeen(ctx.LevelNumber);
        OnboardingPersistence.Clear(ctx.LevelNumber);
    }
}
