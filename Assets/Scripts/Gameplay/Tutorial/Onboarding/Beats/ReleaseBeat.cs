using System.Collections;
using UnityEngine;

/// <summary>
/// Beat 6 — Final dialogue, mark tutorial as seen, clear resume progress, hand control
/// back to the LevelFlowController so normal waves can begin.
/// </summary>
public sealed class ReleaseBeat : OnboardingBeat
{
    public override OnboardingBeatType BeatType => OnboardingBeatType.Release;

    public override IEnumerator Play(OnboardingContext ctx)
    {
        if (ctx == null || ctx.Sequence == null) yield break;

        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.release);

        LevelTutorialProgress.MarkLevel1TutorialSeen();
        OnboardingPersistence.Clear();
    }
}
