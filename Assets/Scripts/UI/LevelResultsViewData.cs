using System.Collections.Generic;

/// <summary>
/// The structured payload the victory screen renders. Assembled by
/// LevelFlowController.BuildResultsViewData from the same values
/// BuildResultsSummary reads, so the text fallback and the panel readout can
/// never disagree.
/// </summary>
public struct LevelResultsViewData
{
    /// <summary>Stars earned this attempt (0-3).</summary>
    public int Stars;

    /// <summary>The 0-100 score readout.</summary>
    public int Score;

    /// <summary>Hearts left at the end of the level.</summary>
    public int HeartsRemaining;

    /// <summary>Total heart slots the level started with.</summary>
    public int HeartsMax;

    /// <summary>Hints consumed this run.</summary>
    public int HintsUsed;

    /// <summary>Score points the emergency-hint penalty removed; 0 hides the row.</summary>
    public int HintPenaltyScorePoints;

    /// <summary>Display labels of the restored focus words; null/empty hides the row.</summary>
    public IReadOnlyList<string> RestoredLabels;

    /// <summary>Newly unlocked symbol count; 0 hides the row.</summary>
    public int NewSymbolCount;
}
