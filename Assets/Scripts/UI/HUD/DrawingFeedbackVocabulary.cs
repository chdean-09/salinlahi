/// <summary>
/// Player-facing wording for drawing-recognition outcomes (SALIN-163 AC1).
///
/// The recognizer's confidence score is deliberately not a parameter of anything here.
/// FeedbackToast used to render it straight to the player as "83%", which graded them
/// against an internal threshold without telling them what to change. Keeping the score
/// out of these signatures is what stops that leak coming back: a caller cannot print
/// what it has no way to pass in.
/// </summary>
public static class DrawingFeedbackVocabulary
{
    public const string Accepted = "Nice — that's the one.";
    public const string RejectedFirstAttempt = "Not quite. Give that stroke another try.";
    public const string RejectedAgain = "Almost. Take your time with the shape.";
    public const string HelpOffered = "Want to see how this one is drawn?";

    /// <summary>
    /// Wording for a rejected drawing. <paramref name="consecutiveRejects"/> counts the
    /// rejection being reported, so the first rejection in a run passes 1.
    /// When <paramref name="helpAvailable"/> is set the player has reached the configured
    /// help threshold and the wording switches to offering the hint rather than repeating
    /// encouragement they have already read.
    /// </summary>
    public static string ForRejection(int consecutiveRejects, bool helpAvailable)
    {
        if (helpAvailable)
            return HelpOffered;

        return consecutiveRejects <= 1 ? RejectedFirstAttempt : RejectedAgain;
    }
}
