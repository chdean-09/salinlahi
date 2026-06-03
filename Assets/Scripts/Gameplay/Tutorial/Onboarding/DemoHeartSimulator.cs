using System.Collections;
using UnityEngine;

/// <summary>
/// Plays the heart-loss visuals (HUD shake + camera feedback) WITHOUT decrementing real hearts.
/// Used by the Level 1 onboarding heart-loss demo beat.
///
/// Mechanism: raises a dedicated EventBus.OnTutorialBaseHitDemo event. HeartDisplay and
/// BaseHitFeedbackController subscribe to this event in addition to OnBaseHit and play
/// the same shake/flash visuals — but no HeartSystem.LoseHeart() call is made anywhere
/// in the chain. PlayerBase is NOT involved because it listens only to OnBaseHit.
/// </summary>
public sealed class DemoHeartSimulator : MonoBehaviour
{
    [Tooltip("Damage value passed in the demo event (purely informational; nothing decrements).")]
    [SerializeField] private int _demoDamage = 1;

    [Tooltip("Seconds to wait after firing the demo hit so the shake/flash animations are visible before the next beat step.")]
    [SerializeField] private float _postHitHoldSeconds = 0.6f;

    [Tooltip("Seconds to wait after firing the demo restore so the heart refill pulse is visible.")]
    [SerializeField] private float _postRestoreHoldSeconds = 0.6f;

    /// <summary>Fires the demo hit event and waits long enough for HUD/feedback to play.</summary>
    public IEnumerator PlayDemoHit()
    {
        EventBus.RaiseTutorialBaseHitDemo(Mathf.Max(0, _demoDamage));
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, _postHitHoldSeconds));
    }

    /// <summary>Fires the demo restore event (visible heart refill pulse) and waits for it to play.</summary>
    public IEnumerator PlayDemoRestore()
    {
        EventBus.RaiseTutorialBaseRestoreDemo();
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, _postRestoreHoldSeconds));
    }
}
