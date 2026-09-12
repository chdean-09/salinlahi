using System;
using System.Collections.Generic;

/// <summary>
/// Immutable per-level phase plan computed once from a <see cref="LevelConfigSO"/>.
/// A phase that is not planned is skipped by <see cref="LevelFlowMachine"/> without
/// executor involvement.
///
/// SALIN-223: <see cref="LevelPhase.ContextChallenge"/> and
/// <see cref="LevelPhase.MemoryReward"/> are now planned on EVERY level, whether or not
/// their content exists. Planning them only when content was authored meant a level with
/// neither ran Story → Defense → AtomicSave → Results and completed on wave clear alone —
/// the phases vanished silently instead of failing loudly. Missing content is now
/// reported through <see cref="ContextChallengeContentMissing"/> and
/// <see cref="MemoryRewardContentMissing"/>, and the executors refuse to complete the
/// phase rather than falling through.
///
/// FocusWords, SymbolLearning and RequiredPractice keep the content-conditional rule;
/// this ticket does not change them.
/// </summary>
public sealed class LevelPhasePlan
{
    /// <summary>The nine playable phases in LF-CONTRACT-v2 order.</summary>
    public static readonly LevelPhase[] PhaseOrder =
    {
        LevelPhase.Story,
        LevelPhase.FocusWords,
        LevelPhase.SymbolLearning,
        LevelPhase.RequiredPractice,
        LevelPhase.Defense,
        LevelPhase.ContextChallenge,
        LevelPhase.MemoryReward,
        LevelPhase.AtomicSave,
        LevelPhase.Results,
    };

    private readonly bool _hasFocusWords;
    private readonly bool _hasSymbolLearning;
    private readonly bool _hasRequiredPractice;
    private readonly bool _hasContextChallenge;
    private readonly bool _hasMemoryReward;
    private readonly LevelFlowSegment[] _segments;

    private LevelPhasePlan(
        bool hasFocusWords,
        bool hasSymbolLearning,
        bool hasRequiredPractice,
        bool hasContextChallenge,
        bool hasMemoryReward,
        bool contextChallengeContentMissing,
        bool memoryRewardContentMissing,
        LevelFlowSegment[] segments = null,
        bool segmentPlanInvalid = false)
    {
        _hasFocusWords = hasFocusWords;
        _hasSymbolLearning = hasSymbolLearning;
        _hasRequiredPractice = hasRequiredPractice;
        _hasContextChallenge = hasContextChallenge;
        _hasMemoryReward = hasMemoryReward;
        ContextChallengeContentMissing = contextChallengeContentMissing;
        MemoryRewardContentMissing = memoryRewardContentMissing;
        _segments = segments ?? Array.Empty<LevelFlowSegment>();
        SegmentPlanInvalid = segmentPlanInvalid;
    }

    /// <summary>
    /// SALIN-223. True when <see cref="LevelPhase.ContextChallenge"/> is planned but the
    /// level has no <c>challengeSequence</c> to run. The executor presents the
    /// content-missing panel and refuses to complete the phase.
    /// </summary>
    public bool ContextChallengeContentMissing { get; }

    /// <summary>
    /// SALIN-223. True when <see cref="LevelPhase.MemoryReward"/> is planned but the level
    /// has no memory content to give.
    ///
    /// Both keys are required, deliberately. The plan has always keyed this phase on
    /// <c>rewardIds</c> while the executor keys it on <c>contextMedia.cutscene</c>; on
    /// today's data the two agree on all fifteen levels, so no test can distinguish them
    /// and picking one would ship a silent wrong answer the moment a level is authored
    /// half-way. Requiring both is the only rule that cannot pass on a half-authored level.
    /// </summary>
    public bool MemoryRewardContentMissing { get; }

    /// <summary>
    /// SALIN-226. How many Defense/ContextChallenge passes this level runs. Always at least
    /// 1: an unsegmented level is simply a one-segment level, which is why every existing
    /// caller keeps its present behaviour with no branch.
    /// </summary>
    public int SegmentCount => _segments.Length == 0 ? 1 : _segments.Length;

