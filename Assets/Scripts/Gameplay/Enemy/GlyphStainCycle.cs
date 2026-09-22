using System;

/// <summary>
/// Readability timing for a <b>stained</b> glyph badge. The cycle controls how long an ink stain
/// pulses and how long the stable character remains readable; it never changes the enemy's true
/// symbol. The same pure helpers also remain available to deliberate false-carrier badges.
///
/// <para>Two separable decisions live here, both pure:</para>
/// <list type="number">
/// <item><b>How fast may a badge change?</b> Deliberately <b>asymmetric</b>: short stain pulses
/// pass quickly, then the true face rests for a long, readable beat. Splitting the two bands keeps
/// the pulse legible as an interruption rather than a replacement glyph. <see
/// cref="ResolveFalseInterval"/> and <see cref="ResolveInterval"/> own the two bands, each with
/// its own floor.</item>
/// <item><b>How should the badge be tinted?</b> <see cref="ResolveBadgeAlpha"/> keeps the legacy
/// dimming helper for deliberate false-carrier badges without changing their identity.</item>
/// </list>
///
/// <para>Deliberately free of UnityEngine types, following <see cref="DrawTargetResolver"/> and
/// <c>ActiveClueSelector</c>, so the timing floor and the false-glyph treatment are EditMode
/// assertions rather than a scene rehearsal.</para>
/// </summary>
public sealed class GlyphStainCycle
{
    /// <summary>
    /// Shortest dwell the <b>true</b> face may be held for, in seconds. A hard floor rather than a
    /// default: this is the face the player has to read and act on, so no authored value may make
    /// it brief. Roughly a comfortable read of a single glyph.
    /// </summary>
    public const float MinimumReadableInterval = 0.9f;

    /// <summary>
    /// Shortest dwell a short stain pulse may be held for. Far below
    /// <see cref="MinimumReadableInterval"/> - churning is the point - but not zero: a wrong face
    /// should read as a blur flicking past, never as a hard strobe. The dim from
    /// <see cref="ResolveBadgeAlpha"/> already keeps the churn visually quiet.
    /// </summary>
    public const float MinimumFalseGlyphInterval = 0.22f;

    /// <summary>Authoring default: minimum seconds a short stain pulse is held.</summary>
    public const float DefaultFalseMinInterval = 0.28f;

    /// <summary>Authoring default: maximum seconds a short stain pulse is held.</summary>
    public const float DefaultFalseMaxInterval = 0.45f;

    /// <summary>Authoring default: minimum seconds the <b>true</b> face rests.</summary>
    public const float DefaultTrueMinInterval = 2.6f;

    /// <summary>Authoring default: maximum seconds the <b>true</b> face rests.</summary>
    public const float DefaultTrueMaxInterval = 3.6f;

    /// <summary>
    /// How many short stain pulses pass before the true face returns. The burst makes the stain
    /// read as an interruption while preserving the stable character underneath.
    /// </summary>
    public const int DefaultFalseBurstCount = 4;

    /// <summary>Opacity multiplier applied to a badge showing a false face.</summary>
    public const float DefaultFalseGlyphAlpha = 0.45f;

    private readonly float _falseMinInterval;
    private readonly float _falseMaxInterval;
    private readonly float _trueMinInterval;
    private readonly float _trueMaxInterval;
    private readonly int _burstCount;

    private int _remainingFalseFaces;

    /// <summary>
    /// True while the badge is in its short stain-pulse phase. The legacy property name is kept for
    /// existing timing tests and false-carrier helpers; Mantsa never swaps the actual glyph.
    /// </summary>
    public bool IsFalseGlyphVisible { get; private set; }

    /// <summary>
    /// True when the caller should advance to a fresh stain pulse. The legacy name remains for
    /// compatibility with the existing timing seam.
    /// </summary>
    public bool NeedsNewFalseGlyph { get; private set; }

    /// <summary>Time (same clock as the caller) at which the next change is due.</summary>
    public float NextToggleTime { get; private set; }

    /// <summary>Authoring-default cycle. Wrong faces churn, the true face rests.</summary>
    public GlyphStainCycle(float now, float unitRandom)
        : this(now, DefaultFalseMinInterval, DefaultFalseMaxInterval,
               DefaultTrueMinInterval, DefaultTrueMaxInterval, DefaultFalseBurstCount, unitRandom)
    {
    }

