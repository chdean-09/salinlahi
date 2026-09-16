using TMPro;
using UnityEngine;

/// <summary>
/// Named font-size floors for player-facing text, in 1080x1920 reference-resolution
/// units (the CanvasScaler reference every screen shares). On a phone-class display
/// 40 units lands around 14-18px rendered, which is why Body is the floor for reading
/// text: anything smaller is what made menu and story copy hard to read on mobile.
///
/// These are floors, not a mandated scale — hero prompts and banner numbers go above
/// them freely; nothing player-facing goes below. Editor tooling
/// (Salinlahi/UI/Font Readability Report) reads the same constants so scene and
/// prefab text stays consistent with runtime-built text.
/// </summary>
public static class UITextScale
{
    /// <summary>Absolute minimum for player-facing text: hints, timers, small labels.</summary>
    public const float Caption = 30f;

    /// <summary>Supporting labels: slider readouts, locked rows, secondary lines.</summary>
    public const float Secondary = 34f;

    /// <summary>Reading text and standard button labels.</summary>
    public const float Body = 40f;

    /// <summary>Panel titles and headings.</summary>
    public const float Title = 52f;

    /// <summary>Hero numerals and prompts.</summary>
    public const float Display = 72f;

    /// <summary>Lowest fontSizeMin allowed when a text auto-sizes to fit its rect.</summary>
    public const float AutoSizeFloor = 28f;

    /// <summary>Never returns below <paramref name="floor"/>.</summary>
    public static float RaiseToFloor(float size, float floor)
    {
        return Mathf.Max(size, floor);
    }

    /// <summary>
    /// Raises <paramref name="text"/> to <paramref name="floor"/>: fontSize is clamped up,
    /// and when the component auto-sizes, fontSizeMin is clamped to
    /// <see cref="AutoSizeFloor"/> so it cannot shrink back into caption range.
    /// </summary>
    public static void ApplyFloor(TMP_Text text, float floor)
    {
        if (text == null)
            return;

        if (text.enableAutoSizing)
        {
            text.fontSizeMin = Mathf.Max(text.fontSizeMin, AutoSizeFloor);
            text.fontSizeMax = Mathf.Max(text.fontSizeMax, floor);
        }
        else
        {
            text.fontSize = Mathf.Max(text.fontSize, floor);
        }
    }
}
