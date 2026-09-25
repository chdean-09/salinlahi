using System.Collections.Generic;

/// <summary>
/// SALIN-240. One focus word as the memory card presents it: the Latin label, its
/// approved meaning, and the Baybayin symbols it decomposes into.
///
/// The symbols are held as object references, never as stable ids. D-025 is renaming
/// symbol.dara -> symbol.da on a concurrent branch; a hard-coded id here would rot the
/// moment that lands, whereas the object reference is a GUID the rename does not touch.
/// </summary>
public sealed class MemoryArchiveWord
{
    public MemoryArchiveWord(string label, string meaning, IReadOnlyList<BaybayinCharacterSO> symbols)
    {
        Label = label;
        Meaning = meaning;
        Symbols = symbols;
    }

    public string Label { get; }
    public string Meaning { get; }

    /// <summary>Decomposition symbols, in authored order. Each carries the glyphOutlineSprite.</summary>
    public IReadOnlyList<BaybayinCharacterSO> Symbols { get; }
}

/// <summary>
/// SALIN-240. One archive slot. There is exactly one per level in the campaign, whether or
/// not that level grants a memory — see <see cref="MemoryArchiveModel"/> for why.
/// </summary>
public sealed class MemoryArchiveEntry
{
    public MemoryArchiveEntry(
        string eraName,
        int eraOrder,
        int eraLocalOrder,
        int levelNumber,
        string levelStableId,
        string memoryId,
        bool isUnlocked,
        string title,
        IReadOnlyList<MemoryArchiveWord> words,
        string lore)
    {
        EraName = eraName;
        EraOrder = eraOrder;
        EraLocalOrder = eraLocalOrder;
        LevelNumber = levelNumber;
        LevelStableId = levelStableId;
        MemoryId = memoryId;
        IsUnlocked = isUnlocked;
        Title = title;
        Words = words;
        Lore = lore;
    }

    public string EraName { get; }
    public int EraOrder { get; }

    /// <summary>1-5 within the era. This is the collectible number shown on the card (AC-6).</summary>
    public int EraLocalOrder { get; }

    /// <summary>Global 1-15. Used only by the "Earn in Level n" label — see MemoryCardCopy.</summary>
    public int LevelNumber { get; }

    public string LevelStableId { get; }

    /// <summary>The granted memory id, or null when this level grants no memory.</summary>
    public string MemoryId { get; }

    public bool IsUnlocked { get; }
    public string Title { get; }
    public IReadOnlyList<MemoryArchiveWord> Words { get; }

    /// <summary>The restored-memory cutscene's panel text, joined. Filipino, verbatim.</summary>
    public string Lore { get; }

    /// <summary>
    /// False for Levels 6-15, which carry rewardIds: [] and no memory cutscene. That is
    /// D-015 (Ugat complete and polished, Levels 6-15 present but flagged incomplete), not
    /// a defect, and it renders as a locked silhouette.
    /// </summary>
    public bool HasAuthoredContent => MemoryId != null && !string.IsNullOrEmpty(Lore);
}

/// <summary>
/// SALIN-240. Derives the Memory Archive from content that is already authored.
///
/// WHY THERE IS NO MemoryCardSO. Every field the card needs already exists in shipped
/// assets: the collectible number is LevelConfigSO.eraLocalOrder, the title is
/// LevelConfigSO.levelName (authored Filipino - "Ang Unang Tinig"), the words and meanings
/// are focusWords[*].displayLabel/.meaning, the Baybayin forms are reached through
/// focusWords[*].decomposition[*].symbol.glyphOutlineSprite, and the lore is the
/// restored-memory cutscene's panel text. Introducing a ScriptableObject to hold copies of
/// all that would fork the source of truth for narrative text. This derives instead.
///
/// WHY THE ENUMERATION IS PER-LEVEL, NOT PER-MEMORY-ID. Levels 6-15 have rewardIds: [], so
/// there is no memory id to key a slot on -- keying on ids would render an archive with
/// five entries and silently drop the other ten. AC-8 requires a slot for every level 2-15
/// reading "Earn in Level n". Enumerating levels is the only shape that satisfies AC-8
/// while D-015 holds.
///
/// Pure and static by design: no MonoBehaviour, no SaveManager, no scene. The UI on top of
/// it stays thin, and EditMode tests exercise the whole derivation with in-memory
/// ScriptableObject.CreateInstance fixtures.
/// </summary>
public static class MemoryArchiveModel
{
    /// <summary>Separator between the joined cutscene panels on the card back.</summary>
    public const string LoreParagraphSeparator = "\n\n";

