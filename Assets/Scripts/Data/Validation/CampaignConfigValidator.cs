using System;
using System.Collections.Generic;
using UnityEngine;

public static class CampaignConfigValidator
{
    private const string CampaignPath = "campaign.revised-v1";

    /// <summary>
    /// SALIN-215: issues are split into identity errors, which always block, and
    /// content-completeness issues, whose severity follows <paramref name="profile"/>.
    /// Under <see cref="ContentValidationProfile.Authoring"/> (the default, and what the runtime
    /// boot path uses) content gaps are Warnings, so unfinished media, focus words, requirements,
    /// pools, rosters, final values, challenge sequences and reward ids no longer refuse the
    /// campaign. Under <see cref="ContentValidationProfile.Strict"/> they are Errors again, which
    /// is how the release profile will gate at content-complete.
    /// The profile is a parameter rather than static state on purpose: the validator is static and
    /// the Editor re-enters it, so a mutable static profile would leak between calls.
    /// </summary>
    public static IReadOnlyList<ContentValidationIssue> Validate(
        CampaignConfigSO campaign,
        ContentValidationProfile profile = ContentValidationProfile.Authoring)
    {
        var issues = new IssueSink(profile);
        try
        {
            if (campaign == null)
            {
                AddError(issues, ContentValidationCode.ManifestMissing, CampaignPath,
                    "Campaign root is missing.");
                return issues.ToReadOnly();
            }

            ValidateManifest(campaign, issues);
            ValidateTuning(campaign, issues);
            ValidateLearningTuning(campaign, issues);
            ValidateEraTopology(campaign, issues);
            ValidateSymbolCatalog(campaign, issues);

            // SALIN: docs/design/gated-finale-levels-2-4.md Section 4. Built once here because
            // both the campaign-wide check below and ValidateLevelTopology's per-level pool check
            // need the same fact -- which level's learningRequirements actually introduces each
            // symbol via an Instruction entry -- and it requires a full pass over every level.
            Dictionary<string, List<string>> symbolIntroducersById = BuildSymbolIntroductionMap(campaign);
            ValidateSymbolIntroductionSources(campaign, symbolIntroducersById, issues);
            ValidateLevelTopology(campaign, symbolIntroducersById, issues);
        }
        catch (Exception exception)
        {
            AddError(issues, ContentValidationCode.ValidatorInternalError, CampaignPath,
                "Validation failed internally: " + exception, campaign);
        }

        return issues.ToReadOnly();
    }

    /// <summary>
    /// Collects issues and carries the profile that decides the severity of content-completeness
    /// issues, so the profile never has to be threaded as a separate parameter through every
    /// validation helper.
    /// </summary>
    private sealed class IssueSink
    {
        private readonly List<ContentValidationIssue> issues = new List<ContentValidationIssue>();
        private readonly ContentValidationSeverity contentSeverity;

        public IssueSink(ContentValidationProfile profile)
        {
            contentSeverity = profile == ContentValidationProfile.Strict
                ? ContentValidationSeverity.Error
                : ContentValidationSeverity.Warning;
        }

        public ContentValidationSeverity ContentSeverity => contentSeverity;

        public void Add(
            ContentValidationSeverity severity,
            string code,
            string path,
            string message,
            UnityEngine.Object context)
        {
            issues.Add(new ContentValidationIssue(severity, code, path, message, context));
        }

        public IReadOnlyList<ContentValidationIssue> ToReadOnly()
        {
            return issues.AsReadOnly();
        }
    }

    private static void ValidateManifest(
        CampaignConfigSO campaign,
        IssueSink issues)
    {
        string path = CampaignPath + ".manifest";
        CampaignIdentityManifest manifest = campaign.manifest;
        if (manifest == null)
        {
            AddError(issues, ContentValidationCode.ManifestMissing, path,
                "Campaign identity manifest is required.", campaign);
            return;
        }

        if (!ContentIdentity.IsCanonical(manifest.campaignId) ||
            !string.Equals(manifest.campaignId, ContentIdentity.RevisedCampaignId, StringComparison.Ordinal))
        {
            AddError(issues, ContentValidationCode.CampaignIdInvalid, path + ".campaignId",
                "Campaign ID must be campaign.revised-v1.", campaign);
        }

        if (!string.Equals(manifest.sourceWorkbookSha256,
                ContentIdentity.ApprovedWorkbookSha256, StringComparison.Ordinal))
        {
            AddError(issues, ContentValidationCode.WorkbookHashMismatch, path + ".sourceWorkbookSha256",
                "Source workbook hash does not match the approved SALIN-166 contract.", campaign);
        }

        if (!manifest.IsRevisedV1)
        {
            AddError(issues, ContentValidationCode.ManifestUnsupported, path,
                "Campaign identity, content, save, or compatibility metadata is unsupported.", campaign);
        }
    }

    private static void ValidateTuning(
        CampaignConfigSO campaign,
        IssueSink issues)
    {
        if (campaign.tuning == null || campaign.tuning.defaultShrineHearts < 1)
        {
            AddError(issues, ContentValidationCode.TuningInvalid, CampaignPath + ".tuning.defaultShrineHearts",
                "Default shrine hearts must be greater than zero.", campaign);
        }
    }

    private static void ValidateLearningTuning(
        CampaignConfigSO campaign,
        IssueSink issues)
    {
        if (campaign.learningTuning == null)
        {
            AddError(issues, ContentValidationCode.LearningTuningMissing,
                CampaignPath + ".learningTuning",
                "The campaign has no learning tuning asset assigned.", campaign);
        }
    }

    private static void ValidateEraTopology(
        CampaignConfigSO campaign,
        IssueSink issues)
    {
        if (campaign.eras == null || campaign.eras.Count != ContentIdentity.RevisedEraIds.Count)
        {
            AddError(issues, ContentValidationCode.EraCountInvalid, CampaignPath + ".eras",
                "Revised campaign must contain exactly three eras.", campaign);
        }

        if (campaign.eras == null)
            return;

        var seenEraIds = new HashSet<string>(StringComparer.Ordinal);
        for (int eraIndex = 0; eraIndex < campaign.eras.Count; eraIndex++)
        {
            EraConfigSO era = campaign.eras[eraIndex];
            string path = CampaignPath + ".eras[" + eraIndex + "]";
            if (era == null)
            {
                AddError(issues, ContentValidationCode.EraIdInvalid, path,
                    "Era reference is missing.", campaign);
                continue;
            }

            if (!seenEraIds.Add(era.stableId))
            {
                AddError(issues, ContentValidationCode.DuplicateId, path + ".stableId",
                    "Era stable ID is duplicated.", era);
            }

            if (IsLegacyEraIdentity(era.stableId))
            {
                AddError(issues, ContentValidationCode.LegacyEraIdentityActive, path + ".stableId",
                    "Legacy colonial-era identity cannot be active revised campaign identity.", era);
            }
            else if (!ContainsOrdinal(ContentIdentity.RevisedEraIds, era.stableId))
            {
                AddError(issues, ContentValidationCode.EraIdInvalid, path + ".stableId",
                    "Era stable ID is not one of the fixed revised era IDs.", era);
            }

            if (era.order != eraIndex + 1)
            {
                AddError(issues, ContentValidationCode.EraOrderInvalid, path + ".order",
                    "Era order must match the fixed revised campaign order.", era);
            }

            ValidateRequiredReference(issues, era.storyReference, path + ".storyReference", era);
            ValidateRequiredReference(issues, era.memoryReference, path + ".memoryReference", era);

            if (era.levels == null || era.levels.Count != ContentIdentity.RevisedLevelsPerEra)
            {
                AddError(issues, ContentValidationCode.LevelCountInvalid, path + ".levels",
                    "Each revised era must contain exactly five levels.", era);
            }
        }
    }

