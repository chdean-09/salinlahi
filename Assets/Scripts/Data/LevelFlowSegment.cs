using System;

/// <summary>
/// SALIN-226. One "wave group then restoration" beat of an alternating level flow.
///
/// A level's <see cref="LevelConfigSO.flowSegments"/> list partitions that level's flat
/// <c>waves</c> list in order: segment 0 consumes the first <see cref="waveCount"/> waves,
/// segment 1 the next, and so on. Segments never reorder waves and never skip any.
///
/// The list is optional and empty by default. An empty list means the level runs one
/// Defense pass then one ContextChallenge pass, exactly as it did before this ticket, so
/// no shipped level config changes behaviour or needs reserialization.
/// </summary>
[Serializable]
public class LevelFlowSegment
{
    /// <summary>
    /// Waves consumed by this segment's Defense leg, taken in order from the level's flat
    /// waves list. The segments partition that list; they never reorder it.
    /// </summary>
    public int waveCount;

    /// <summary>
    /// <see cref="ChallengeUnitDefinition.unitId"/> values played by this segment's
    /// ContextChallenge leg, in order.
    ///
    /// Empty means this segment has no restoration leg: its ContextChallenge phase
    /// completes without playing anything. That is the shape a trailing wave group needs
    /// ("clear waves 1-2, restore line 1, then clear wave 3" is two segments, the second
    /// with no units). It is authored intent, not missing content, and it is distinct from
    /// the SALIN-223 refusal — a level with no <c>challengeSequence</c> at all still
    /// refuses to complete. A segment list in which NO segment names a unit would let the
    /// whole level finish with no challenge ever played, which is precisely the defect
    /// SALIN-223 closed, so <see cref="LevelPhasePlan.SegmentPlanInvalid"/> rejects it.
    /// </summary>
    public string[] challengeUnitIds = Array.Empty<string>();
}