    /// <summary>
    /// Builds every archive slot, ordered by era then by position within the era.
    ///
    /// A null or empty <paramref name="unlockedMemoryIds"/> is the fresh-save case and is
    /// valid: every slot comes back locked. Callers must never treat that as an error --
    /// the archive has to render on a save that has unlocked nothing.
    /// </summary>
    public static IReadOnlyList<MemoryArchiveEntry> Build(
        CampaignConfigSO campaign,
        IReadOnlyCollection<string> unlockedMemoryIds)
    {
        var entries = new List<MemoryArchiveEntry>();
        if (campaign == null || campaign.eras == null)
            return entries;

        var eras = new List<EraConfigSO>();
        for (int i = 0; i < campaign.eras.Count; i++)
            if (campaign.eras[i] != null)
                eras.Add(campaign.eras[i]);

        // Stable sort on the authored era order. List.Sort is unstable, so ties would
        // reorder arbitrarily between runs and make the archive non-deterministic.
        eras = StableSortByEraOrder(eras);

        foreach (EraConfigSO era in eras)
        {
            if (era.levels == null)
                continue;

            var levels = new List<LevelConfigSO>();
            for (int i = 0; i < era.levels.Count; i++)
                if (era.levels[i] != null)
                    levels.Add(era.levels[i]);

            levels = StableSortByEraLocalOrder(levels);

            foreach (LevelConfigSO level in levels)
                entries.Add(BuildEntry(era, level, unlockedMemoryIds));
        }

        return entries;
    }

    /// <summary>
    /// SALIN-253. Builds every archive slot for ONE era, in that era's local order.
    ///
    /// WHY THIS LIVES HERE. Both existing consumers hand-roll their own era scoping —
    /// MemoryArchiveController.RenderRows re-groups the whole-campaign list by era name, and
    /// LevelFlowController.ShowMemoryCard reaches for era.levels.Count directly. The era
    /// completion screen needs the same slice a third time, and a third hand-rolled copy is how
    /// the three drift apart. This is the era filter that did not exist; it reuses
    /// <see cref="BuildEntry"/> and the same stable sort <see cref="Build"/> uses, so an entry
    /// built here and the same entry built by Build are identical by construction.
    ///
    /// A null or empty <paramref name="unlockedMemoryIds"/> is the fresh-save case and is
    /// valid: every slot comes back locked. A null era returns an empty list rather than
    /// throwing — the caller is on the results path, where an exception would take the Results
    /// screen down with it.
    /// </summary>
    public static IReadOnlyList<MemoryArchiveEntry> BuildForEra(
        EraConfigSO era,
        IReadOnlyCollection<string> unlockedMemoryIds)
    {
        var entries = new List<MemoryArchiveEntry>();
        if (era == null || era.levels == null)
            return entries;

        var levels = new List<LevelConfigSO>();
        for (int i = 0; i < era.levels.Count; i++)
            if (era.levels[i] != null)
                levels.Add(era.levels[i]);

        // Stable sort on the authored eraLocalOrder. List.Sort is unstable, so ties would
        // reorder arbitrarily between runs and make the era screen non-deterministic.
        levels = StableSortByEraLocalOrder(levels);

        foreach (LevelConfigSO level in levels)
            entries.Add(BuildEntry(era, level, unlockedMemoryIds));

        return entries;
    }