    private static void ValidateSymbolCatalog(
        CampaignConfigSO campaign,
        IssueSink issues)
    {
        if (campaign.symbols == null || campaign.symbols.Count != ContentIdentity.RevisedSymbolIds.Count)
        {
            AddError(issues, ContentValidationCode.SymbolCountInvalid, CampaignPath + ".symbols",
                "Revised campaign must contain exactly eighteen visual symbols.", campaign);
        }

        if (campaign.symbols == null)
            return;

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        int spokenValueCount = 0;
        int daraCount = 0;
        BaybayinCharacterSO dara = null;
        int raCount = 0;
        BaybayinCharacterSO ra = null;
        for (int symbolIndex = 0; symbolIndex < campaign.symbols.Count; symbolIndex++)
        {
            BaybayinCharacterSO symbol = campaign.symbols[symbolIndex];
            string path = CampaignPath + ".symbols[" + symbolIndex + "]";
            if (symbol == null)
            {
                AddError(issues, ContentValidationCode.SymbolIdInvalid, path,
                    "Symbol reference is missing.", campaign);
                continue;
            }

            if (!seenIds.Add(symbol.stableId))
            {
                AddError(issues, ContentValidationCode.DuplicateId, path + ".stableId",
                    "Symbol stable ID is duplicated.", symbol);
            }

            if (!ContentIdentity.IsCanonical(symbol.stableId) ||
                !ContainsOrdinal(ContentIdentity.RevisedSymbolIds, symbol.stableId))
            {
                AddError(issues, ContentValidationCode.SymbolIdInvalid, path + ".stableId",
                    "Symbol stable ID is not one of the fixed revised visual identities.", symbol);
            }

            if (!ContentIdentity.IsCanonical(symbol.firstIntroductionLevelId) ||
                !ContainsOrdinal(ContentIdentity.RevisedLevelIds, symbol.firstIntroductionLevelId))
            {
                AddError(issues, ContentValidationCode.SymbolIntroductionLevelInvalid,
                    path + ".firstIntroductionLevelId",
                    "Symbol introduction level must be one of the fixed revised level IDs.", symbol);
            }

            if (string.Equals(symbol.stableId, ContentIdentity.RevisedDaSymbolId,
                    StringComparison.Ordinal))
            {
                daraCount++;
                dara = symbol;
            }

            if (string.Equals(symbol.stableId, ContentIdentity.RevisedRaSymbolId,
                    StringComparison.Ordinal))
            {
                raCount++;
                ra = symbol;
            }

            if (symbol.spokenValues == null)
            {
                AddError(issues, ContentValidationCode.SpokenValueCountInvalid, path + ".spokenValues",
                    "Symbol spoken values cannot be null.", symbol);
                continue;
            }

            spokenValueCount += symbol.spokenValues.Count;
            ValidateSymbolValues(symbol, path, issues);
        }

        if (spokenValueCount != ContentIdentity.RevisedSpokenValueCount)
        {
            AddError(issues, ContentValidationCode.SpokenValueCountInvalid, CampaignPath + ".symbols",
                "Revised campaign must contain exactly " +
                ContentIdentity.RevisedSpokenValueCount +
                " contextual spoken values.", campaign);
        }

        // SALIN-217, rulings Q2 / OQ-6: DA and RA are two visual identities, not two readings of
        // one. D-025 renamed the id to symbol.da; it carries value.da only, and symbol.ra carries
        // value.ra. The DARA_VISUAL_IDENTITY_INVALID code name is deliberately NOT renamed: the
        // enum and its severity table are SALIN-215's file-level territory, and the code string
        // appears in validator reports that are diffed against baselines. It is now a legacy name
        // for a rule about symbol.da -- worth tidying in a ticket that owns that file, not here.
        if (daraCount != 1 || dara == null ||
            !dara.TryGetSpokenValue(ContentIdentity.RevisedDaSpokenValueId, out _) ||
            dara.TryGetSpokenValue(ContentIdentity.RevisedRaSpokenValueId, out _))
        {
            AddError(issues, ContentValidationCode.DaraVisualIdentityInvalid, CampaignPath + ".symbols",
                "symbol.da must carry value.da alone; RA is its own visual identity.",
                dara != null ? (UnityEngine.Object)dara : campaign);
        }

        if (raCount != 1 || ra == null ||
            !ra.TryGetSpokenValue(ContentIdentity.RevisedRaSpokenValueId, out _))
        {
            AddError(issues, ContentValidationCode.DaraVisualIdentityInvalid, CampaignPath + ".symbols",
                "symbol.ra must exist exactly once and carry value.ra.",
                ra != null ? (UnityEngine.Object)ra : campaign);
        }
    }

    private static void ValidateSymbolValues(
        BaybayinCharacterSO symbol,
        string path,
        IssueSink issues)
    {
        var seenValueIds = new HashSet<string>(StringComparer.Ordinal);
        for (int valueIndex = 0; valueIndex < symbol.spokenValues.Count; valueIndex++)
        {
            SpokenValueDefinition value = symbol.spokenValues[valueIndex];
            string valuePath = path + ".spokenValues[" + valueIndex + "]";
            if (value == null || !ContentIdentity.IsCanonical(value.stableId))
            {
                AddError(issues, ContentValidationCode.SpokenValueUnknown, valuePath,
                    "Spoken value must have a canonical stable ID.", symbol);
                continue;
            }

            if (!seenValueIds.Add(value.stableId))
            {
                AddError(issues, ContentValidationCode.SpokenValueCountInvalid, valuePath,
                    "Spoken value stable ID is duplicated on its visual symbol.", symbol);
            }

            // SALIN-217: symbol.da no longer gets to accept either value — it carries value.da
            // alone, and symbol.ra carries value.ra.
            // SALIN-221 deleted the local GetPrimaryValueId helper this rule used to call and moved
            // the decision into ContentIdentity.IsApprovedSpokenValue, whose narrowed
            // ApprovedSpokenValueIds map now encodes exactly the SALIN-217 rule: symbol.da maps to
            // { value.da }, and symbol.ra has no entry so the default yields value.ra.
            if (!ContentIdentity.IsApprovedSpokenValue(symbol.stableId, value.stableId))
            {
                AddError(issues, ContentValidationCode.SpokenValueUnknown, valuePath,
                    "Spoken value is not approved for its canonical visual symbol.", symbol);
            }
        }
    }

