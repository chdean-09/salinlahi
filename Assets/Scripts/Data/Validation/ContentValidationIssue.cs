using UnityEngine;

public enum ContentValidationSeverity
{
    Warning,
    Error,
}

/// <summary>
/// SALIN-215: selects how strictly content-completeness issues are reported. Identity issues are
/// Errors in every profile and are not affected by this.
/// </summary>
public enum ContentValidationProfile
{
    /// <summary>
    /// Default. Content-completeness issues are Warnings, so unfinished content does not refuse
    /// the campaign while it is still being authored.
    /// </summary>
    Authoring,

    /// <summary>
    /// Release profile. Content-completeness issues are Errors again, for gating at
    /// content-complete. Nothing binds a build to this yet — see SALIN-215 Escalation 1.
    /// </summary>
    Strict,
}

public static class ContentValidationCode
{
    public const string ManifestMissing = "MANIFEST_MISSING";
    public const string ManifestUnsupported = "MANIFEST_UNSUPPORTED";
    public const string WorkbookHashMismatch = "WORKBOOK_HASH_MISMATCH";
    public const string CampaignIdInvalid = "CAMPAIGN_ID_INVALID";
    public const string EraCountInvalid = "ERA_COUNT_INVALID";
    public const string EraIdInvalid = "ERA_ID_INVALID";
    public const string EraOrderInvalid = "ERA_ORDER_INVALID";
    public const string LevelCountInvalid = "LEVEL_COUNT_INVALID";
    public const string LevelIdInvalid = "LEVEL_ID_INVALID";
    public const string LevelOrderInvalid = "LEVEL_ORDER_INVALID";
    public const string FocusSlotCountInvalid = "FOCUS_SLOT_COUNT_INVALID";
    public const string DuplicateId = "DUPLICATE_ID";
    public const string SymbolCountInvalid = "SYMBOL_COUNT_INVALID";
    public const string SymbolIdInvalid = "SYMBOL_ID_INVALID";
    public const string SymbolIntroductionLevelInvalid = "SYMBOL_INTRODUCTION_LEVEL_INVALID";
    public const string SymbolNotIntroduced = "SYMBOL_NOT_INTRODUCED";
    public const string SpokenValueCountInvalid = "SPOKEN_VALUE_COUNT_INVALID";
    public const string SpokenValueUnknown = "SPOKEN_VALUE_UNKNOWN";
    public const string DaraVisualIdentityInvalid = "DARA_VISUAL_IDENTITY_INVALID";
    public const string FocusDecompositionEmpty = "FOCUS_DECOMPOSITION_EMPTY";
    public const string FocusMeaningMissing = "FOCUS_MEANING_MISSING";
    public const string FocusDecompositionInvalid = "FOCUS_DECOMPOSITION_INVALID";
    public const string KudlitUnsupported = "KUDLIT_UNSUPPORTED";
    public const string CumulativePoolInvalid = "CUMULATIVE_POOL_INVALID";
    public const string CombatRosterInvalid = "COMBAT_ROSTER_INVALID";
    public const string WaveCharactersUnresolvable = "WAVE_CHARACTERS_UNRESOLVABLE";
    public const string WaveRosterNarrowsRestoration = "WAVE_ROSTER_NARROWS_RESTORATION";
    public const string GatedFinaleUnwinnable = "GATED_FINALE_UNWINNABLE";
    public const string FinalRestorationInvalid = "FINAL_RESTORATION_INVALID";
    public const string PaInstructionOrderInvalid = "PA_INSTRUCTION_ORDER_INVALID";
    public const string RequiredMediaMissing = "REQUIRED_MEDIA_MISSING";
    public const string RequiredReferenceMissing = "REQUIRED_REFERENCE_MISSING";
    public const string LegacyEraIdentityActive = "LEGACY_ERA_IDENTITY_ACTIVE";
    public const string TuningInvalid = "TUNING_INVALID";
    public const string LearningTuningMissing = "LEARNING_TUNING_MISSING";
    public const string RequirementInvalid = "REQUIREMENT_INVALID";
    public const string ChallengeSequenceMissing = "CHALLENGE_SEQUENCE_MISSING";
    public const string ChallengeSequenceInvalid = "CHALLENGE_SEQUENCE_INVALID";
    public const string ChallengeModeEraProgressionInvalid = "CHALLENGE_MODE_ERA_PROGRESSION_INVALID";
    public const string ClueChannelsInvalid = "CLUE_CHANNELS_INVALID";
    public const string FlowSegmentsInvalid = "FLOW_SEGMENTS_INVALID";
    public const string RestorationObjectiveInvalid = "RESTORATION_OBJECTIVE_INVALID";
    public const string RestorationOccurrenceUnreachable = "RESTORATION_OCCURRENCE_UNREACHABLE";
    public const string SymbolIntroductionIntegrityInvalid = "SYMBOL_INTRODUCTION_INTEGRITY_INVALID";
    public const string ValidatorInternalError = "VALIDATOR_INTERNAL_ERROR";
}

public sealed class ContentValidationIssue
{
    public ContentValidationSeverity Severity { get; }
    public string Code { get; }
    public string Path { get; }
    public string Message { get; }
    public Object Context { get; }

    public ContentValidationIssue(
        ContentValidationSeverity severity,
        string code,
        string path,
        string message,
        Object context = null)
    {
        Severity = severity;
        Code = code;
        Path = path;
        Message = message;
        Context = context;
    }
}
