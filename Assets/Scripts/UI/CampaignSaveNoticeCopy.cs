/// <summary>
/// ============================================================================
/// SALIN-272 — PLACEHOLDER PLAYER-FACING COPY. NOT PRODUCT-APPROVED.
/// ============================================================================
/// Every string the campaign save-notice overlay can show lives here, and nowhere
/// else, so product/content can rewrite the wording without touching flow logic.
/// Mirrors <see cref="LevelLockNoticeCopy"/> (SALIN-137), which shipped under the
/// same banner.
///
/// WHY THE STRINGS MOVED: they were inline in <see cref="CampaignSaveNoticePanel"/>
/// since SALIN-171 (commit 0131818c) and had never been reviewed as copy, because
/// there was nothing to review — no copy deck anywhere under docs/, and no approval
/// note on SALIN-272 or its SALIN-219 parent. Extracting them makes the approval gap
/// visible instead of implicit.
///
/// LANGUAGE: English. The project splits by role -- UI chrome is English (this class,
/// LevelLockNoticeCopy, LevelContentMissingPanel), narrative content is Filipino
/// (Assets/ScriptableObjects/Dialogue/*). That split was already in force; it is
/// followed here rather than re-decided.
///
/// KNOWN LIMIT — the safe-reset wording is deliberately true of BOTH arms.
/// A superseded save and a corrupt save reach this notice by different routes but
/// arrive indistinguishable: CampaignSaveService.cs:157 writes reasonCode
/// "safe-reset" for both, and the "superseded-schema" code (CampaignSaveService.cs:28)
/// reaches only the quarantine filename, never the receipt. So however carefully the
/// copy is written, the panel cannot tell the player which of the two happened.
/// <see cref="SafeResetBody"/> is therefore worded so it is not a lie in either case.
/// Splitting the two requires a change to CampaignSaveService, which SALIN-272 puts
/// out of scope; a follow-up ticket to carry the superseded reason code into the
/// recovery receipt is pending with the ticket owner.
///
/// ACTION REQUIRED: product/content review of the wording before release. The
/// language split above is settled; the sentences themselves are not sacred.
/// Approval follow-up ticket is pending with the owner and was not yet keyed when
/// this shipped — SALIN-272 AC4 is satisfied by this record, not by approval.
/// ============================================================================
/// </summary>
public static class CampaignSaveNoticeCopy
{
    /// <summary>Confirm-button label when the notice is informational and dismissible.</summary>
    /// <remarks>
    /// Matches the shipped register: MainMenuUI uses "Continue" for the Play button
    /// (MainMenuUI.cs:58) and LevelLockNoticeCopy.DismissLabel is "OK".
    /// </remarks>
    public const string ContinueLabel = "Continue";

    /// <summary>Confirm-button label when the notice is blocking and the action retries init.</summary>
    public const string RetryLabel = "Retry";

    /// <summary>Title for <see cref="CampaignSaveNoticeKind.Migration"/>. Unchanged since SALIN-171.</summary>
    public const string MigrationTitle = "Your Journey Has Been Updated";

    /// <summary>
    /// Title for <see cref="CampaignSaveNoticeKind.Recovery"/>.
    ///
    /// CHANGED by SALIN-272. The shipped title was "Journey Save Recovered", which is a
    /// false claim: nothing is recovered on this path. CampaignSaveService.cs:152 calls
    /// CampaignProgressFactory.CreateClean, which builds a brand-new document with every
    /// level locked but the first (CampaignProgressFactory.cs:38-47); the stored progress
    /// is not read back at all. The receipt the same branch writes is literally a reset:
    /// reasonCode "safe-reset" (CampaignSaveService.cs:157).
    /// </summary>
    public const string SafeResetTitle = "Your Journey Was Reset";

    /// <summary>Title for <see cref="CampaignSaveNoticeKind.Blocking"/> and the default arm.</summary>
    public const string BlockedTitle = "Journey Data Cannot Be Opened";