    /// <summary>
    /// docs/design/gated-finale-levels-2-4.md Section 4. Scans every level's
    /// <c>learningRequirements</c> for <see cref="ContentRequirementKind.Instruction"/> entries --
    /// the same entries <c>SymbolLearningCardController.HasPresentableRequirement</c> presents to
    /// the player as "this level teaches you X" -- and records which level(s) name each symbol.
    /// This is the authored fact; <see cref="BaybayinCharacterSO.firstIntroductionLevelId"/> is a
    /// second, unenforced restatement of it from the symbol's side, and the two can drift.
    /// </summary>
    private static Dictionary<string, List<string>> BuildSymbolIntroductionMap(CampaignConfigSO campaign)
    {
        var introducersBySymbolId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (campaign?.eras == null)
            return introducersBySymbolId;

        for (int eraIndex = 0; eraIndex < campaign.eras.Count; eraIndex++)
        {
            EraConfigSO era = campaign.eras[eraIndex];
            if (era == null || era.levels == null)
                continue;

            for (int levelIndex = 0; levelIndex < era.levels.Count; levelIndex++)
            {
                LevelConfigSO level = era.levels[levelIndex];
                if (level == null || level.learningRequirements == null ||
                    string.IsNullOrEmpty(level.stableId))
                    continue;

                for (int reqIndex = 0; reqIndex < level.learningRequirements.Count; reqIndex++)
                {
                    ContentRequirement requirement = level.learningRequirements[reqIndex];
                    if (requirement == null || requirement.kind != ContentRequirementKind.Instruction)
                        continue;

                    string symbolId = requirement.symbolValue?.symbol?.stableId;
                    if (string.IsNullOrEmpty(symbolId))
                        continue;

                    if (!introducersBySymbolId.TryGetValue(symbolId, out List<string> introducingLevelIds))
                    {
                        introducingLevelIds = new List<string>();
                        introducersBySymbolId.Add(symbolId, introducingLevelIds);
                    }

                    if (!ContainsOrdinal(introducingLevelIds, level.stableId))
                        introducingLevelIds.Add(level.stableId);
                }
            }
        }

        return introducersBySymbolId;
    }

    /// <summary>
    /// docs/design/gated-finale-levels-2-4.md Section 4, rules 1 and 2. Campaign-wide because both
    /// rules ask a question about the whole registry ("how many levels introduce this symbol",
    /// "does the symbol's own metadata point at the level that actually introduces it"), not about
    /// any single level's authored fields -- unlike rule 3 below, which is naturally a per-level
    /// check and is registered in <see cref="ValidateLevelTopology"/> instead.
    /// </summary>
    private static void ValidateSymbolIntroductionSources(
        CampaignConfigSO campaign,
        Dictionary<string, List<string>> symbolIntroducersById,
        IssueSink issues)
    {
        if (campaign.symbols == null)
            return;

        for (int symbolIndex = 0; symbolIndex < campaign.symbols.Count; symbolIndex++)
        {
            BaybayinCharacterSO symbol = campaign.symbols[symbolIndex];
            if (symbol == null || string.IsNullOrEmpty(symbol.stableId))
                continue;

            string path = CampaignPath + ".symbols[" + symbolIndex + "]";
            symbolIntroducersById.TryGetValue(symbol.stableId, out List<string> introducingLevelIds);
            int introducerCount = introducingLevelIds != null ? introducingLevelIds.Count : 0;

            if (introducerCount == 0)
            {
                AddContentIssue(issues, ContentValidationCode.SymbolIntroductionIntegrityInvalid,
                    path + ".firstIntroductionLevelId",
                    "Symbol '" + symbol.stableId + "' is introduced by no level: no level's " +
                    "learningRequirements carries an Instruction requirement naming it, so a " +
                    "player can meet it as a spawnable symbol having never been taught it.", symbol);
            }
            else if (introducerCount > 1)
            {
                AddContentIssue(issues, ContentValidationCode.SymbolIntroductionIntegrityInvalid,
                    path + ".firstIntroductionLevelId",
                    "Symbol '" + symbol.stableId + "' is introduced by more than one level: " +
                    string.Join(", ", introducingLevelIds) + ". Exactly one level's " +
                    "learningRequirements may carry an Instruction requirement for a symbol.", symbol);
            }

            bool firstIntroductionLevelAgrees = introducingLevelIds != null &&
                ContainsOrdinal(introducingLevelIds, symbol.firstIntroductionLevelId);
            if (!firstIntroductionLevelAgrees)
            {
                AddContentIssue(issues, ContentValidationCode.SymbolIntroductionIntegrityInvalid,
                    path + ".firstIntroductionLevelId",
                    "Symbol '" + symbol.stableId + "' declares firstIntroductionLevelId '" +
                    symbol.firstIntroductionLevelId + "', but no level carries an Instruction " +
                    "requirement for it there. firstIntroductionLevelId must name the level whose " +
                    "learningRequirements actually introduces this symbol.", symbol);
            }
        }
    }

    private static void ValidateLevelTopology(
        CampaignConfigSO campaign,
        Dictionary<string, List<string>> symbolIntroducersById,
        IssueSink issues)
    {
        if (campaign.eras == null)
            return;

        var seenLevelIds = new HashSet<string>(StringComparer.Ordinal);
        int globalIndex = 0;
        for (int eraIndex = 0; eraIndex < campaign.eras.Count; eraIndex++)
        {
            EraConfigSO era = campaign.eras[eraIndex];
            if (era == null || era.levels == null)
                continue;

            for (int localIndex = 0; localIndex < era.levels.Count; localIndex++)
            {
                LevelConfigSO level = era.levels[localIndex];
                string path = CampaignPath + ".eras[" + eraIndex + "].levels[" + localIndex + "]";
                if (level == null)
                {
                    AddError(issues, ContentValidationCode.LevelIdInvalid, path,
                        "Level reference is missing.", era);
                    globalIndex++;
                    continue;
                }

                if (!seenLevelIds.Add(level.stableId))
                {
                    AddError(issues, ContentValidationCode.DuplicateId, path + ".stableId",
                        "Level stable ID is duplicated.", level);
                }

                string expectedId = globalIndex < ContentIdentity.RevisedLevelIds.Count
                    ? ContentIdentity.RevisedLevelIds[globalIndex]
                    : null;
                if (!ContentIdentity.IsCanonical(level.stableId) ||
                    !string.Equals(level.stableId, expectedId, StringComparison.Ordinal))
                {
                    AddError(issues, ContentValidationCode.LevelIdInvalid, path + ".stableId",
                        "Level stable ID does not match its fixed era and global position.", level);
                }

                if (level.levelNumber != globalIndex + 1 || level.eraLocalOrder != localIndex + 1)
                {
                    AddError(issues, ContentValidationCode.LevelOrderInvalid, path,
                        "Level global and era-local order must match the fixed revised campaign order.", level);
                }

                ValidateFocusWords(campaign, level, path, issues);
                ValidateRequirements(campaign, level, path, issues);
                ValidateCumulativePool(campaign, level, globalIndex, path, issues);
                ValidateSymbolIntroductionOrder(level, globalIndex, symbolIntroducersById, path, issues);
                ValidateCombatRoster(campaign, level, globalIndex, path, issues);
                ValidateWaveCharacters(level, path, issues);
                ValidateCombatWaveRoster(level, path, issues);
                ValidateGatedFinale(level, path, issues);
                ValidateFinalRestoration(campaign, level, path, issues);
                ValidateRequiredReferences(level, path, issues);
                ValidatePaInstructionOrder(level, path, issues);
                ValidateChallengeSequence(level, path, issues);
                ValidateFlowSegments(level, path, issues);
                ValidateClueChannels(level, path, issues);
                globalIndex++;
            }
        }

        if (globalIndex != ContentIdentity.RevisedLevelIds.Count)
        {
            AddError(issues, ContentValidationCode.LevelCountInvalid, CampaignPath + ".eras",
                "Revised campaign must contain exactly fifteen globally ordered levels.", campaign);
        }
    }

