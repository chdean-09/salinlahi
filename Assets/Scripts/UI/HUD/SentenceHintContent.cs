using System.Collections.Generic;

/// <summary>
/// Resolves the sentence hints a level offers during defense: every focus word's
/// dialogue explanation lines plus the challenge-sequence prompts that evidence it.
/// Pure data assembly with no Unity object lifetimes, so the same rules are
/// EditMode-testable and the HUD controller only renders what this returns.
/// </summary>
public static class SentenceHintContent
{
    /// <summary>One block in the hint scroll: an optional focus-word heading plus its sentences.</summary>
    public sealed class Entry
    {
        /// <summary>Focus-word label (displayLabel, else latinSpelling). Empty for the
        /// trailing block of prompts that name no focus word.</summary>
        public string Label = string.Empty;
        public readonly List<string> Lines = new List<string>();
    }

    /// <summary>
    /// Builds hint entries for a level. Per focus word: its dialogue lines first,
    /// then challenge prompts whose <c>evidenceContentId</c> names that word.
    /// Prompts that name no focus word collect into a trailing unlabeled entry.
    /// GuidedTracing prompts are excluded — "Draw X. Follow the guide." is a
    /// drawing instruction, not a sentence hint. Returns an empty list when the
    /// level has nothing to show, which is how the chip knows to stay hidden.
    /// </summary>
    public static List<Entry> Build(LevelConfigSO level)
    {
        var entries = new List<Entry>();
        if (level == null)
            return entries;

        ChallengeUnitDefinition[] units = level.challengeSequence != null
            ? level.challengeSequence.units
            : null;
        var focusIds = new HashSet<string>();
        var seen = new HashSet<string>();

        if (level.focusWords != null)
        {
            for (int i = 0; i < level.focusWords.Count; i++)
            {
                FocusWordDefinition focus = level.focusWords[i];
                if (focus == null)
                    continue;

                if (!string.IsNullOrEmpty(focus.stableId))
                    focusIds.Add(focus.stableId);

                var entry = new Entry
                {
                    Label = !string.IsNullOrEmpty(focus.displayLabel)
                        ? focus.displayLabel
                        : focus.latinSpelling,
                };

                DialogueSO dialogue = focus.media?.dialogue;
                if (dialogue?.lines != null)
                {
                    for (int line = 0; line < dialogue.lines.Length; line++)
                        AddLine(entry, dialogue.lines[line].text, seen);
                }

                if (units != null)
                {
                    for (int u = 0; u < units.Length; u++)
                    {
                        ChallengeUnitDefinition unit = units[u];
                        if (IsSentencePrompt(unit)
                            && !string.IsNullOrEmpty(unit.evidenceContentId)
                            && unit.evidenceContentId == focus.stableId)
                        {
                            AddLine(entry, unit.prompt, seen);
                        }
                    }
                }

                if (entry.Lines.Count > 0)
                    entries.Add(entry);
            }
        }

        if (units != null)
        {
            Entry general = null;
            for (int u = 0; u < units.Length; u++)
            {
                ChallengeUnitDefinition unit = units[u];
                if (!IsSentencePrompt(unit) || focusIds.Contains(unit.evidenceContentId))
                    continue;

                general ??= new Entry();
                AddLine(general, unit.prompt, seen);
            }

            if (general != null && general.Lines.Count > 0)
                entries.Add(general);
        }

        return entries;
    }

    /// <summary>True for prompts that carry sentence content rather than a draw instruction.</summary>
    private static bool IsSentencePrompt(ChallengeUnitDefinition unit)
    {
        return unit != null
            && unit.mode != ChallengeMode.GuidedTracing
            && !string.IsNullOrWhiteSpace(unit.prompt);
    }

    /// <summary>Trims, drops empties, and dedupes across the whole scroll: a sentence
    /// authored in both a dialogue and a prompt shows once. Angle brackets are
    /// softened to guillemets so authored copy can never break TMP tag parsing.</summary>
    private static void AddLine(Entry entry, string text, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        string cleaned = text.Trim().Replace('<', '‹').Replace('>', '›');
        if (seen.Add(cleaned))
            entry.Lines.Add(cleaned);
    }
}
