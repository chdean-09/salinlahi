/// <summary>
/// Player-facing strings for the defeat screen, kept beside the code that renders
/// them so copy review never has to read control logic. Mirrors LevelResultsCopy.
/// </summary>
public static class DefeatScreenCopy
{
    /// <summary>Subheading under the DEFEAT banner — names what happened in one line.</summary>
    public const string Subtitle = "Napuno ng mga kalaban ang base.";

    /// <summary>Caption under the hearts row when the run ended with none remaining.</summary>
    public const string NoHeartsLeftLabel = "Wala nang puso";

    /// <summary>Caption under the hearts row when hearts somehow remain.</summary>
    public const string HeartsLeftLabel = "Natitirang puso";

    /// <summary>Brief counterplay guidance that fits the frame without scrolling.</summary>
    public const string TipLine1 = "Iguhit ang mga simbolo.";
    public const string TipLine2 = "Pigilan ang mga kalaban.";

    /// <summary>Primary action — re-enter the level.</summary>
    public const string RetryLabel = "Labanan Muli";
}
