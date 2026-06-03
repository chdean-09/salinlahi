using System.Collections;
using UnityEngine;

/// <summary>
/// Beat 2 — Spotlight the player's base and play the base introduction dialogue.
/// </summary>
public sealed class BaseIntroBeat : OnboardingBeat
{
    public override OnboardingBeatType BeatType => OnboardingBeatType.BaseIntro;

    public override IEnumerator Play(OnboardingContext ctx)
    {
        if (ctx == null || ctx.Sequence == null)
            yield break;

        if (ctx.Spotlight != null && ctx.PlayerBase != null)
        {
            Bounds bounds = ResolveBaseBounds(ctx.PlayerBase);
            ctx.Spotlight.SetCamera(ctx.WorldCamera);
            ctx.Spotlight.Show(bounds, ctx.Sequence.baseSpotlightPadding);
        }

        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.baseIntro);

        if (ctx.Spotlight != null)
            ctx.Spotlight.Hide();
    }

    private static Bounds ResolveBaseBounds(PlayerBase playerBase)
    {
        Collider2D col = playerBase.GetComponent<Collider2D>();
        if (col != null) return col.bounds;
        Renderer r = playerBase.GetComponent<Renderer>();
        if (r != null) return r.bounds;
        return new Bounds(playerBase.transform.position, Vector3.one);
    }
}
