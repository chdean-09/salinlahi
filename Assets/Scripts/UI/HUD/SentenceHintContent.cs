using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Resolves the sentence hints a level offers during defense: the blanked
/// restoration context, then each focus word's meaning and its descriptor line.
/// A hint points the player at the answer's context — it never names the word —
/// so spelling recipes are stripped and every focus-word mention is blanked
/// before display. Instruction lines and challenge prompts are excluded on
/// purpose: the prompts already stand on the challenge board, and a scroll that
/// repeats them reads as a wall of text at the floor font size. Pure data
/// assembly with no Unity object lifetimes, so the same rules are
/// EditMode-testable and the HUD controller only renders what this returns.
/// </summary>
public static class SentenceHintContent
{
    /// <summary>One block in the hint scroll: an optional meaning heading plus its sentences.</summary>
    public sealed class Entry
    {
        /// <summary>Plain-language meaning hint (focus.meaning). Empty for the
        /// unlabeled restoration-context block.</summary>
        public string Label = string.Empty;
        public readonly List<string> Lines = new List<string>();
    }

    private const string Blank = "______";

    /// <summary>Opens the spelling recipe in Ugat dialogue lines ("Binubuo ito ng
    /// dalawang titik: I at NA."). Everything from this marker on is the answer's
    /// letters, not a hint — the line is cut there.</summary>
    private const string SpellingMarker = "Binubuo";

    /// <summary>The spelling recipe that opens Ugnayan/Pamana dialogue lines:
    /// uppercase syllables joined by '+' ending in a period ("A + WA.",
    /// "KA + SA + MA."). Anchored to line start so an authored mid-sentence
    /// scaffold like "Sa KA + ______" is never touched.</summary>
    private static readonly Regex SpellingSumPrefix = new Regex(
        @"^\s*[A-Z0-9]+(?:/[A-Z0-9]+)?(?:\s*\+\s*[A-Z0-9]+(?:/[A-Z0-9]+)?)+\s*\.\s*",
        RegexOptions.Compiled);