    /// <summary>
    /// SALIN-226. The authored segments, or empty when the level is unsegmented. Empty and
    /// <see cref="SegmentCount"/> == 1 are the same state deliberately: nothing downstream
    /// should have to distinguish "no segments authored" from "one segment authored".
    /// </summary>
    public IReadOnlyList<LevelFlowSegment> Segments => _segments;

    /// <summary>
    /// SALIN-226, in the SALIN-223 reporting style. True when a level authored a segment
    /// list that cannot be honoured — the segments collapse to one pass, but LOUDLY, so the
    /// fault is reported rather than silently degrading into the unsegmented flow.
    /// </summary>
    public bool SegmentPlanInvalid { get; }

    /// <summary>
    /// SALIN-226. The half-open wave range <c>[startWaveIndex, endWaveIndexExclusive)</c>
    /// this segment's Defense leg runs, by summing the preceding segments' wave counts.
    /// False when the level is unsegmented or the index is out of range, in which case the
    /// caller runs the whole wave list exactly as it does today.
    /// </summary>
    public bool TryGetSegmentWaveRange(
        int segmentIndex, out int startWaveIndex, out int endWaveIndexExclusive)
    {
        startWaveIndex = 0;
        endWaveIndexExclusive = 0;
        if (_segments.Length == 0 || segmentIndex < 0 || segmentIndex >= _segments.Length)
            return false;

        int start = 0;
        for (int i = 0; i < segmentIndex; i++)
            start += Math.Max(0, _segments[i].waveCount);

        startWaveIndex = start;
        endWaveIndexExclusive = start + Math.Max(0, _segments[segmentIndex].waveCount);
        return true;
    }

    /// <summary>
    /// SALIN-226. The challenge unit ids this segment's ContextChallenge leg plays, or an
    /// empty list when the level is unsegmented (play the whole sequence, as today) or the
    /// segment has no restoration leg.
    /// </summary>
    public IReadOnlyList<string> SegmentChallengeUnitIds(int segmentIndex)
    {
        if (_segments.Length == 0 || segmentIndex < 0 || segmentIndex >= _segments.Length)
            return Array.Empty<string>();

        return _segments[segmentIndex].challengeUnitIds ?? Array.Empty<string>();
    }

    public static LevelPhasePlan FromConfig(LevelConfigSO config)
    {
        if (config == null)
        {
            // A null config is the legacy case. It still plans both phases: leaving them
            // unplanned here would reintroduce the silent completion through the back door.
            return new LevelPhasePlan(
                hasFocusWords: false,
                hasSymbolLearning: false,
                hasRequiredPractice: false,
                hasContextChallenge: true,
                hasMemoryReward: true,
                contextChallengeContentMissing: true,
                memoryRewardContentMissing: true);
        }

        // The challenge-prototype path plays the sequence as a pre-wave tutorial
        // replacement inside the Defense executor; planning phase 6 as well would
        // run the same sequence twice. This carve-out is the one case where
        // ContextChallenge is legitimately unplanned, and it survives SALIN-223.
        bool contextChallenge = !config.challengePrototypeEnabled;

        // SALIN-226. Segments decide how many times Defense/ContextChallenge run, NEVER
        // whether they are planned. Nothing below this line reads a segment, so SALIN-223's
        // content-missing rules are unchanged and cannot be re-loosened through this path.
        LevelFlowSegment[] segments = PlanSegments(config, out bool segmentPlanInvalid);

        return new LevelPhasePlan(
            hasFocusWords: config.focusWords != null && config.focusWords.Count > 0,
            hasSymbolLearning: config.learningRequirements != null && config.learningRequirements.Count > 0,
            hasRequiredPractice: config.practiceRequirements != null && config.practiceRequirements.Count > 0,
            hasContextChallenge: contextChallenge,
            hasMemoryReward: true,
            contextChallengeContentMissing: contextChallenge && config.challengeSequence == null,
            memoryRewardContentMissing:
                config.rewardIds == null
                || config.rewardIds.Count == 0
                || config.contextMedia == null
                || config.contextMedia.cutscene == null,
            segments: segments,
            segmentPlanInvalid: segmentPlanInvalid);
    }