    /// <summary>
    /// Builds the slot for one level. Exposed so the claim flow can build a single card
    /// without walking the whole campaign.
    /// </summary>
    public static MemoryArchiveEntry BuildEntry(
        EraConfigSO era,
        LevelConfigSO level,
        IReadOnlyCollection<string> unlockedMemoryIds)
    {
        if (level == null)
            return null;

        string memoryId = ResolveMemoryId(level);
        bool isUnlocked = memoryId != null
            && unlockedMemoryIds != null
            && Contains(unlockedMemoryIds, memoryId);

        return new MemoryArchiveEntry(
            era != null ? era.eraName : null,
            era != null ? era.order : 0,
            level.eraLocalOrder,
            level.levelNumber,
            level.stableId,
            memoryId,
            isUnlocked,
            level.levelName,
            ResolveWords(level),
            ResolveLore(level));
    }

    /// <summary>
    /// The first rewardIds entry that is a memory grant, or null.
    ///
    /// The prefix is <see cref="LevelRewardResolver.MemoryRewardPrefix"/> rather than a
    /// restated "memory." literal, so this and the grant path cannot drift apart. Reward
    /// ids that are not memory grants are ignored, not misread as one.
    /// </summary>
    public static string ResolveMemoryId(LevelConfigSO level)
    {
        if (level == null || level.rewardIds == null)
            return null;

        for (int i = 0; i < level.rewardIds.Count; i++)
        {
            string rewardId = level.rewardIds[i];
            if (string.IsNullOrWhiteSpace(rewardId))
                continue;
            if (rewardId.StartsWith(LevelRewardResolver.MemoryRewardPrefix, System.StringComparison.Ordinal))
                return rewardId;
        }

        return null;
    }

    private static IReadOnlyList<MemoryArchiveWord> ResolveWords(LevelConfigSO level)
    {
        var words = new List<MemoryArchiveWord>();
        if (level.focusWords == null)
            return words;

        foreach (FocusWordDefinition focusWord in level.focusWords)
        {
            if (focusWord == null)
                continue;

            var symbols = new List<BaybayinCharacterSO>();
            if (focusWord.decomposition != null)
            {
                foreach (SymbolValueReference reference in focusWord.decomposition)
                {
                    // Unity's fake-null: a destroyed or unassigned asset reference is not a
                    // plain null, so only the overloaded == null comparison is reliable.
                    if (reference == null || reference.symbol == null)
                        continue;
                    symbols.Add(reference.symbol);
                }
            }

            words.Add(new MemoryArchiveWord(focusWord.displayLabel, focusWord.meaning, symbols));
        }

        return words;
    }

    private static string ResolveLore(LevelConfigSO level)
    {
        CutsceneSO cutscene = level.contextMedia != null ? level.contextMedia.cutscene : null;
        if (cutscene == null || cutscene.panels == null)
            return string.Empty;

        var paragraphs = new List<string>();
        for (int i = 0; i < cutscene.panels.Length; i++)
        {
            string text = cutscene.panels[i].text;
            if (!string.IsNullOrWhiteSpace(text))
                paragraphs.Add(text.Trim());
        }

        return string.Join(LoreParagraphSeparator, paragraphs);
    }

    private static bool Contains(IReadOnlyCollection<string> values, string value)
    {
        foreach (string candidate in values)
            if (string.Equals(candidate, value, System.StringComparison.Ordinal))
                return true;
        return false;
    }

    private static List<EraConfigSO> StableSortByEraOrder(List<EraConfigSO> source)
    {
        var result = new List<EraConfigSO>(source);
        for (int i = 1; i < result.Count; i++)
        {
            EraConfigSO current = result[i];
            int j = i - 1;
            while (j >= 0 && result[j].order > current.order)
            {
                result[j + 1] = result[j];
                j--;
            }
            result[j + 1] = current;
        }
        return result;
    }

    private static List<LevelConfigSO> StableSortByEraLocalOrder(List<LevelConfigSO> source)
    {
        var result = new List<LevelConfigSO>(source);
        for (int i = 1; i < result.Count; i++)
        {
            LevelConfigSO current = result[i];
            int j = i - 1;
            while (j >= 0 && result[j].eraLocalOrder > current.eraLocalOrder)
            {
                result[j + 1] = result[j];
                j--;
            }
            result[j + 1] = current;
        }
        return result;
    }
}