    /// <summary>
    /// Builds hint entries for a level. First the restoration text with every
    /// target blanked, then per focus word: its meaning as the heading over its
    /// descriptor line — the dialogue's first surviving line after cleaning.
    /// Later dialogue lines are drawing instructions ("Bakasin mo…"), and
    /// challenge prompts duplicate the board's working text, so neither is
    /// shown. Returns an empty list when the level has nothing to show, which
    /// is how the chip knows to stay hidden.
    /// </summary>
    public static List<Entry> Build(LevelConfigSO level)
    {
        var entries = new List<Entry>();
        if (level == null)
            return entries;

        Regex answers = BuildAnswerPattern(level.focusWords);
        var seen = new HashSet<string>();

        AddObjectiveContext(entries, level.restorationObjective, answers, seen);

        if (level.focusWords != null)
        {
            for (int i = 0; i < level.focusWords.Count; i++)
            {
                FocusWordDefinition focus = level.focusWords[i];
                if (focus == null)
                    continue;

                var entry = new Entry
                {
                    // The meaning hints at the word without naming it; the word
                    // itself (displayLabel/latinSpelling) is the answer and never
                    // heads a hint block.
                    Label = focus.meaning ?? string.Empty,
                };

                DialogueSO dialogue = focus.media?.dialogue;
                if (dialogue?.lines != null)
                {
                    // First surviving line only — the "WORD — definition."
                    // descriptor. Everything after it is instruction, not hint.
                    for (int line = 0; line < dialogue.lines.Length && entry.Lines.Count == 0; line++)
                        AddLine(entry, dialogue.lines[line].text, seen, answers);
                }

                if (entry.Lines.Count > 0)
                    entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>Adds the leading unlabeled block: the level's restoration text
    /// with every target blanked. Word-mode objectives keep their clue beside the
    /// blanks ("__ __ — ilaw ng tahanan"); context-mode units concatenate into the
    /// sentence skeleton. Levels 6-15 author no objective units, so this block is
    /// absent there.</summary>
    private static void AddObjectiveContext(
        List<Entry> entries,
        RestorationObjectiveDefinition objective,
        Regex answers,
        HashSet<string> seen)
    {
        if (objective?.units == null || objective.units.Count == 0)
            return;

        bool wordMode = objective.displayMode == RestorationDisplayMode.GuidedWords
            || objective.displayMode == RestorationDisplayMode.ClueOnlyWords;

        var context = new Entry();
        var continuous = new StringBuilder();
        for (int i = 0; i < objective.units.Count; i++)
        {
            RestorationObjectiveUnit unit = objective.units[i];
            if (unit == null)
                continue;

            if (wordMode)
            {
                string line = RenderHiddenUnit(unit).Trim();
                if (!string.IsNullOrWhiteSpace(unit.clue))
                    line = line.Length > 0 ? line + " — " + unit.clue : unit.clue;
                AddLine(context, line, seen, answers);
            }
            else
            {
                // Untrimmed: authored literals carry the spacing between units.
                continuous.Append(RenderHiddenUnit(unit));
            }
        }

        if (!wordMode)
            AddLine(context, continuous.ToString(), seen, answers);

        if (context.Lines.Count > 0)
            entries.Add(context);
    }

    /// <summary>Renders one objective unit with literals kept and every target
    /// blanked to "__", spaced when adjacent so the missing-symbol count stays
    /// legible.</summary>
    private static string RenderHiddenUnit(RestorationObjectiveUnit unit)
    {
        if (unit?.tokens == null)
            return string.Empty;

        var builder = new StringBuilder();
        bool previousWasTarget = false;
        for (int i = 0; i < unit.tokens.Count; i++)
        {
            RestorationObjectiveToken token = unit.tokens[i];
            if (token == null)
                continue;

            if (token.kind == RestorationTokenKind.Literal)
            {
                builder.Append(token.literalText);
                previousWasTarget = false;
                continue;
            }

            if (previousWasTarget)
                builder.Append(' ');
            builder.Append("__");
            previousWasTarget = true;
        }

        return builder.ToString();
    }

    /// <summary>One case-insensitive whole-word pattern covering every focus-word
    /// spelling in the level, longest first so a nested spelling (SAMA inside
    /// KASAMA) never blanks the inside of the longer word. Null when the level has
    /// no words to hide.</summary>
    private static Regex BuildAnswerPattern(IReadOnlyList<FocusWordDefinition> focusWords)
    {
        if (focusWords == null)
            return null;

        var terms = new List<string>();
        for (int i = 0; i < focusWords.Count; i++)
        {
            FocusWordDefinition focus = focusWords[i];
            if (focus == null)
                continue;

            AddAnswerTerm(terms, focus.latinSpelling);
            AddAnswerTerm(terms, focus.displayLabel);
        }

        if (terms.Count == 0)
            return null;

        terms.Sort((a, b) => b.Length.CompareTo(a.Length));
        var pattern = new StringBuilder(@"\b(?:");
        for (int i = 0; i < terms.Count; i++)
        {
            if (i > 0)
                pattern.Append('|');
            pattern.Append(Regex.Escape(terms[i]));
        }
        pattern.Append(@")\b");
        return new Regex(pattern.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private static void AddAnswerTerm(List<string> terms, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return;

        term = term.Trim();
        if (!terms.Contains(term))
            terms.Add(term);
    }

    /// <summary>Trims, cleans, and dedupes across the whole scroll: a line
    /// authored identically for two focus words shows once.</summary>
    private static void AddLine(Entry entry, string text, HashSet<string> seen, Regex answers)
    {
        string cleaned = CleanHintLine(text, answers);
        if (cleaned.Length == 0)
            return;

        if (seen.Add(cleaned))
            entry.Lines.Add(cleaned);
    }

    /// <summary>Strips everything that would make a line an answer instead of a
    /// hint: the "Binubuo ito ng ... titik: ..." recipe, a leading "A + WA."
    /// syllable sum, and every whole-word focus-word mention (blanked to
    /// "______"). Angle brackets are softened to guillemets so authored copy can
    /// never break TMP tag parsing. Returns empty when nothing but the answer
    /// survives.</summary>
    private static string CleanHintLine(string text, Regex answers)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string cleaned = text.Trim();

        int recipe = cleaned.IndexOf(SpellingMarker, StringComparison.Ordinal);
        if (recipe >= 0)
            cleaned = cleaned.Substring(0, recipe).TrimEnd();

        cleaned = SpellingSumPrefix.Replace(cleaned, string.Empty).TrimStart();

        if (answers != null)
            cleaned = answers.Replace(cleaned, Blank);

        cleaned = cleaned.Trim().Replace('<', '‹').Replace('>', '›');

        // A line that is only blanks and punctuation hints at nothing.
        string remainder = cleaned.Replace(Blank, string.Empty)
            .Trim(' ', '—', '-', '.', ',', ':', ';');
        return remainder.Length == 0 ? string.Empty : cleaned;
    }
}
