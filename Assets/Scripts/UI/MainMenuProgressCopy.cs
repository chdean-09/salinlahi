/// <summary>
/// ============================================================================
/// SALIN-256 — PLACEHOLDER PLAYER-FACING COPY. NOT PRODUCT-APPROVED.
/// ============================================================================
/// Every new player-facing string this ticket adds, isolated in one small file so the
/// approval surface is a single review, following the pattern CampaignLevelLabel.cs:1-38
/// established for SALIN-258.
///
/// LANGUAGE: English. The project splits by role — UI chrome is English, narrative content
/// is Filipino (the dialogue assets). Every string here is chrome. The era NAMES that appear
/// inside the progress line (Ugat, Ugnayan, Pamana) are authored Filipino read verbatim from
/// EraConfigSO.eraName; no narrative text is drafted here.
///
/// WHY THE SEPARATOR IS NOT INVENTED. The acceptance criterion writes the progress line as
/// "Ugat Level 3 13%" and does not specify a separator. "  ·  " is the separator ALREADY
/// SHIPPING in MemoryArchiveController.LockedRowLabel (MemoryArchiveController.cs:201-203).
/// Reusing it is a selection between the forms already in the product, not an invention.
///
/// ACTION REQUIRED: product/content review of the wording before release.
/// Approval follow-up: SALIN-291.
/// ============================================================================
/// </summary>
public static class MainMenuProgressCopy
{
    /// <summary>
    /// {0} = the era-relative level label from CampaignLevelLabel (e.g. "Ugat Level 3"),
    /// {1} = whole-number completion percentage.
    /// </summary>
    public const string ProgressFormat = "{0}  ·  {1}%";

    /// <summary>
    /// Rendered when there is nothing truthful to say — no campaign, or a level the campaign
    /// cannot name. Empty means "render nothing", the same contract CampaignLevelLabel.cs:55-58
    /// establishes for an unnameable level: staying silent beats showing a half-true line.
    /// </summary>
    public const string ProgressUnavailable = "";

    public const string ExitConfirmTitle = "Exit Salinlahi?";

    public const string ExitConfirmBody =
        "Your progress is saved as you play, so you can pick your journey back up "
        + "the next time you open Salinlahi.";

    public const string ExitConfirmButtonLabel = "Exit";

    public const string ExitCancelButtonLabel = "Cancel";
}
