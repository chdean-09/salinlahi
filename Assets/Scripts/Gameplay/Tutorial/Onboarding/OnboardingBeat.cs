using System.Collections;
using UnityEngine;

/// <summary>
/// Base class for every onboarding beat. Each beat is a MonoBehaviour attached to the
/// Level1OnboardingController GameObject. The controller selects beats by Type and runs
/// them in the order defined by the sequence SO.
///
/// Beats should:
///   1. Pause gameplay via GameManager.EnterDialoguePause where appropriate.
///   2. Restore any temporary state (spotlight, spawns, HUD visibility) when finishing.
///   3. Implement OnResumeFromHere if mid-sequence resume needs special restoration.
/// </summary>
public abstract class OnboardingBeat : MonoBehaviour
{
    /// <summary>The beat type this MonoBehaviour fulfills. Used by the controller to look up the right beat.</summary>
    public abstract OnboardingBeatType BeatType { get; }

    /// <summary>Main entry point. Yield through the beat's flow; return when finished.</summary>
    public abstract IEnumerator Play(OnboardingContext ctx);

    /// <summary>
    /// Optional hook for when the player resumes mid-sequence and this beat is the first to run.
    /// Default no-op. Beats that need to restore prerequisite scene state override this.
    /// </summary>
    public virtual void OnResumeFromHere(OnboardingContext ctx) { }
}