    /// <summary>Body for <see cref="CampaignSaveNoticeKind.Migration"/>. Unchanged since SALIN-171.</summary>
    public const string MigrationBody =
        "Your previous journey progress was archived safely. Audio preferences were preserved. " +
        "The revised journey begins at Ugat Level 1.";

    /// <summary>
    /// Body for <see cref="CampaignSaveNoticeKind.Recovery"/>.
    ///
    /// CHANGED by SALIN-272, three grounded parts:
    /// "at Ugat Level 1" is where the clean journey actually starts — CreateClean seeds
    /// activeLevelId from manifest.startingLevelId (CampaignProgressFactory.cs:31), which is
    /// "level.ugat.01" (CampaignConfig_RevisedV1.asset:26) — and it reuses the phrasing the
    /// shipped <see cref="MigrationBody"/> already uses for the same destination;
    /// "kept for diagnostics" retains the shipped clause verbatim;
    /// "could not be carried over" replaces "could not be recovered" so the sentence is true of
    /// the superseded arm as well as the corrupt one. See the KNOWN LIMIT note on this class.
    /// </summary>
    public const string SafeResetBody =
        "Your previous journey could not be carried over, so a clean journey was created at " +
        "Ugat Level 1. The earlier files were kept for diagnostics.";

    /// <summary>Body when the save was written by a newer build. Unchanged since SALIN-171.</summary>
    public const string UnsupportedSchemaBody =
        "This journey was created by a newer version of Salinlahi. Update the game to continue. " +
        "Progress was not changed.";

    /// <summary>Body when the save files could not be read at all. Unchanged since SALIN-171.</summary>
    public const string BlockedIoBody =
        "Journey files could not be read. Check device storage and try again. " +
        "Progress was not changed.";

    /// <summary>Body for a blocking notice with no more specific reason. Unchanged since SALIN-171.</summary>
    public const string BlockedDefaultBody =
        "The revised journey content is incomplete or incompatible. Progress was not changed.";

    /// <summary>Reason codes <see cref="Body"/> recognises on the blocking arm.</summary>
    /// <remarks>
    /// Both spellings of each code are matched because the producer is not settled: the
    /// persistence layer writes kebab-case, while older callers passed the enum name.
    /// Carried over verbatim from the pre-extraction panel rather than narrowed here.
    /// </remarks>
    public const string UnsupportedSchemaReason = "UnsupportedSchema";
    public const string UnsupportedSchemaReasonKebab = "unsupported-schema";
    public const string BlockedIoReason = "BlockedIo";
    public const string BlockedIoReasonKebab = "io-failure";

    /// <summary>Title for a notice, by kind.</summary>
    public static string Title(CampaignSaveNoticeKind kind)
    {
        if (kind == CampaignSaveNoticeKind.Migration) return MigrationTitle;
        if (kind == CampaignSaveNoticeKind.Recovery) return SafeResetTitle;
        return BlockedTitle;
    }

    /// <summary>Body for a notice, by kind and — on the blocking arm only — reason code.</summary>
    public static string Body(CampaignSaveNoticeKind kind, string reasonCode)
    {
        if (kind == CampaignSaveNoticeKind.Migration) return MigrationBody;
        if (kind == CampaignSaveNoticeKind.Recovery) return SafeResetBody;
        if (reasonCode == UnsupportedSchemaReason || reasonCode == UnsupportedSchemaReasonKebab)
            return UnsupportedSchemaBody;
        if (reasonCode == BlockedIoReason || reasonCode == BlockedIoReasonKebab)
            return BlockedIoBody;
        return BlockedDefaultBody;
    }

    /// <summary>Confirm-button label for a notice, by kind.</summary>
    public static string ConfirmLabel(CampaignSaveNoticeKind kind) =>
        kind == CampaignSaveNoticeKind.Blocking ? RetryLabel : ContinueLabel;
}
