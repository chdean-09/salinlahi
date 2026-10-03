using System.Collections;
using Salinlahi.Runtime.Gameplay;
using UnityEngine;

/// <summary>
/// Beat 1 — Protagonist walks onto the screen, then plays the intro dialogue.
/// </summary>
public sealed class ProtagonistIntroBeat : OnboardingBeat
{
    public override OnboardingBeatType BeatType => OnboardingBeatType.ProtagonistIntro;

    public override IEnumerator Play(OnboardingContext ctx)
    {
        if (ctx == null || ctx.Sequence == null)
            yield break;

        EnsureProtagonistAtTarget(ctx);
        yield return WaitForWalkIn(ctx.Sequence.protagonistWalkSeconds);
        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.protagonistIntro);
    }

    public override void OnResumeFromHere(OnboardingContext ctx)
    {
        // Resume: skip the walk; ensure the protagonist is at the target position.
        EnsureProtagonistAtTarget(ctx, skipWalk: true);
    }

    private static void EnsureProtagonistAtTarget(OnboardingContext ctx, bool skipWalk = false)
    {
        ProtagonistManager manager = ctx.Protagonist != null ? ctx.Protagonist : ProtagonistManager.Instance;
        if (manager == null) return;
        Vector3 target = manager.CalculateProtagonistPosition();
        manager.EnsureProtagonist(target, spawnBelowScreen: !skipWalk);
        if (!skipWalk)
            manager.WalkInProtagonist(target);
        else if (manager.ProtagonistTransform != null)
            manager.ProtagonistTransform.position = target;
    }

    private static IEnumerator WaitForWalkIn(float seconds)
    {
        if (seconds <= 0f) yield break;
        yield return new WaitForSecondsRealtime(seconds);
    }
}
