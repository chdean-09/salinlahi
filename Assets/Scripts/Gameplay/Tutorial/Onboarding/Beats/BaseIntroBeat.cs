using System.Collections;
using UnityEngine;

/// <summary>
/// Beat 2 — Spotlight the player's base and play the base introduction dialogue.
/// </summary>
public sealed class BaseIntroBeat : OnboardingBeat
{
    [Tooltip("Real-time pause after the base-introduction scroll closes, before the next tutorial beat starts.")]
    [SerializeField, Min(0f)] private float _postDialogueDelaySeconds = 0.75f;

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

        // The base sits in the bottom band. Use the compact full-scroll callout above it.
        yield return OnboardingDialogueRunner.Play(ctx.Dialogue, ctx.Sequence.baseIntro, presentAtTop: true);

        if (ctx.Spotlight != null)
            ctx.Spotlight.Hide();

        // Give the player a moment to see the base before the enemy demonstration.
        if (_postDialogueDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(_postDialogueDelaySeconds);
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
