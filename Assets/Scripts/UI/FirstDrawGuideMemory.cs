using System.Collections.Generic;

/// <summary>
/// Which glyphs have already had their one non-blocking trace guide. Pure, so the once-only rule
/// is EditMode-asserted separately from the presenter that draws it.
/// </summary>
public static class FirstDrawGuideMemory
{
    public static bool ShouldShowFor(string characterID, ISet<string> alreadyShown)
    {
        if (string.IsNullOrWhiteSpace(characterID) || alreadyShown == null)
            return false;

        return !alreadyShown.Contains(characterID.Trim().ToLowerInvariant());
    }
}
