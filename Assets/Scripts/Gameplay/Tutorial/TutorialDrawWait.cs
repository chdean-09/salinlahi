using System.Collections;
using UnityEngine;

/// <summary>
/// Blocks until the recognizer resolves the expected character. Extracted from SoloTeachBeat when
/// that beat was deleted; the eight-beat lesson's beat 8 is its only remaining caller.
/// </summary>
public static class TutorialDrawWait
{
    public static IEnumerator WaitForCorrectDraw(string expectedCharacterID)
    {
        bool resolved = false;
        System.Action<RecognitionResult, bool, float> handler = (result, passed, _) =>
        {
            if (passed && string.Equals(result.characterID, expectedCharacterID,
                    System.StringComparison.OrdinalIgnoreCase))
                resolved = true;
        };
        EventBus.OnRecognitionResolved += handler;
        try { yield return new WaitUntil(() => resolved); }
        finally { EventBus.OnRecognitionResolved -= handler; }
    }

    /// <summary>
    /// Blocks until the recognizer resolves the expected character, or until
    /// <paramref name="abandonWhen"/> answers true — whichever happens first.
    /// <paramref name="onFinished"/> is called once with true for the draw and false for the
    /// abandonment.
    ///
    /// <para>
    /// <b>The draw is checked before the abandonment, every tick, and that ordering is the whole
    /// point.</b> The correct draw KILLS the enemy, so the caller's "my subject is gone" condition
    /// becomes true in the same frame the recognition resolves. Polling the abandonment first would
    /// report every successful draw as a subject that walked away.
    /// </para>
    ///
    /// <para>
    /// A null <paramref name="abandonWhen"/> is the plain wait, which is what
    /// <see cref="WaitForCorrectDraw"/> still is.
    /// </para>
    /// </summary>
    public static IEnumerator WaitForCorrectDrawOrAbandon(
        string expectedCharacterID,
        System.Func<bool> abandonWhen,
        System.Action<bool> onFinished)
    {
        bool resolved = false;
        System.Action<RecognitionResult, bool, float> handler = (result, passed, _) =>
        {
            if (passed && string.Equals(result.characterID, expectedCharacterID,
                    System.StringComparison.OrdinalIgnoreCase))
                resolved = true;
        };
        EventBus.OnRecognitionResolved += handler;
        try
        {
            while (!resolved && (abandonWhen == null || !abandonWhen()))
                yield return null;
        }
        finally { EventBus.OnRecognitionResolved -= handler; }

        onFinished?.Invoke(resolved);
    }
}