    public GlyphStainCycle(
        float now,
        float falseMinInterval,
        float falseMaxInterval,
        float trueMinInterval,
        float trueMaxInterval,
        int burstCount,
        float unitRandom)
    {
        _falseMinInterval = falseMinInterval;
        _falseMaxInterval = falseMaxInterval;
        _trueMinInterval = trueMinInterval;
        _trueMaxInterval = trueMaxInterval;
        _burstCount = burstCount < 1 ? 1 : burstCount;

        IsFalseGlyphVisible = true;
        NeedsNewFalseGlyph = true;
        _remainingFalseFaces = _burstCount - 1;
        NextToggleTime = now + ResolveFalseInterval(falseMinInterval, falseMaxInterval, unitRandom);
    }

    /// <summary>
    /// Advances the cycle to <paramref name="now"/>. Returns true when the badge needs updating,
    /// including each short stain pulse, so a caller that only watches <see
    /// cref="IsFalseGlyphVisible"/> would miss most of the pulse.
    /// <paramref name="unitRandom"/> is a 0..1 roll used to pick the next dwell.
    /// </summary>
    public bool Advance(float now, float unitRandom)
    {
        if (now < NextToggleTime)
            return false;

        if (IsFalseGlyphVisible && _remainingFalseFaces > 0)
        {
            // Mid-burst: another short stain pulse.
            _remainingFalseFaces--;
            NeedsNewFalseGlyph = true;
            NextToggleTime = now + ResolveFalseInterval(_falseMinInterval, _falseMaxInterval, unitRandom);
            return true;
        }

        if (IsFalseGlyphVisible)
        {
            // Burst spent: let the true face rest.
            IsFalseGlyphVisible = false;
            NeedsNewFalseGlyph = false;
            NextToggleTime = now + ResolveInterval(_trueMinInterval, _trueMaxInterval, unitRandom);
            return true;
        }

        // Rest over: start churning again.
        IsFalseGlyphVisible = true;
        NeedsNewFalseGlyph = true;
        _remainingFalseFaces = _burstCount - 1;
        NextToggleTime = now + ResolveFalseInterval(_falseMinInterval, _falseMaxInterval, unitRandom);
        return true;
    }

    /// <summary>
    /// Dwell for a short stain pulse. Same shape as <see cref="ResolveInterval"/> but floored at
    /// <see cref="MinimumFalseGlyphInterval"/>, so the pulse stays brief.
    /// </summary>
    public static float ResolveFalseInterval(float minInterval, float maxInterval, float unitRandom)
        => ResolveBand(minInterval, maxInterval, unitRandom, MinimumFalseGlyphInterval);

    /// <summary>
    /// Dwell for the <b>true</b> face. Orders a reversed min/max pair, clamps the roll, and applies
    /// <see cref="MinimumReadableInterval"/> so no authored value can make the answer brief.
    /// </summary>
    public static float ResolveInterval(float minInterval, float maxInterval, float unitRandom)
        => ResolveBand(minInterval, maxInterval, unitRandom, MinimumReadableInterval);

    private static float ResolveBand(float minInterval, float maxInterval, float unitRandom, float floor)
    {
        float low = Math.Min(minInterval, maxInterval);
        float high = Math.Max(minInterval, maxInterval);

        low = Math.Max(low, floor);
        high = Math.Max(high, low);

        float roll = unitRandom;
        if (roll < 0f) roll = 0f;
        if (roll > 1f) roll = 1f;
        if (float.IsNaN(roll)) roll = 0f;

        return low + ((high - low) * roll);
    }

    /// <summary>
    /// True when <paramref name="shownCharacterId"/> is not the enemy's true symbol. A missing
    /// shown id cannot be claimed false, so it reads as true and keeps full opacity.
    /// </summary>
    public static bool IsFalseGlyph(string shownCharacterId, string trueCharacterId)
    {
        if (string.IsNullOrEmpty(shownCharacterId))
            return false;

        if (string.IsNullOrEmpty(trueCharacterId))
            return false;

        return !string.Equals(shownCharacterId, trueCharacterId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Composes the badge's final opacity. <paramref name="routineAlpha"/> is whatever the
    /// swap/fade/final-draw coroutines currently own; the false-glyph dim multiplies it rather
    /// than overwriting it, so the tell survives a swap without fighting the animation.
    /// </summary>
    public static float ResolveBadgeAlpha(float routineAlpha, bool showingFalseGlyph, float falseGlyphAlpha)
    {
        float baseAlpha = routineAlpha;
        if (baseAlpha < 0f) baseAlpha = 0f;
        if (baseAlpha > 1f) baseAlpha = 1f;

        if (!showingFalseGlyph)
            return baseAlpha;

        float dim = falseGlyphAlpha;
        if (dim < 0f) dim = 0f;
        if (dim > 1f) dim = 1f;

        return baseAlpha * dim;
    }
}
