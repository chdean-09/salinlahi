/// <summary>
/// Player-facing strings for the defeat screen, kept beside the code that renders
/// them so copy review never has to read control logic. Mirrors LevelResultsCopy.
/// </summary>
public static class DefeatScreenCopy
{
    /// <summary>Subheading under the DEFEAT banner — names what happened in one line.</summary>
    public const string Subtitle = "The base was overrun.";

    /// <summary>Caption under the hearts row when the run ended with none remaining.</summary>
    public const string NoHeartsLeftLabel = "No hearts left";

    /// <summary>Caption under the hearts row when hearts somehow remain.</summary>
    public const string HeartsLeftLabel = "Hearts left";

    /// <summary>Framed tip: restates the loss mechanic and names the counterplay.</summary>
    public const string TipLine1 = "Enemies at the base cost hearts.";
    public const string TipLine2 = "Draw the glowing symbols to stop them.";

    /// <summary>Primary action — re-enter the level.</summary>
    public const string RetryLabel = "Retry Combat";
}
