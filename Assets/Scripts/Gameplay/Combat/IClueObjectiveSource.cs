using System.Collections.Generic;

/// <summary>
/// Tells the active-clue system whether clue combat is currently running and which content
/// the level is trying to teach right now.
///
/// This is the SALIN-178 seam. The current implementation is backed by LevelConfigSO; the
/// future Defense phase can implement the same interface without changing the director.
/// </summary>
public interface IClueObjectiveSource
{
    bool IsClueCombatActive { get; }

    IReadOnlyCollection<string> CurrentObjectiveContentIds { get; }
}
