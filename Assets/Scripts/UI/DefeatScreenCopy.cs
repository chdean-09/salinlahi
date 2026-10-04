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

    /// <summary>Framed tip: restates the loss mechanic and names the counterplay.</summary>
    public const string TipLine1 = "Nababawasan ang puso kapag nakakarating sa base ang mga kalaban.";
    public const string TipLine2 = "Iguhit ang kumikinang na mga simbolo para pigilan sila.";

    /// <summary>Primary action — re-enter the level.</summary>
    public const string RetryLabel = "Subukan Muli ang Labanan";
}
