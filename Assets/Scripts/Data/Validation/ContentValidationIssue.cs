using UnityEngine;

public enum ContentValidationSeverity
{
    Warning,
    Error,
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
    public const string ClueChannelsInvalid = "CLUE_CHANNELS_INVALID";
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
