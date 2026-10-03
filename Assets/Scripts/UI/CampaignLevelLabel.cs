/// <summary>
/// ============================================================================
/// SALIN-258 — PLACEHOLDER PLAYER-FACING COPY. NOT PRODUCT-APPROVED.
/// ============================================================================
/// The one place that turns a level's era identity into the string a player reads.
///
/// THE RULING. docs/design/spec-rulings-2026-09.md (row "How are levels numbered for
/// the player?", Jon Wayne Cabusbusan, 2026-09-11, SALIN-258/T60): levels are presented
/// as "Era N, Level 1 to 5" everywhere, NEVER as a global 1 to 15.
///
/// WHY THIS FORM. The ruling sanctions two renderings — "Ugat Level 2" or "Era 1 Level 2"
/// — and does not pick one. "{eraName} Level {n}" is chosen because it is ALREADY SHIPPING:
/// CampaignSaveNoticeCopy.cs:71 reads "The revised journey begins at Ugat Level 1." and
/// :87 reads "Ugat Level 1. The earlier files were kept for diagnostics." Choosing the
/// other form would leave the save-recovery copy inconsistent with every other surface.
/// This is a selection between two sanctioned options, not an invention.
///
/// WHERE THE ERA NAME COMES FROM — READ THIS BEFORE CHANGING THE CALLERS.
/// The era name MUST be sourced from EraConfigSO.eraName (Assets/Scripts/Data/EraConfigSO.cs:14).
/// It must NOT be sourced from LevelConfigSO.chapterName (LevelConfigSO.cs:12), which looks
/// like the era axis and is correctly authored on all 15 level configs, but has ZERO
/// production readers — its only references in the tree are its own declaration and two
/// assertions in Assets/Tests/Editor/Salin185CampaignNamingTests.cs:33 and :51. Binding the
/// UI to it would compile, pass, and render correctly today while silently depending on a
/// dead field that no validator keeps in sync.
///
/// LIKEWISE, eraLocalOrder IS READ, NEVER RECOMPUTED. Callers pass the authored
/// LevelConfigSO.eraLocalOrder. Deriving it as ((globalNumber - 1) % 5 + 1) would bypass the
/// invariant CampaignConfigValidator.cs:389 enforces between levelNumber and eraLocalOrder,
/// and would silently produce wrong labels the moment an era is not exactly five levels.
///
/// LANGUAGE: English. The project splits by role — UI chrome is English, narrative content
/// is Filipino (the dialogue assets). The era NAMES themselves (Ugat, Ugnayan, Pamana) are
/// authored Filipino read verbatim from EraConfigSO; no narrative text is drafted here.
///
/// ACTION REQUIRED: product/content review of the wording before release.
/// Approval follow-up: SALIN-291.
/// ============================================================================
/// </summary>
public static class CampaignLevelLabel
{
    /// <summary>{0} = era display name, {1} = 1-based order within that era.</summary>
    public const string EraLevelFormat = "{0} Level {1}";

    /// <summary>
    /// Legacy/degraded form, used only when the era is unknown. The legacy progress path
    /// genuinely cannot name an era — LevelSelectUI.FindEraForLevel returns no era for a
    /// level that is not in the campaign — and a blank label there would take the whole
    /// notice off screen. A plain number is worse copy than an era-relative one but it is
    /// still true, so the copy degrades rather than disappearing.
    /// </summary>
    public const string FallbackLevelFormat = "Level {0}";

    /// <summary>
    /// The player-facing label for a level, or <see cref="string.Empty"/> when there is
    /// nothing to name. Empty is a meaningful result: every caller treats an empty label as
    /// "stay silent", which is what preserves the existing "no prerequisite to explain"
    /// behaviour on Level Select.
    /// </summary>
    /// <param name="eraName">
    /// EraConfigSO.eraName. Null or empty degrades to <see cref="FallbackLevelFormat"/>.
    /// </param>
    /// <param name="eraLocalOrder">
    /// The authored LevelConfigSO.eraLocalOrder, 1-5. Below 1 there is nothing to name.
    /// </param>
    public static string Format(string eraName, int eraLocalOrder)
    {
        if (eraLocalOrder < 1)
            return string.Empty;

        if (string.IsNullOrEmpty(eraName))
            return string.Format(FallbackLevelFormat, eraLocalOrder);

        return string.Format(EraLevelFormat, eraName, eraLocalOrder);
    }

    /// <summary>
    /// The resilient overload every UI call site should prefer. Uses the era-relative form
    /// whenever the era is known, and only then falls back to the global number.
    ///
    /// The fallback policy lives HERE rather than at each call site on purpose: duplicating
    /// "use the global number when the era lookup failed" across callers is exactly how a
    /// global 1-15 leaks back onto a screen after this ticket closes.
    /// </summary>
    /// <param name="eraName">EraConfigSO.eraName, or null when the era could not be resolved.</param>
    /// <param name="eraLocalOrder">Authored eraLocalOrder, or 0 when the era could not be resolved.</param>
    /// <param name="globalLevelNumber">
    /// ProgressManager's global 1-15 id. Used ONLY to keep the copy non-empty on the legacy
    /// path; it is never shown when the era is known.
    /// </param>
    public static string Format(string eraName, int eraLocalOrder, int globalLevelNumber)
    {
        if (eraLocalOrder >= 1 && !string.IsNullOrEmpty(eraName))
            return Format(eraName, eraLocalOrder);

        return Format(null, globalLevelNumber);
    }
}