    private static void ValidateChallengeSequence(
        LevelConfigSO level,
        string path,
        IssueSink issues)
    {
        if (!level.challengePrototypeEnabled)
            return;

        string challengePath = path + ".challengeSequence";
        ChallengeSequenceSO sequence = level.challengeSequence;
        if (sequence == null)
        {
            AddContentIssue(issues, ContentValidationCode.ChallengeSequenceMissing, challengePath,
                "An enabled challenge prototype requires an assigned challenge sequence.", level);
            return;
        }

        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(sequence);
        for (int index = 0; index < result.Errors.Count; index++)
        {
            string error = result.Errors[index];
            if (string.IsNullOrWhiteSpace(error))
                continue;

            AddContentIssue(issues, ContentValidationCode.ChallengeSequenceInvalid, challengePath,
                "Challenge sequence is invalid: " + error, sequence);
        }
    }

    /// <summary>
    /// SALIN-226. Checks an authored alternating-segment list against the level's own waves
    /// and challenge sequence, at the profile's content severity (Warning while authoring,
    /// Error under Strict) — never a hard Error in the Authoring profile.
    /// </summary>
    /// <remarks>
    /// Armed on Level 5 today: SALIN-283 authored real segments there, and Level 5 clears both
    /// early error branches below, so this check is already live on the current campaign. The
    /// other fourteen levels serialize an empty flowSegments list (SALIN-281) and return at the
    /// Count == 0 guard, so they arm nothing. It arms further for SALIN-273 and SALIN-280, which
    /// author the remaining segment lists this engine consumes.
    /// </remarks>
    private static void ValidateFlowSegments(
        LevelConfigSO level,
        string path,
        IssueSink issues)
    {
        if (level.flowSegments == null || level.flowSegments.Count == 0)
            return;

        string segmentsPath = path + ".flowSegments";

        if (level.challengePrototypeEnabled)
        {
            AddContentIssue(issues, ContentValidationCode.FlowSegmentsInvalid, segmentsPath,
                "A level cannot combine flow segments with challengePrototypeEnabled: the "
                + "prototype carve-out leaves ContextChallenge unplanned, so the alternating "
                + "loop would have no restoration leg to return through.", level);
            return;
        }

        if (level.challengeSequence == null)
        {
            AddContentIssue(issues, ContentValidationCode.FlowSegmentsInvalid, segmentsPath,
                "Flow segments name challenge units, so the level must assign a "
                + "challengeSequence to resolve them against.", level);
            return;
        }

        var knownUnitIds = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (ChallengeUnitDefinition unit in level.challengeSequence.units
            ?? System.Array.Empty<ChallengeUnitDefinition>())
        {
            if (unit != null && !string.IsNullOrWhiteSpace(unit.unitId))
                knownUnitIds.Add(unit.unitId);
        }

        int waveBudget = level.waves == null ? 0 : level.waves.Count;
        int consumedWaves = 0;
        bool anySegmentPlaysAUnit = false;

        for (int index = 0; index < level.flowSegments.Count; index++)
        {
            LevelFlowSegment segment = level.flowSegments[index];
            if (segment == null)
            {
                AddContentIssue(issues, ContentValidationCode.FlowSegmentsInvalid,
                    segmentsPath + "[" + index + "]", "Flow segment is null.", level);
                continue;
            }

            if (segment.waveCount < 0)
            {
                AddContentIssue(issues, ContentValidationCode.FlowSegmentsInvalid,
                    segmentsPath + "[" + index + "].waveCount",
                    "Flow segment wave count cannot be negative.", level);
            }
            else
            {
                consumedWaves += segment.waveCount;
            }

            foreach (string unitId in segment.challengeUnitIds
                ?? System.Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(unitId) || !knownUnitIds.Contains(unitId))
                {
                    AddContentIssue(issues, ContentValidationCode.FlowSegmentsInvalid,
                        segmentsPath + "[" + index + "].challengeUnitIds",
                        "Flow segment references unknown challenge unit id '" + unitId + "'.",
                        level);
                    continue;
                }

                anySegmentPlaysAUnit = true;
            }
        }

        if (consumedWaves > waveBudget)
        {
            AddContentIssue(issues, ContentValidationCode.FlowSegmentsInvalid, segmentsPath,
                "Flow segments consume " + consumedWaves + " waves but the level authors only "
                + waveBudget + ". Segments partition the wave list; they cannot overrun it.",
                level);
        }