    /// <summary>
    /// SALIN-226. Accepts an authored segment list only when the level can actually honour
    /// it. Every rejection returns an EMPTY segment array (so the level runs the single
    /// unsegmented pass it runs today) together with <paramref name="segmentPlanInvalid"/>
    /// true, so the fault is reported rather than silently degrading.
    /// </summary>
    private static LevelFlowSegment[] PlanSegments(LevelConfigSO config, out bool segmentPlanInvalid)
    {
        segmentPlanInvalid = false;
        if (config.flowSegments == null || config.flowSegments.Count == 0)
            return Array.Empty<LevelFlowSegment>();

        // The challenge-prototype carve-out leaves ContextChallenge unplanned, so a segment
        // loop would have no restoration leg to return through. Segments and the prototype
        // are mutually exclusive by construction, not by authoring discipline.
        if (config.challengePrototypeEnabled)
        {
            segmentPlanInvalid = true;
            return Array.Empty<LevelFlowSegment>();
        }

        if (config.challengeSequence == null)
        {
            segmentPlanInvalid = true;
            return Array.Empty<LevelFlowSegment>();
        }

        var knownUnitIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ChallengeUnitDefinition unit in config.challengeSequence.units
            ?? Array.Empty<ChallengeUnitDefinition>())
        {
            if (unit != null && !string.IsNullOrWhiteSpace(unit.unitId))
                knownUnitIds.Add(unit.unitId);
        }

        int waveBudget = config.waves == null ? 0 : config.waves.Count;
        int consumedWaves = 0;
        bool anySegmentPlaysAUnit = false;

        foreach (LevelFlowSegment segment in config.flowSegments)
        {
            if (segment == null || segment.waveCount < 0)
            {
                segmentPlanInvalid = true;
                return Array.Empty<LevelFlowSegment>();
            }

            consumedWaves += segment.waveCount;

            foreach (string unitId in segment.challengeUnitIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(unitId) || !knownUnitIds.Contains(unitId))
                {
                    segmentPlanInvalid = true;
                    return Array.Empty<LevelFlowSegment>();
                }

                anySegmentPlaysAUnit = true;
            }
        }

        // Overrunning the wave list would silently drop authored waves.
        if (consumedWaves > waveBudget)
        {
            segmentPlanInvalid = true;
            return Array.Empty<LevelFlowSegment>();
        }

        // A trailing segment with no units is legitimate ("clear waves 1-2, restore line 1,
        // then clear wave 3"). A list where NO segment names a unit is not: the level would
        // complete with the challenge never played, which is exactly the SALIN-223 defect.
        if (!anySegmentPlaysAUnit)
        {
            segmentPlanInvalid = true;
            return Array.Empty<LevelFlowSegment>();
        }

        return new List<LevelFlowSegment>(config.flowSegments).ToArray();
    }

    public bool Has(LevelPhase phase)
    {
        switch (phase)
        {
            case LevelPhase.Story:
            case LevelPhase.Defense:
            case LevelPhase.AtomicSave:
            case LevelPhase.Results:
                return true;
            case LevelPhase.FocusWords:
                return _hasFocusWords;
            case LevelPhase.SymbolLearning:
                return _hasSymbolLearning;
            case LevelPhase.RequiredPractice:
                return _hasRequiredPractice;
            case LevelPhase.ContextChallenge:
                return _hasContextChallenge;
            case LevelPhase.MemoryReward:
                return _hasMemoryReward;
            default:
                return false;
        }
    }

    /// <summary>
    /// The first planned phase strictly after <paramref name="phase"/> in
    /// LF-CONTRACT-v2 order, or <see cref="LevelPhase.Completed"/> when
    /// <paramref name="phase"/> is the last planned phase.
    /// </summary>
    public LevelPhase NextPlannedAfter(LevelPhase phase)
    {
        bool passed = phase == LevelPhase.NotStarted;
        for (int i = 0; i < PhaseOrder.Length; i++)
        {
            LevelPhase candidate = PhaseOrder[i];
            if (passed && Has(candidate))
                return candidate;
            if (candidate == phase)
                passed = true;
        }

        return LevelPhase.Completed;
    }
}
