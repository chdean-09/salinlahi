using System.Collections.Generic;
using System.Text;

/// <summary>
/// Composes the player-facing text for an authored restoration objective.
///
/// This is deliberately a pure UI helper. The objective controller owns which occurrences are
/// restored; callers pass its attempt-local state when they want a live combat rendering, and omit
/// it for a pre-combat preview.
/// </summary>
public static class RestorationObjectiveTextFormatter
{
    public static string Render(
        RestorationObjectiveDefinition definition,
        RestorationObjectiveState state = null)
    {
        if (definition?.units == null)
            return string.Empty;

        bool isWordMode = definition.displayMode == RestorationDisplayMode.GuidedWords
            || definition.displayMode == RestorationDisplayMode.ClueOnlyWords;
        var lines = new List<string>(definition.units.Count);
        var continuousContext = new StringBuilder();

        for (int unitIndex = 0; unitIndex < definition.units.Count; unitIndex++)
        {
            RestorationObjectiveUnit unit = definition.units[unitIndex];
            if (unit == null)
                continue;

            string rendered = RenderUnit(unit, definition.displayMode, state);
            if (string.IsNullOrEmpty(rendered))
                continue;

            if (isWordMode)
            {
                if (definition.displayMode == RestorationDisplayMode.ClueOnlyWords
                    && !string.IsNullOrEmpty(unit.clue))
                {
                    rendered = unit.clue + "\n" + rendered;
                }

                lines.Add(rendered);
            }
            else
            {
                continuousContext.Append(rendered);
            }
        }

        return isWordMode
            ? string.Join("\n", lines)
            : continuousContext.ToString();
    }

    public static string RenderUnit(
        RestorationObjectiveUnit unit,
        RestorationDisplayMode displayMode,
        RestorationObjectiveState state = null)
    {
        if (unit?.tokens == null)
            return string.Empty;

        bool unitComplete = IsUnitComplete(unit, state);
        var builder = new StringBuilder();

        for (int tokenIndex = 0; tokenIndex < unit.tokens.Count; tokenIndex++)
        {
            RestorationObjectiveToken token = unit.tokens[tokenIndex];
            if (token == null)
                continue;

            if (token.kind == RestorationTokenKind.Literal)
            {
                builder.Append(token.literalText);
                continue;
            }

            string label = ResolveTargetLabel(token);
            bool restored = state != null && state.IsOccurrenceRestored(token.occurrenceId);
            bool showTarget = displayMode == RestorationDisplayMode.GuidedWords
                || displayMode == RestorationDisplayMode.MarkedContext
                || (displayMode == RestorationDisplayMode.ClueOnlyWords && unitComplete)
                || (displayMode == RestorationDisplayMode.HiddenContext && restored);

            if (showTarget)
            {
                if (displayMode == RestorationDisplayMode.MarkedContext && !restored)
                    builder.Append("<u>").Append(label).Append("</u>");
                else
                    builder.Append(label);
            }
            else
            {
                builder.Append("__");
            }
        }

        if (displayMode == RestorationDisplayMode.GuidedWords
            && !string.IsNullOrEmpty(unit.clue))
        {
            builder.Append(" — ").Append(unit.clue);
        }

        return builder.ToString();
    }

    private static bool IsUnitComplete(
        RestorationObjectiveUnit unit,
        RestorationObjectiveState state)
    {
        if (unit?.tokens == null)
            return false;

        bool hasTarget = false;
        for (int tokenIndex = 0; tokenIndex < unit.tokens.Count; tokenIndex++)
        {
            RestorationObjectiveToken token = unit.tokens[tokenIndex];
            if (token?.IsTarget != true)
                continue;

            hasTarget = true;
            if (state == null || !state.IsOccurrenceRestored(token.occurrenceId))
                return false;
        }

        return !hasTarget || state != null;
    }

    private static string ResolveTargetLabel(RestorationObjectiveToken token)
    {
        string label = SpokenValueResolver.ResolveLabel(
            token?.target?.symbol,
            token?.SpokenValueId);
        return string.IsNullOrWhiteSpace(label)
            ? "?"
            : label.ToUpperInvariant();
    }
}
