using System;
using System.Collections.Generic;

/// <summary>
/// Pre-SALIN-178 implementation of the clue objective seam. Clue combat is active when the
/// level arms it and the game is actually in play.
/// </summary>
public sealed class LevelConfigClueObjectiveSource : IClueObjectiveSource
{
    private static readonly string[] Empty = Array.Empty<string>();

    private readonly LevelConfigSO _level;
    private readonly Func<bool> _isPlayingProbe;
    private readonly List<string> _objectiveContentIds = new List<string>();

    /// <param name="isPlayingProbe">
    /// Injected rather than reading GameManager directly, so this type is testable without a scene.
    /// A null probe treats the level's active flag as sufficient.
    /// </param>
    public LevelConfigClueObjectiveSource(LevelConfigSO level, Func<bool> isPlayingProbe)
    {
        _level = level;
        _isPlayingProbe = isPlayingProbe;
    }

    public bool IsClueCombatActive
    {
        get
        {
            if (_level == null || !_level.activeClueCombatEnabled)
                return false;

            return _isPlayingProbe == null || _isPlayingProbe();
        }
    }

    /// <summary>
    /// Rebuilt on each read and returned as a copy. The internal list is reused to keep the
    /// read allocation-light, but handing it out directly would alias a buffer that the next
    /// read clears -- a trap for the SALIN-178 consumer that will hold this collection.
    /// </summary>
    public IReadOnlyCollection<string> CurrentObjectiveContentIds
    {
        get
        {
            if (_level == null)
                return Empty;

            _objectiveContentIds.Clear();
            Collect(_level.learningRequirements);
            Collect(_level.practiceRequirements);
            return _objectiveContentIds.Count == 0
                ? Empty
                : _objectiveContentIds.ToArray();
        }
    }

    private void Collect(List<ContentRequirement> requirements)
    {
        if (requirements == null)
            return;

        for (int i = 0; i < requirements.Count; i++)
        {
            ContentRequirement requirement = requirements[i];
            if (requirement?.symbolValue?.symbol == null)
                continue;

            // Learning evidence keys on the canonical stable ID (for example, symbol.ba),
            // not the legacy characterID (for example, BA).
            string contentId = requirement.symbolValue.symbol.stableId;
            if (!string.IsNullOrEmpty(contentId) && !_objectiveContentIds.Contains(contentId))
                _objectiveContentIds.Add(contentId);
        }
    }
}