        if (!anySegmentPlaysAUnit)
        {
            AddContentIssue(issues, ContentValidationCode.FlowSegmentsInvalid, segmentsPath,
                "No flow segment plays any challenge unit, so the level would complete with "
                + "its context challenge never played.", level);
        }
    }

    private static void ValidateClueChannels(
        LevelConfigSO level,
        string path,
        IssueSink issues)
    {
        if (!level.activeClueCombatEnabled)
            return;

        ClueChannels resolved = ClueChannelResolver.Resolve(
            level.clueChannels, level.audioVisualFallback);

        if (ClueChannelResolver.HasReadableVisual(resolved))
            return;

        AddContentIssue(issues, ContentValidationCode.ClueChannelsInvalid, path + ".clueChannels",
            "A level with active-clue combat enabled must resolve to at least one readable "
            + "visual channel so the clue stays playable when audio is unavailable.", level);
    }

    private static void ValidateFocusWords(
        CampaignConfigSO campaign,
        LevelConfigSO level,
        string path,
        IssueSink issues)
    {
        if (level.focusWords == null ||
            level.focusWords.Count != ContentIdentity.RevisedFocusWordsPerLevel)
        {
            AddContentIssue(issues, ContentValidationCode.FocusSlotCountInvalid, path + ".focusWords",
                "Each level must contain exactly two inline focus words.", level);
        }

        if (level.focusWords == null)
            return;

        var seenFocusIds = new HashSet<string>(StringComparer.Ordinal);
        for (int focusIndex = 0; focusIndex < level.focusWords.Count; focusIndex++)
        {
            FocusWordDefinition focus = level.focusWords[focusIndex];
            string focusPath = path + ".focusWords[" + focusIndex + "]";
            if (focus == null)
            {
                AddContentIssue(issues, ContentValidationCode.FocusDecompositionInvalid, focusPath,
                    "Focus word reference is missing.", level);
                continue;
            }

            if (!seenFocusIds.Add(focus.stableId))
            {
                AddContentIssue(issues, ContentValidationCode.DuplicateId, focusPath + ".stableId",
                    "Focus stable ID is duplicated within its level.", level);
            }

            string expectedId = level.stableId + ".focus." + (focusIndex + 1).ToString("00");
            if (!ContentIdentity.IsCanonical(focus.stableId) ||
                !string.Equals(focus.stableId, expectedId, StringComparison.Ordinal))
            {
                AddContentIssue(issues, ContentValidationCode.FocusDecompositionInvalid, focusPath + ".stableId",
                    "Focus stable ID must match its inline slot.", level);
            }

            if (string.IsNullOrWhiteSpace(focus.meaning))
            {
                AddContentIssue(issues, ContentValidationCode.FocusMeaningMissing, focusPath + ".meaning",
                    $"Focus word '{focus.stableId}' has no meaning.", level);
            }

            ValidateMedia(focus.media, focusPath + ".media", level, issues);
            if (focus.decomposition == null || focus.decomposition.Count == 0)
            {
                AddContentIssue(issues, ContentValidationCode.FocusDecompositionEmpty, focusPath + ".decomposition",
                    "Focus word decomposition must contain at least one symbol value.", level);
                continue;
            }

            for (int decompositionIndex = 0; decompositionIndex < focus.decomposition.Count; decompositionIndex++)
            {
                SymbolValueReference reference = focus.decomposition[decompositionIndex];
                string referencePath = focusPath + ".decomposition[" + decompositionIndex + "]";
                if (IsKudlit(reference?.spokenValueId))
                {
                    AddContentIssue(issues, ContentValidationCode.KudlitUnsupported, referencePath,
                        "Modified kudlit forms are outside the frozen core.", level);
                    continue;
                }

                if (reference?.symbol != null &&
                    campaign.TryGetSymbol(reference.symbol.stableId, out BaybayinCharacterSO referencedSymbol) &&
                    !referencedSymbol.TryGetSpokenValue(reference.spokenValueId, out _))
                {
                    AddContentIssue(issues, ContentValidationCode.SpokenValueUnknown, referencePath,
                        "Focus decomposition references an unknown spoken value.", level);
                }
                else if (!TryResolveReference(campaign, reference, out _))
                {
                    AddContentIssue(issues, ContentValidationCode.FocusDecompositionInvalid, referencePath,
                        "Focus decomposition contains an unknown symbol value.", level);
                }
                else if (!IsSymbolIntroduced(campaign, level, reference.symbol))
                {
                    AddContentIssue(issues, ContentValidationCode.SymbolNotIntroduced, referencePath,
                        "Focus decomposition references a symbol outside this level's cumulative pool.", level);
                }
            }
        }
    }

    private static void ValidateRequirements(
        CampaignConfigSO campaign,
        LevelConfigSO level,
        string path,
        IssueSink issues)
    {
        ValidateRequirementList(campaign, level, level.learningRequirements,
            path + ".learningRequirements", issues);
        ValidateRequirementList(campaign, level, level.practiceRequirements,
            path + ".practiceRequirements", issues);
        ValidateRequirementList(campaign, level, level.masteryRequirements,
            path + ".masteryRequirements", issues);
    }

    private static void ValidateRequirementList(
        CampaignConfigSO campaign,
        LevelConfigSO level,
        List<ContentRequirement> requirements,
        string path,
        IssueSink issues)
    {
        if (requirements == null || requirements.Count == 0)
        {
            AddContentIssue(issues, ContentValidationCode.RequirementInvalid, path,
                "Required content requirements are missing.");
            return;
        }

        for (int index = 0; index < requirements.Count; index++)
        {
            ContentRequirement requirement = requirements[index];
            string requirementPath = path + "[" + index + "]";
            if (requirement == null || requirement.requiredSuccesses < 1 ||
                !TryResolveReference(campaign, requirement.symbolValue, out _))
            {
                AddContentIssue(issues, ContentValidationCode.RequirementInvalid, requirementPath,
                    "Content requirement must have a positive count and known symbol value.");
            }
            else if (!IsSymbolIntroduced(campaign, level, requirement.symbolValue.symbol))
            {
                AddContentIssue(issues, ContentValidationCode.SymbolNotIntroduced, requirementPath,
                    "Content requirement references a symbol outside this level's cumulative pool.", level);
            }
        }
    }

    private static void ValidateCumulativePool(
        CampaignConfigSO campaign,
        LevelConfigSO level,
        int globalIndex,
        string path,
        IssueSink issues)
    {
        HashSet<string> expected = ExpectedPoolSymbolIds(campaign, globalIndex);

        var actual = new HashSet<string>(StringComparer.Ordinal);
        if (level.cumulativeSymbolPool != null)
        {
            for (int index = 0; index < level.cumulativeSymbolPool.Count; index++)
            {
                SymbolValueReference reference = level.cumulativeSymbolPool[index];
                if (TryResolveReference(campaign, reference, out _))
                    actual.Add(reference.symbol.stableId);
            }
        }

        if (level.cumulativeSymbolPool == null ||
            level.cumulativeSymbolPool.Count != expected.Count ||
            actual.Count != expected.Count ||
            !actual.SetEquals(expected))
        {
            AddContentIssue(issues, ContentValidationCode.CumulativePoolInvalid, path + ".cumulativeSymbolPool",
                "Cumulative symbol pool does not match the symbols introduced through this level.", level);
        }
    }

    /// <summary>
    /// docs/design/gated-finale-levels-2-4.md Section 4, rule 3 -- "the property that actually
    /// protects the player". Checks each pool entry against <paramref name="symbolIntroducersById"/>
    /// (the authored Instruction data built once by <see cref="BuildSymbolIntroductionMap"/>) rather
    /// than against <c>firstIntroductionLevelId</c>, so this stays correct even when that metadata
    /// has drifted from the authored requirements -- see
    /// <see cref="ValidateSymbolIntroductionSources"/>, which reports the drift itself. A symbol
    /// with no recorded introducer at all fails here on every level whose pool carries it, exactly
    /// like the known Char_RA gap: firstIntroductionLevelId claims level.pamana.03, but no level's
    /// learningRequirements actually introduces RA, so it fails this check on levels 13-15 too.
    /// </summary>
    private static void ValidateSymbolIntroductionOrder(
        LevelConfigSO level,
        int globalIndex,
        Dictionary<string, List<string>> symbolIntroducersById,
        string path,
        IssueSink issues)
    {
        if (level.cumulativeSymbolPool == null)
            return;

        for (int index = 0; index < level.cumulativeSymbolPool.Count; index++)
        {
            SymbolValueReference reference = level.cumulativeSymbolPool[index];
            BaybayinCharacterSO symbol = reference?.symbol;
            if (symbol == null || string.IsNullOrEmpty(symbol.stableId))
                continue;

            symbolIntroducersById.TryGetValue(symbol.stableId, out List<string> introducingLevelIds);

            bool introducedInTime = false;
            if (introducingLevelIds != null)
            {
                for (int levelIdIndex = 0; levelIdIndex < introducingLevelIds.Count; levelIdIndex++)
                {
                    int introducingGlobalIndex = IndexOfOrdinal(
                        ContentIdentity.RevisedLevelIds, introducingLevelIds[levelIdIndex]);
                    if (introducingGlobalIndex >= 0 && introducingGlobalIndex <= globalIndex)
                    {
                        introducedInTime = true;
                        break;
                    }
                }
            }

            if (!introducedInTime)
            {
                AddContentIssue(issues, ContentValidationCode.SymbolIntroductionIntegrityInvalid,
                    path + ".cumulativeSymbolPool[" + index + "]",
                    "Cumulative symbol pool includes '" + symbol.stableId + "', but no level at or " +
                    "before this one carries an Instruction requirement introducing it, so a player " +
                    "could meet it as a spawnable symbol having never been taught it.", level);
            }
        }
    }

    /// <summary>
    /// The symbols a level may legitimately use: every catalog symbol introduced at or before it.
    /// Derived from symbol metadata, never from the level's own authored fields.
    /// </summary>
    private static HashSet<string> ExpectedPoolSymbolIds(CampaignConfigSO campaign, int globalIndex)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        if (campaign?.symbols == null)
            return expected;

        for (int symbolIndex = 0; symbolIndex < campaign.symbols.Count; symbolIndex++)
        {
            BaybayinCharacterSO symbol = campaign.symbols[symbolIndex];
            int introductionIndex = IndexOfOrdinal(
                ContentIdentity.RevisedLevelIds, symbol?.firstIntroductionLevelId);
            if (introductionIndex >= 0 && introductionIndex <= globalIndex && symbol != null)
                expected.Add(symbol.stableId);
        }

        return expected;
    }

    /// <summary>
    /// SALIN-204: the combat roster gates which glyphs waves may demand
    /// (LevelConfigSO.PruneToRoster). It must match the symbols the player has been taught by
    /// this level, or the level asks for glyphs it never introduced. The validator previously
    /// never read this field, so Ugat Levels 2-5 shipped rosters drawn from later eras.
    /// </summary>
    private static void ValidateCombatRoster(
        CampaignConfigSO campaign,
        LevelConfigSO level,
        int globalIndex,
        string path,
        IssueSink issues)
    {
        HashSet<string> expected = ExpectedPoolSymbolIds(campaign, globalIndex);
        var actual = new HashSet<string>(StringComparer.Ordinal);

        if (level.allowedCharacters != null)
        {
            for (int index = 0; index < level.allowedCharacters.Count; index++)
            {
                BaybayinCharacterSO symbol = level.allowedCharacters[index];
                if (symbol != null && !string.IsNullOrEmpty(symbol.stableId))
                    actual.Add(symbol.stableId);
            }
        }

        if (actual.SetEquals(expected))
            return;

        var untaught = new List<string>(actual);
        untaught.RemoveAll(expected.Contains);
        untaught.Sort(StringComparer.Ordinal);

        string detail = untaught.Count > 0
            ? "Combat roster includes symbols this level has not taught: " +
              string.Join(", ", untaught) + "."
            : "Combat roster does not cover every symbol introduced through this level.";

        AddContentIssue(issues, ContentValidationCode.CombatRosterInvalid, path + ".allowedCharacters",
            detail, level);
    }

    private static bool IsSymbolIntroduced(
        CampaignConfigSO campaign,
        LevelConfigSO level,
        BaybayinCharacterSO symbol)
    {
        if (campaign == null || level?.cumulativeSymbolPool == null || symbol == null)
            return false;

        for (int index = 0; index < level.cumulativeSymbolPool.Count; index++)
        {
            SymbolValueReference reference = level.cumulativeSymbolPool[index];
            if (TryResolveReference(campaign, reference, out _) && reference.symbol == symbol)
                return true;
        }

        return false;
    }

    private static void ValidateFinalRestoration(
        CampaignConfigSO campaign,
        LevelConfigSO level,
        string path,
        IssueSink issues)
    {
        SymbolValueReference reference = level.finalRestorationValue;
        if (!TryResolveReference(campaign, reference, out _))
        {
            AddContentIssue(issues, ContentValidationCode.FinalRestorationInvalid, path + ".finalRestorationValue",
                "Final restoration must reference a known symbol value.", level);
            return;
        }

        if (string.Equals(level.stableId, ContentIdentity.RevisedFinaleLevelId,
                StringComparison.Ordinal) &&
            (!string.Equals(reference.symbol.stableId, ContentIdentity.RevisedFinaleSymbolId,
                 StringComparison.Ordinal) ||
             !string.Equals(reference.spokenValueId, ContentIdentity.RevisedFinaleSpokenValueId,
                 StringComparison.Ordinal)))
        {
            AddContentIssue(issues, ContentValidationCode.FinalRestorationInvalid, path + ".finalRestorationValue",
                "The revised campaign finale must restore symbol.ya/value.ya.", level);
        }
    }

    private static void ValidateRequiredReferences(
        LevelConfigSO level,
        string path,
        IssueSink issues)
    {
        ValidateMedia(level.contextMedia, path + ".contextMedia", level, issues);
        if (level.defenseRules == null || level.defenseRules.shrineHearts < 1)
        {
            AddContentIssue(issues, ContentValidationCode.RequiredReferenceMissing, path + ".defenseRules",
                "Defense rules are required.", level);
        }

        if (level.rewardIds == null || level.rewardIds.Count == 0 ||
            level.rewardIds.Exists(string.IsNullOrWhiteSpace))
        {
            AddContentIssue(issues, ContentValidationCode.RequiredReferenceMissing, path + ".rewardIds",
                "At least one reward reference is required.", level);
        }
    }

    private static void ValidatePaInstructionOrder(
        LevelConfigSO level,
        string path,
        IssueSink issues)
    {
        if (!string.Equals(level.stableId, ContentIdentity.RevisedFinaleLevelId,
                StringComparison.Ordinal))
            return;

        bool hasPaInstructionBeforeLearningExposure =
            ContainsPaInstructionBeforeExposure(level.learningRequirements);
        bool hasPaLaterExposure = ContainsPaExposure(level.practiceRequirements) ||
                                  ContainsPaExposure(level.masteryRequirements) ||
                                  ContainsPaExposure(level.focusWords);
        if (!hasPaInstructionBeforeLearningExposure || !hasPaLaterExposure)
        {
            AddContentIssue(issues, ContentValidationCode.PaInstructionOrderInvalid,
                path + ".learningRequirements", "PA instruction must precede PA practice or assessment content.", level);
        }
    }

    private static bool ContainsPaInstructionBeforeExposure(List<ContentRequirement> requirements)
    {
        if (requirements == null)
            return false;

        for (int i = 0; i < requirements.Count; i++)
        {
            ContentRequirement requirement = requirements[i];
            if (requirement == null || !IsPa(requirement.symbolValue))
                continue;

            return requirement.kind == ContentRequirementKind.Instruction;
        }

        return false;
    }

    private static bool ContainsPaExposure(List<ContentRequirement> requirements)
    {
        if (requirements == null)
            return false;

        for (int i = 0; i < requirements.Count; i++)
        {
            ContentRequirement requirement = requirements[i];
            if (requirement != null &&
                (requirement.kind == ContentRequirementKind.Practice ||
                 requirement.kind == ContentRequirementKind.Assessment ||
                 requirement.kind == ContentRequirementKind.Mastery) &&
                IsPa(requirement.symbolValue))
                return true;
        }

        return false;
    }

    private static bool ContainsPaExposure(List<FocusWordDefinition> focusWords)
    {
        if (focusWords == null)
            return false;

        for (int wordIndex = 0; wordIndex < focusWords.Count; wordIndex++)
        {
            FocusWordDefinition focusWord = focusWords[wordIndex];
            if (focusWord?.decomposition == null)
                continue;

            for (int decompositionIndex = 0; decompositionIndex < focusWord.decomposition.Count; decompositionIndex++)
            {
                if (IsPa(focusWord.decomposition[decompositionIndex]))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// SALIN-217: this read RevisedFinaleSymbolId, which was correct only while PA happened to be
    /// the finale. Ruling Q1 moved the finale to YA, and ValidatePaInstructionOrder must still mean
    /// PA, so PA is now named directly. Without this the PA-ordering rule would have silently
    /// become a YA-ordering rule with no test failing.
    /// </summary>
    private static bool IsPa(SymbolValueReference reference)
    {
        return reference?.symbol != null &&
               string.Equals(reference.symbol.stableId, ContentIdentity.RevisedPaSymbolId,
                   StringComparison.Ordinal) &&
               string.Equals(reference.spokenValueId, ContentIdentity.RevisedPaSpokenValueId,
                   StringComparison.Ordinal);
    }

    private static bool TryResolveReference(
        CampaignConfigSO campaign,
        SymbolValueReference reference,
        out SpokenValueDefinition value)
    {
        value = null;
        if (reference == null || reference.symbol == null ||
            string.IsNullOrWhiteSpace(reference.symbol.stableId))
            return false;

        if (!campaign.TryGetSymbol(reference.symbol.stableId, out BaybayinCharacterSO symbol) ||
            symbol != reference.symbol)
            return false;

        return symbol.TryGetSpokenValue(reference.spokenValueId, out value);
    }

    private static void ValidateMedia(
        ContentMediaReferences media,
        string path,
        UnityEngine.Object context,
        IssueSink issues)
    {
        if (media == null)
        {
            AddContentIssue(issues, ContentValidationCode.RequiredMediaMissing, path,
                "Required content media references are missing.", context);
            return;
        }

        if (media.contextImage == null || media.narrationClip == null)
        {
            AddContentIssue(issues, ContentValidationCode.RequiredMediaMissing, path,
                "Required context image and narration media are missing.", context);
        }

        if (media.dialogue == null || media.cutscene == null)
        {
            AddContentIssue(issues, ContentValidationCode.RequiredReferenceMissing, path,
                "Required dialogue and cutscene references are missing.", context);
        }
    }

    private static void ValidateRequiredReference(
        IssueSink issues,
        UnityEngine.Object reference,
        string path,
        UnityEngine.Object context)
    {
        if (reference == null)
        {
            AddContentIssue(issues, ContentValidationCode.RequiredReferenceMissing, path,
                "Required content reference is missing.", context);
        }
    }

    private static bool IsLegacyEraIdentity(string stableId)
    {
        return string.Equals(stableId, "Spanish", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(stableId, "American", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(stableId, "Japanese", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKudlit(string spokenValueId)
    {
        return spokenValueId != null && spokenValueId.IndexOf(".kudlit.", StringComparison.Ordinal) >= 0;
    }

    private static bool ContainsOrdinal(IReadOnlyList<string> values, string value)
    {
        if (values == null)
            return false;

        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static int IndexOfOrdinal(IReadOnlyList<string> values, string value)
    {
        if (values == null)
            return -1;

        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Ugat QA 2026-09-16: a level that restores its focus text through combat has to be able to
    /// spawn every symbol that text needs, in every wave. A wave that narrows its own roster
    /// starves the spawn director — once the narrowed symbol is restored the filler pool has
    /// nothing else to draw, so the marked enemy carries a glyph no open slot wants, and a needed
    /// symbol with no matching enemy in the wave spawns on a body that contradicts its badge.
    /// An empty wave list is the healthy shape: it means "carry the whole level roster", so only a
    /// non-empty, narrowed list is reported here. (A curve-driven level passes on merit rather
    /// than by that exemption - WaveCurveExpander copies the full roster into every wave.)
    /// </summary>
    private static void ValidateCombatWaveRoster(
        LevelConfigSO level,
        string path,
        IssueSink issues)
    {
        if (!level.activeClueCombatEnabled || level.waves == null || level.focusWords == null)
            return;

        var requiredSymbolIds = new List<string>();
        var seenSymbolIds = new HashSet<string>(StringComparer.Ordinal);
        for (int focusIndex = 0; focusIndex < level.focusWords.Count; focusIndex++)
        {
            FocusWordDefinition focus = level.focusWords[focusIndex];
            if (focus?.decomposition == null)
                continue;

            for (int index = 0; index < focus.decomposition.Count; index++)
            {
                BaybayinCharacterSO symbol = focus.decomposition[index]?.symbol;
                if (symbol == null || string.IsNullOrEmpty(symbol.stableId))
                    continue;

                if (seenSymbolIds.Add(symbol.stableId))
                    requiredSymbolIds.Add(symbol.stableId);
            }
        }

        if (requiredSymbolIds.Count == 0)
            return;

        for (int waveIndex = 0; waveIndex < level.waves.Count; waveIndex++)
        {
            WaveDefinition wave = level.waves[waveIndex];
            if (wave == null || wave.isIntermissionWave)
                continue;

            string wavePath = path + ".waves[" + waveIndex + "]";

            if (wave.characters != null && wave.characters.Count > 0)
            {
                var missing = new List<string>();
                for (int index = 0; index < requiredSymbolIds.Count; index++)
                {
                    if (FindCharacterId(wave.characters, requiredSymbolIds[index]) == null)
                        missing.Add(requiredSymbolIds[index]);
                }

                if (missing.Count > 0)
                {
                    AddContentIssue(issues, ContentValidationCode.WaveRosterNarrowsRestoration,
                        wavePath + ".characters",
                        "A combat-restoration wave that lists characters must carry every symbol its "
                        + "focus text needs, or the spawn director starves once the listed symbols are "
                        + "restored. Leave the list empty to inherit the level roster. Missing: "
                        + string.Join(", ", missing) + ".", level);
                }
            }

            if (wave.enemyTypes == null || wave.enemyTypes.Count == 0)
                continue;

            var unrepresented = new List<string>();
            for (int index = 0; index < requiredSymbolIds.Count; index++)
            {
                if (!WaveCarriesEnemyFor(wave.enemyTypes, requiredSymbolIds[index]))
                    unrepresented.Add(requiredSymbolIds[index]);
            }

            if (unrepresented.Count > 0)
            {
                AddContentIssue(issues, ContentValidationCode.WaveRosterNarrowsRestoration,
                    wavePath + ".enemyTypes",
                    "A combat-restoration wave that lists enemy types must include an enemy whose "
                    + "assignedCharacter covers each symbol its focus text needs, or a needed symbol "
                    + "spawns on a body that contradicts its badge. Leave the list empty to inherit "
                    + "the level roster. Unrepresented: " + string.Join(", ", unrepresented) + ".", level);
            }
        }
    }

    /// <summary>
    /// A level that withholds its final slot until the final wave needs at least two slots, at
    /// least one wave, and at least one symbol that occurs exactly once across its flattened slots.
    /// With one slot the gate withholds the sole win condition; with no waves the token never
    /// opens; with no uniquely-occurring symbol there is nothing to withhold, because restoration
    /// is by symbol (see <see cref="DerivedFinaleGate"/>) and another slot's carrier always fills
    /// the gated one for free. The first two are unwinnable levels, the third a silent no-op that
    /// reads as a shipped feature. All three fail at author time.
    /// </summary>
    private static void ValidateGatedFinale(
        LevelConfigSO level,
        string path,
        IssueSink issues)
    {
        SpawnAssignmentPolicy policy = level.spawnAssignmentPolicy;
        if (policy == null || !policy.gateFinalSlotToFinalWave)
            return;

        var symbolStableIds = new List<string>();
        if (level.focusWords != null)
        {
            for (int focusIndex = 0; focusIndex < level.focusWords.Count; focusIndex++)
            {
                FocusWordDefinition focus = level.focusWords[focusIndex];
                if (focus?.decomposition == null)
                    continue;

                for (int index = 0; index < focus.decomposition.Count; index++)
                {
                    BaybayinCharacterSO symbol = focus.decomposition[index]?.symbol;
                    if (symbol != null)
                        symbolStableIds.Add(symbol.stableId);
                }
            }
        }

        int slotCount = symbolStableIds.Count;

        if (slotCount >= 2 &&
            DerivedFinaleGate.LastUniquelyOccurringIndex(symbolStableIds) == DerivedFinaleGate.NoSlot)
        {
            AddContentIssue(issues, ContentValidationCode.GatedFinaleUnwinnable,
                path + ".spawnAssignmentPolicy.gateFinalSlotToFinalWave",
                "This level withholds its final slot until the final wave but every symbol in its "
                + "focus words occurs more than once. Restoration is by symbol, so whichever slot "
                + "the gate lands on is filled for free by another slot's carrier: the gate "
                + "withholds nothing and the level completes before its final wave exactly as if "
                + "the option were off. Give the level a syllable that appears exactly once, or "
                + "turn gateFinalSlotToFinalWave off.", level);
        }

        if (slotCount < 2)
        {
            AddContentIssue(issues, ContentValidationCode.GatedFinaleUnwinnable,
                path + ".spawnAssignmentPolicy.gateFinalSlotToFinalWave",
                "This level withholds its final slot until the final wave but has "
                + slotCount + " slot(s). Gating the only slot withholds the level's sole win "
                + "condition, so it could never be completed.", level);
        }

        int waveCount = level.waves != null ? level.waves.Count : 0;
        if (waveCount < 1)
        {
            AddContentIssue(issues, ContentValidationCode.GatedFinaleUnwinnable,
                path + ".spawnAssignmentPolicy.gateFinalSlotToFinalWave",
                "This level withholds its final slot until the final wave but authors no waves, "
                + "so the gate would never open.", level);
        }
    }

    private static BaybayinCharacterSO FindCharacterId(
        List<BaybayinCharacterSO> candidates,
        string symbolStableId)
    {
        if (candidates == null)
            return null;

        for (int index = 0; index < candidates.Count; index++)
        {
            if (candidates[index] != null && candidates[index].stableId == symbolStableId)
                return candidates[index];
        }

        return null;
    }

    private static bool WaveCarriesEnemyFor(List<EnemyDataSO> enemyTypes, string symbolStableId)
    {
        for (int index = 0; index < enemyTypes.Count; index++)
        {
            EnemyDataSO enemyData = enemyTypes[index];
            if (enemyData != null &&
                enemyData.assignedCharacter != null &&
                enemyData.assignedCharacter.stableId == symbolStableId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// SALIN-204: a wave with no characters falls back to each enemy type's assignedCharacter
    /// (WaveSpawner.SelectCharacterForSpawn). Colonial enemy data ships without one, so such a
    /// wave spawns enemies that carry no glyph and cannot be defeated. Level 4 waves 2 and 3
    /// shipped that way and were unwinnable while the campaign validated clean. Boss levels
    /// have no waves and are skipped; intermission waves spawn nothing.
    /// </summary>
    private static void ValidateWaveCharacters(
        LevelConfigSO level,
        string path,
        IssueSink issues)
    {
        if (level.waves == null)
            return;

        for (int waveIndex = 0; waveIndex < level.waves.Count; waveIndex++)
        {
            WaveDefinition wave = level.waves[waveIndex];
            if (wave == null || wave.isIntermissionWave)
                continue;

            bool hasCharacter = false;
            if (wave.characters != null)
            {
                for (int index = 0; index < wave.characters.Count && !hasCharacter; index++)
                    hasCharacter = wave.characters[index] != null;
            }

            if (hasCharacter)
                continue;

            var glyphless = new List<string>();
            if (wave.enemyTypes != null)
            {
                for (int index = 0; index < wave.enemyTypes.Count; index++)
                {
                    EnemyDataSO enemyData = wave.enemyTypes[index];
                    if (enemyData != null && enemyData.assignedCharacter == null)
                        glyphless.Add(enemyData.name);
                }
            }

            if (glyphless.Count == 0)
                continue;

            AddContentIssue(issues, ContentValidationCode.WaveCharactersUnresolvable,
                path + ".waves[" + waveIndex + "].characters",
                "Wave has no characters and these enemy types have no default assignedCharacter, " +
                "so they would spawn with no glyph and could not be defeated: " +
                string.Join(", ", glyphless) + ".", level);
        }
    }

    /// <summary>
    /// SALIN-215: an identity issue — manifest, era/level/symbol ids and counts, ordering, the
    /// DA/RA rule, and the validator's own internal failure. Always an Error, in every profile:
    /// these mean the campaign is structurally wrong, not merely unfinished.
    /// </summary>
    private static void AddError(
        IssueSink issues,
        string code,
        string path,
        string message,
        UnityEngine.Object context = null)
    {
        issues.Add(ContentValidationSeverity.Error, code, path, message, context);
    }

    /// <summary>
    /// SALIN-215: a content-completeness issue — media, focus words, requirements, pools, rosters,
    /// final value, challenge sequence, reward ids. Warning while authoring, Error under the strict
    /// release profile. Classified per call site, never by code: DUPLICATE_ID and
    /// SPOKEN_VALUE_UNKNOWN are each emitted from both categories.
    /// </summary>
    private static void AddContentIssue(
        IssueSink issues,
        string code,
        string path,
        string message,
        UnityEngine.Object context = null)
    {
        issues.Add(issues.ContentSeverity, code, path, message, context);
    }
}
