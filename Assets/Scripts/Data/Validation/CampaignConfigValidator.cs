using System;
using System.Collections.Generic;
using UnityEngine;

public static class CampaignConfigValidator
{
    private const string CampaignPath = "campaign.revised-v1";

    public static IReadOnlyList<ContentValidationIssue> Validate(CampaignConfigSO campaign)
    {
        var issues = new List<ContentValidationIssue>();
        try
        {
            if (campaign == null)
            {
                AddError(issues, ContentValidationCode.ManifestMissing, CampaignPath,
                    "Campaign root is missing.");
                return issues.AsReadOnly();
            }

            ValidateManifest(campaign, issues);
            ValidateTuning(campaign, issues);
            ValidateEraTopology(campaign, issues);
            ValidateSymbolCatalog(campaign, issues);
            ValidateLevelTopology(campaign, issues);
        }
        catch (Exception exception)
        {
            AddError(issues, ContentValidationCode.ValidatorInternalError, CampaignPath,
                "Validation failed internally: " + exception, campaign);
        }

        return issues.AsReadOnly();
    }

    private static void ValidateManifest(
        CampaignConfigSO campaign,
        List<ContentValidationIssue> issues)
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
        List<ContentValidationIssue> issues)
    {
        if (campaign.tuning == null || campaign.tuning.defaultShrineHearts < 1)
        {
            AddError(issues, ContentValidationCode.TuningInvalid, CampaignPath + ".tuning.defaultShrineHearts",
                "Default shrine hearts must be greater than zero.", campaign);
        }
    }

    private static void ValidateEraTopology(
        CampaignConfigSO campaign,
        List<ContentValidationIssue> issues)
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
        List<ContentValidationIssue> issues)
    {
        if (campaign.symbols == null || campaign.symbols.Count != ContentIdentity.RevisedSymbolIds.Count)
        {
            AddError(issues, ContentValidationCode.SymbolCountInvalid, CampaignPath + ".symbols",
                "Revised campaign must contain exactly seventeen visual symbols.", campaign);
        }

        if (campaign.symbols == null)
            return;

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        int spokenValueCount = 0;
        int daraCount = 0;
        BaybayinCharacterSO dara = null;
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

            if (string.Equals(symbol.stableId, ContentIdentity.RevisedDaraSymbolId,
                    StringComparison.Ordinal))
            {
                daraCount++;
                dara = symbol;
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
                "Revised campaign must contain exactly eighteen contextual spoken values.", campaign);
        }

        if (daraCount != 1 || dara == null ||
            !dara.TryGetSpokenValue(ContentIdentity.RevisedDaSpokenValueId, out _) ||
            !dara.TryGetSpokenValue(ContentIdentity.RevisedRaSpokenValueId, out _))
        {
            AddError(issues, ContentValidationCode.DaraVisualIdentityInvalid, CampaignPath + ".symbols",
                "DA and RA must be contextual values on one symbol.dara visual identity.",
                dara != null ? (UnityEngine.Object)dara : campaign);
        }
    }

    private static void ValidateSymbolValues(
        BaybayinCharacterSO symbol,
        string path,
        List<ContentValidationIssue> issues)
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

            bool knownValue = string.Equals(
                    symbol.stableId, ContentIdentity.RevisedDaraSymbolId, StringComparison.Ordinal)
                ? string.Equals(value.stableId, ContentIdentity.RevisedDaSpokenValueId,
                      StringComparison.Ordinal) ||
                  string.Equals(value.stableId, ContentIdentity.RevisedRaSpokenValueId,
                      StringComparison.Ordinal)
                : string.Equals(value.stableId, GetPrimaryValueId(symbol.stableId), StringComparison.Ordinal);
            if (!knownValue)
            {
                AddError(issues, ContentValidationCode.SpokenValueUnknown, valuePath,
                    "Spoken value is not approved for its canonical visual symbol.", symbol);
            }
        }
    }

    private static void ValidateLevelTopology(
        CampaignConfigSO campaign,
        List<ContentValidationIssue> issues)
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
                ValidateFinalRestoration(campaign, level, path, issues);
                ValidateRequiredReferences(level, path, issues);
                ValidatePaInstructionOrder(level, path, issues);
                ValidateChallengeSequence(level, path, issues);
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
        List<ContentValidationIssue> issues)
    {
        if (!level.challengePrototypeEnabled)
            return;

        string challengePath = path + ".challengeSequence";
        ChallengeSequenceSO sequence = level.challengeSequence;
        if (sequence == null)
        {
            AddError(issues, ContentValidationCode.ChallengeSequenceMissing, challengePath,
                "An enabled challenge prototype requires an assigned challenge sequence.", level);
            return;
        }

        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(sequence);
        for (int index = 0; index < result.Errors.Count; index++)
        {
            string error = result.Errors[index];
            if (string.IsNullOrWhiteSpace(error))
                continue;

            AddError(issues, ContentValidationCode.ChallengeSequenceInvalid, challengePath,
                "Challenge sequence is invalid: " + error, sequence);
        }
    }

    private static void ValidateFocusWords(
        CampaignConfigSO campaign,
        LevelConfigSO level,
        string path,
        List<ContentValidationIssue> issues)
    {
        if (level.focusWords == null ||
            level.focusWords.Count != ContentIdentity.RevisedFocusWordsPerLevel)
        {
            AddError(issues, ContentValidationCode.FocusSlotCountInvalid, path + ".focusWords",
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
                AddError(issues, ContentValidationCode.FocusDecompositionInvalid, focusPath,
                    "Focus word reference is missing.", level);
                continue;
            }

            if (!seenFocusIds.Add(focus.stableId))
            {
                AddError(issues, ContentValidationCode.DuplicateId, focusPath + ".stableId",
                    "Focus stable ID is duplicated within its level.", level);
            }

            string expectedId = level.stableId + ".focus." + (focusIndex + 1).ToString("00");
            if (!ContentIdentity.IsCanonical(focus.stableId) ||
                !string.Equals(focus.stableId, expectedId, StringComparison.Ordinal))
            {
                AddError(issues, ContentValidationCode.FocusDecompositionInvalid, focusPath + ".stableId",
                    "Focus stable ID must match its inline slot.", level);
            }

            ValidateMedia(focus.media, focusPath + ".media", level, issues);
            if (focus.decomposition == null || focus.decomposition.Count == 0)
            {
                AddError(issues, ContentValidationCode.FocusDecompositionEmpty, focusPath + ".decomposition",
                    "Focus word decomposition must contain at least one symbol value.", level);
                continue;
            }

            for (int decompositionIndex = 0; decompositionIndex < focus.decomposition.Count; decompositionIndex++)
            {
                SymbolValueReference reference = focus.decomposition[decompositionIndex];
                string referencePath = focusPath + ".decomposition[" + decompositionIndex + "]";
                if (IsKudlit(reference?.spokenValueId))
                {
                    AddError(issues, ContentValidationCode.KudlitUnsupported, referencePath,
                        "Modified kudlit forms are outside the frozen core.", level);
                    continue;
                }

                if (reference?.symbol != null &&
                    campaign.TryGetSymbol(reference.symbol.stableId, out BaybayinCharacterSO referencedSymbol) &&
                    !referencedSymbol.TryGetSpokenValue(reference.spokenValueId, out _))
                {
                    AddError(issues, ContentValidationCode.SpokenValueUnknown, referencePath,
                        "Focus decomposition references an unknown spoken value.", level);
                }
                else if (!TryResolveReference(campaign, reference, out _))
                {
                    AddError(issues, ContentValidationCode.FocusDecompositionInvalid, referencePath,
                        "Focus decomposition contains an unknown symbol value.", level);
                }
                else if (!IsSymbolIntroduced(campaign, level, reference.symbol))
                {
                    AddError(issues, ContentValidationCode.SymbolNotIntroduced, referencePath,
                        "Focus decomposition references a symbol outside this level's cumulative pool.", level);
                }
            }
        }
    }

    private static void ValidateRequirements(
        CampaignConfigSO campaign,
        LevelConfigSO level,
        string path,
        List<ContentValidationIssue> issues)
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
        List<ContentValidationIssue> issues)
    {
        if (requirements == null || requirements.Count == 0)
        {
            AddError(issues, ContentValidationCode.RequirementInvalid, path,
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
                AddError(issues, ContentValidationCode.RequirementInvalid, requirementPath,
                    "Content requirement must have a positive count and known symbol value.");
            }
            else if (!IsSymbolIntroduced(campaign, level, requirement.symbolValue.symbol))
            {
                AddError(issues, ContentValidationCode.SymbolNotIntroduced, requirementPath,
                    "Content requirement references a symbol outside this level's cumulative pool.", level);
            }
        }
    }

    private static void ValidateCumulativePool(
        CampaignConfigSO campaign,
        LevelConfigSO level,
        int globalIndex,
        string path,
        List<ContentValidationIssue> issues)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        if (campaign.symbols != null)
        {
            for (int symbolIndex = 0; symbolIndex < campaign.symbols.Count; symbolIndex++)
            {
                BaybayinCharacterSO symbol = campaign.symbols[symbolIndex];
                int introductionIndex = IndexOfOrdinal(
                    ContentIdentity.RevisedLevelIds, symbol?.firstIntroductionLevelId);
                if (introductionIndex >= 0 && introductionIndex <= globalIndex && symbol != null)
                    expected.Add(symbol.stableId);
            }
        }

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
            AddError(issues, ContentValidationCode.CumulativePoolInvalid, path + ".cumulativeSymbolPool",
                "Cumulative symbol pool does not match the symbols introduced through this level.", level);
        }
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
        List<ContentValidationIssue> issues)
    {
        SymbolValueReference reference = level.finalRestorationValue;
        if (!TryResolveReference(campaign, reference, out _))
        {
            AddError(issues, ContentValidationCode.FinalRestorationInvalid, path + ".finalRestorationValue",
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
            AddError(issues, ContentValidationCode.FinalRestorationInvalid, path + ".finalRestorationValue",
                "The revised campaign finale must restore symbol.pa/value.pa.", level);
        }
    }

    private static void ValidateRequiredReferences(
        LevelConfigSO level,
        string path,
        List<ContentValidationIssue> issues)
    {
        ValidateMedia(level.contextMedia, path + ".contextMedia", level, issues);
        if (level.defenseRules == null || level.defenseRules.shrineHearts < 1)
        {
            AddError(issues, ContentValidationCode.RequiredReferenceMissing, path + ".defenseRules",
                "Defense rules are required.", level);
        }

        if (level.rewardIds == null || level.rewardIds.Count == 0 ||
            level.rewardIds.Exists(string.IsNullOrWhiteSpace))
        {
            AddError(issues, ContentValidationCode.RequiredReferenceMissing, path + ".rewardIds",
                "At least one reward reference is required.", level);
        }
    }

    private static void ValidatePaInstructionOrder(
        LevelConfigSO level,
        string path,
        List<ContentValidationIssue> issues)
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
            AddError(issues, ContentValidationCode.PaInstructionOrderInvalid,
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

    private static bool IsPa(SymbolValueReference reference)
    {
        return reference?.symbol != null &&
               string.Equals(reference.symbol.stableId, ContentIdentity.RevisedFinaleSymbolId,
                   StringComparison.Ordinal) &&
               string.Equals(reference.spokenValueId, ContentIdentity.RevisedFinaleSpokenValueId,
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
        List<ContentValidationIssue> issues)
    {
        if (media == null)
        {
            AddError(issues, ContentValidationCode.RequiredMediaMissing, path,
                "Required content media references are missing.", context);
            return;
        }

        if (media.contextImage == null || media.narrationClip == null)
        {
            AddError(issues, ContentValidationCode.RequiredMediaMissing, path,
                "Required context image and narration media are missing.", context);
        }

        if (media.dialogue == null || media.cutscene == null)
        {
            AddError(issues, ContentValidationCode.RequiredReferenceMissing, path,
                "Required dialogue and cutscene references are missing.", context);
        }
    }

    private static void ValidateRequiredReference(
        List<ContentValidationIssue> issues,
        UnityEngine.Object reference,
        string path,
        UnityEngine.Object context)
    {
        if (reference == null)
        {
            AddError(issues, ContentValidationCode.RequiredReferenceMissing, path,
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

    private static string GetPrimaryValueId(string symbolId)
    {
        if (string.IsNullOrEmpty(symbolId) ||
            !symbolId.StartsWith("symbol.", StringComparison.Ordinal))
            return "value.invalid";

        return symbolId == ContentIdentity.RevisedDaraSymbolId
            ? ContentIdentity.RevisedDaSpokenValueId
            : "value." + symbolId.Substring("symbol.".Length);
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

    private static void AddError(
        List<ContentValidationIssue> issues,
        string code,
        string path,
        string message,
        UnityEngine.Object context = null)
    {
        issues.Add(new ContentValidationIssue(
            ContentValidationSeverity.Error, code, path, message, context));
    }
}
