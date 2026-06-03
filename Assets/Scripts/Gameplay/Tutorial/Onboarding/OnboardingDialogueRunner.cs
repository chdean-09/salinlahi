using System.Collections;
using UnityEngine;

/// <summary>
/// Bridges OnboardingBeatCopy (single-string fallback OR DialogueSO override) to the
/// existing DialogueController. Each call yields until the dialogue completes
/// (tap-to-advance through all lines, typewriter animation included).
/// </summary>
public static class OnboardingDialogueRunner
{
    /// <summary>
    /// Plays the copy via DialogueController and yields until OnDialogueComplete fires.
    /// If <paramref name="copy"/>.dialogue is set, that DialogueSO is played as-is.
    /// Otherwise the fallback string is wrapped in a transient one-line DialogueSO.
    /// </summary>
    public static IEnumerator Play(DialogueController dialogue, OnboardingBeatCopy copy)
    {
        if (dialogue == null)
        {
            DebugLogger.LogWarning("OnboardingDialogueRunner.Play: DialogueController is null. Skipping.");
            yield break;
        }

        DialogueSO toPlay = copy.dialogue;
        DialogueSO transient = null;
        if (toPlay == null)
        {
            if (string.IsNullOrWhiteSpace(copy.fallbackText))
                yield break;
            transient = ScriptableObject.CreateInstance<DialogueSO>();
            transient.lines = new[]
            {
                new DialogueLine { text = copy.fallbackText, speakerName = "", portrait = null },
            };
            toPlay = transient;
        }

        bool complete = false;
        System.Action onComplete = () => complete = true;
        EventBus.OnDialogueComplete += onComplete;

        try
        {
            dialogue.Play(toPlay);
            yield return new WaitUntil(() => complete);
        }
        finally
        {
            EventBus.OnDialogueComplete -= onComplete;
            if (transient != null)
                Object.Destroy(transient);
        }
    }
}
