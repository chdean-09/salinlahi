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
}
