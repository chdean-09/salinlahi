using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-198: idempotent authoring of the revised campaign content layer.
/// Creates/updates the campaign root, era shells, and the fully-authored Level 1
/// (INA/AMA) configuration in place — existing assets keep their GUIDs so every
/// scene and wave reference survives. Levels 2-15 receive identity skeletons
/// only; their content remains on SALIN-172/204/205. Word meanings and the
/// provisional OU introduction level are review inputs for SALIN-188.
/// </summary>
public static class RevisedCampaignBootstrap
{
    public const string CampaignAssetPath =
        "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

    private const string CharacterFolder = "Assets/ScriptableObjects/Characters";
    private const string LevelFolder = "Assets/ScriptableObjects/Levels";
    private const string ThemeFolder = "Assets/ScriptableObjects/Themes";
    private const string ChallengeAssetPath =
        "Assets/ScriptableObjects/Challenges/Challenge_Ugat01_Context.asset";
    private const string LearningTuningPath = "Assets/ScriptableObjects/LearningTuning.asset";

    // Visual symbol -> (character asset, introduction level). SALIN-217 (ruling Q2,
    // reaffirmed by OQ-6): DA and RA are two taught identities, so Char_RA is a full
    // member of the revised catalog, introduced at level.pamana.03 per ruling R8, and
    // symbol.dara carries value.da alone. symbol.dara keeps its name because renaming
    // it is a save migration owned by SALIN-227. OU is introduced at
    // level.ugnayan.04 (OO/UNA), confirmed against the approved workbook matrix
    // under SALIN-204; it was previously level.ugat.04, which put OU in the Ugat
    // Levels 4-5 pools even though no Ugat focus word uses it.
    private static readonly (string SymbolId, string AssetName, string IntroLevelId)[] SymbolMap =
    {
        ("symbol.a", "Char_A", "level.ugat.01"),
        ("symbol.ei", "Char_EI", "level.ugat.01"),
        ("symbol.ba", "Char_BA", "level.ugat.02"),
        ("symbol.ma", "Char_MA", "level.ugat.01"),
        ("symbol.na", "Char_NA", "level.ugat.01"),
        ("symbol.ta", "Char_TA", "level.ugat.02"),
        ("symbol.ou", "Char_OU", "level.ugnayan.04"),
        ("symbol.ka", "Char_KA", "level.ugnayan.02"),
        ("symbol.ga", "Char_GA", "level.ugnayan.01"),
        ("symbol.sa", "Char_SA", "level.ugnayan.02"),
        ("symbol.wa", "Char_WA", "level.ugnayan.01"),
        ("symbol.ya", "Char_YA", "level.ugnayan.03"),
        ("symbol.dara", "Char_DA", "level.pamana.01"),
        ("symbol.ha", "Char_HA", "level.pamana.02"),
        ("symbol.la", "Char_LA", "level.pamana.01"),
        ("symbol.nga", "Char_NGA", "level.pamana.02"),
        ("symbol.ra", "Char_RA", "level.pamana.03"),
        ("symbol.pa", "Char_PA", "level.pamana.05"),
    };

    [MenuItem("Salinlahi/Campaign/Bootstrap Revised Campaign (Level 1)")]
    public static void Run()
    {
        // Level identities are positional: era rosters and the Level 1 authoring
        // both index the list, so a short list would author the wrong assets.
        List<LevelConfigSO> levels = BackfillLevelIdentities();
        if (levels == null)
            return;

        List<BaybayinCharacterSO> symbols = BackfillSymbolCatalog();
        List<EraConfigSO> eras = EnsureEraShells(levels);
        AuthorLevelOne(levels[0], symbols);
        EnsureCampaignRoot(symbols, eras);
        AssetDatabase.SaveAssets();
    }

    private static List<BaybayinCharacterSO> BackfillSymbolCatalog()
    {
        var symbols = new List<BaybayinCharacterSO>(SymbolMap.Length);
        foreach ((string symbolId, string assetName, string introLevelId) in SymbolMap)
        {
            var character = AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(
                $"{CharacterFolder}/{assetName}.asset");
            if (character == null)
            {
                Debug.LogError($"RevisedCampaignBootstrap: missing character asset {assetName}.");
                continue;
            }

            character.stableId = symbolId;
            character.firstIntroductionLevelId = introLevelId;
            // SALIN-217: symbol.dara emits value.da only — value.ra now belongs to symbol.ra, which
            // takes the generic branch. Left as it was, a bootstrap run would put value.ra back on
            // Char_DA and drop the catalog to 17 again.
            // SALIN-221: the previously authored list is captured first so AppendContextSpokenValues
            // can preserve the clip and label already recorded for each context value.
            List<SpokenValueDefinition> authored = character.spokenValues;
            character.spokenValues = symbolId == ContentIdentity.RevisedDaraSymbolId
                ? new List<SpokenValueDefinition>
                {
                    SpokenValue(ContentIdentity.RevisedDaSpokenValueId, "da", character),
                }
                : new List<SpokenValueDefinition>
                {
                    SpokenValue(
                        "value." + symbolId.Substring("symbol.".Length),
                        string.IsNullOrEmpty(character.syllable)
                            ? symbolId.Substring("symbol.".Length)
                            : character.syllable,
                        character),
                };

            AppendContextSpokenValues(character, symbolId, authored);
            EditorUtility.SetDirty(character);
            symbols.Add(character);
        }

        return symbols;
    }

    /// <summary>
    /// SALIN-221 (ruling Q2): the shared E/I and O/U glyphs carry per-word-context values beyond
    /// their combined primary one. The clip and label already authored for a context value are
    /// preserved rather than regenerated: no recording exists for E, I or U, and reusing the
    /// character-level clip would silently record O.wav against value.u.
    ///
    /// SALIN-217 (merge integration): the DA/RA early-return below is now redundant — symbol.dara's
    /// ApprovedSpokenValueIds entry was narrowed to value.da alone, so the loop has nothing to
    /// append for it either way. It is kept as a cheap, explicit guard. The original note here
    /// claimed this bootstrap "keeps writing Char_DA byte-identically"; that is no longer true —
    /// SALIN-217 drops value.ra from Char_DA and authors it on the new Char_RA instead, which takes
    /// the generic single-value branch above.
    /// </summary>
    private static void AppendContextSpokenValues(
        BaybayinCharacterSO character,
        string symbolId,
        List<SpokenValueDefinition> authored)
    {
        if (symbolId == ContentIdentity.RevisedDaraSymbolId ||
            !ContentIdentity.ApprovedSpokenValueIds.TryGetValue(
                symbolId, out IReadOnlyList<string> approvedValueIds))
        {
            return;
        }

        for (int index = 1; index < approvedValueIds.Count; index++)
        {
            string valueId = approvedValueIds[index];
            SpokenValueDefinition existing = authored?.Find(
                value => value != null && value.stableId == valueId);

            character.spokenValues.Add(new SpokenValueDefinition
            {
                stableId = valueId,
                displayValue = string.IsNullOrEmpty(existing?.displayValue)
                    ? valueId.Substring("value.".Length)
                    : existing.displayValue,
                pronunciationClip = existing?.pronunciationClip,
            });
        }
    }

    private static SpokenValueDefinition SpokenValue(
        string stableId, string displayValue, BaybayinCharacterSO character)
    {
        // Real pronunciation clips are SALIN-199's manifest scope; reuse the
        // character's existing clip when one is already recorded.
        return new SpokenValueDefinition
        {
            stableId = stableId,
            displayValue = displayValue,
            pronunciationClip = character.pronunciationClip,
        };
    }

    /// <summary>
    /// Loads all fifteen level configs before writing to any of them. Returns null
    /// when one is missing so the caller aborts with nothing modified.
    /// </summary>
    private static List<LevelConfigSO> BackfillLevelIdentities()
    {
        var levels = new List<LevelConfigSO>(ContentIdentity.RevisedLevelIds.Count);
        for (int index = 0; index < ContentIdentity.RevisedLevelIds.Count; index++)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                $"{LevelFolder}/Level{index + 1}_Config.asset");
            if (level == null)
            {
                Debug.LogError(
                    $"RevisedCampaignBootstrap: missing Level{index + 1}_Config asset. "
                    + "Aborting; no assets were modified.");
                return null;
            }

            levels.Add(level);
        }

        for (int index = 0; index < levels.Count; index++)
        {
            LevelConfigSO level = levels[index];
            level.stableId = ContentIdentity.RevisedLevelIds[index];
            level.levelNumber = index + 1;
            level.eraLocalOrder = (index % ContentIdentity.RevisedLevelsPerEra) + 1;
            EditorUtility.SetDirty(level);
        }

        return levels;
    }

    private static List<EraConfigSO> EnsureEraShells(List<LevelConfigSO> levels)
    {
        string[] eraNames = { "Ugat", "Ugnayan", "Pamana" };
        var eras = new List<EraConfigSO>(ContentIdentity.RevisedEraIds.Count);
        for (int eraIndex = 0; eraIndex < ContentIdentity.RevisedEraIds.Count; eraIndex++)
        {
            string path = $"{ThemeFolder}/Era_{eraIndex + 1:00}.asset";
            var era = AssetDatabase.LoadAssetAtPath<EraConfigSO>(path);
            if (era == null)
            {
                era = ScriptableObject.CreateInstance<EraConfigSO>();
                era.eraName = eraNames[eraIndex];
                AssetDatabase.CreateAsset(era, path);
            }

            era.stableId = ContentIdentity.RevisedEraIds[eraIndex];
            era.order = eraIndex + 1;
            era.levels = levels.GetRange(
                eraIndex * ContentIdentity.RevisedLevelsPerEra,
                ContentIdentity.RevisedLevelsPerEra);
            EditorUtility.SetDirty(era);
            eras.Add(era);
        }

        return eras;
    }

    private static void AuthorLevelOne(LevelConfigSO level, List<BaybayinCharacterSO> symbols)
    {
        BaybayinCharacterSO ei = Find(symbols, "symbol.ei");
        BaybayinCharacterSO na = Find(symbols, "symbol.na");
        BaybayinCharacterSO a = Find(symbols, "symbol.a");
        BaybayinCharacterSO ma = Find(symbols, "symbol.ma");
        if (ei == null || na == null || a == null || ma == null)
            return;

        // Re-running must not wipe attachments made by later bootstraps
        // (Level1NarrativeBootstrap wires media dialogue/cutscene onto these).
        ContentMediaReferences inaMedia =
            level.focusWords != null && level.focusWords.Count > 0 ? level.focusWords[0].media : null;
        ContentMediaReferences amaMedia =
            level.focusWords != null && level.focusWords.Count > 1 ? level.focusWords[1].media : null;

        level.focusWords = new List<FocusWordDefinition>
        {
            new FocusWordDefinition
            {
                stableId = "level.ugat.01.focus.01",
                latinSpelling = "INA",
                displayLabel = "INA",
                meaning = "mother",
                // SALIN-221 (ruling Q2): a focus-word slot selects the value its word context needs.
                // INA is romanised with "I", so the shared E/I glyph carries value.i here, while the
                // pools and requirements below keep the combined citation value.
                decomposition = new List<SymbolValueReference>
                {
                    ContextReference(ei, "value.i"), Reference(na),
                },
                media = inaMedia,
            },
            new FocusWordDefinition
            {
                stableId = "level.ugat.01.focus.02",
                latinSpelling = "AMA",
                displayLabel = "AMA",
                meaning = "father",
                decomposition = new List<SymbolValueReference>
                {
                    Reference(a), Reference(ma),
                },
                media = amaMedia,
            },
        };

        level.cumulativeSymbolPool = new List<SymbolValueReference>
        {
            Reference(ei), Reference(na), Reference(a), Reference(ma),
        };

        level.learningRequirements = Requirements(ContentRequirementKind.Instruction, 1, ei, na, a, ma);
        // The x2 practice and x1 mastery success thresholds are provisional until
        // the workbook matrix confirms them (SALIN-188 review input).
        level.practiceRequirements = Requirements(ContentRequirementKind.Practice, 2, ei, na, a, ma);
        level.masteryRequirements = Requirements(ContentRequirementKind.Mastery, 1, ei, na, a, ma);

        // AMA's closing syllable, confirmed by ruling Q3 (docs/audit/AUDIT.md:313,
        // docs/design/spec-rulings-2026-09.md): the Level 1 final restoration value is MA.
        level.finalRestorationValue = Reference(ma);
        level.rewardIds = new List<string> { "memory.ugat.01" };

        level.activeClueCombatEnabled = true;
        // Glyph badge art for EI/NA/A/MA is tracked by the SALIN-199 manifest, so
        // the Latin text channel is declared alongside it to keep the clue visible.
        level.clueChannels = ClueChannels.Glyph | ClueChannels.LatinText;
        level.audioVisualFallback = ClueChannels.LatinText;
        level.challengePolicy = ChallengeTierPolicy.ForTier(1);
        level.challengeSequence = EnsureLevelOneChallengeSequence();
        // The context challenge is phase 6 of the nine-phase plan; the pre-wave
        // prototype path bypasses the tier policy and the evidence sink.
        level.challengePrototypeEnabled = false;

        // The revised roster: waves carry only the Level 1 syllables. Enemy
        // movement/attack behavior is reused per SALIN-180; glyph badge art for
        // these symbols is tracked by the SALIN-199 manifest.
        level.allowedCharacters = new List<BaybayinCharacterSO> { ei, na, a, ma };
        if (level.waves != null)
        {
            foreach (WaveDefinition wave in level.waves)
            {
                if (wave != null && !wave.isIntermissionWave)
                    wave.characters = new List<BaybayinCharacterSO> { ei, na, a, ma };
            }
        }

        EditorUtility.SetDirty(level);
    }

    private static ChallengeSequenceSO EnsureLevelOneChallengeSequence()
    {
        var sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>(ChallengeAssetPath);
        if (sequence == null)
        {
            sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            AssetDatabase.CreateAsset(sequence, ChallengeAssetPath);
        }

        sequence.sequenceId = "challenge.ugat.01";
        sequence.displayName = "Unang Alaala";
        sequence.units = new[]
        {
            PlacementUnit(
                unitId: "ugat01-place-ina",
                prompt: "Ibalik ang INA sa alaala.",
                evidenceContentId: "level.ugat.01.focus.01",
                correctTokenId: "ugat01-ina",
                correctText: "INA",
                decoyTokenId: "ugat01-ama-decoy",
                decoyText: "AMA"),
            PlacementUnit(
                unitId: "ugat01-place-ama",
                prompt: "Ibalik ang AMA sa alaala.",
                evidenceContentId: "level.ugat.01.focus.02",
                correctTokenId: "ugat01-ama",
                correctText: "AMA",
                decoyTokenId: "ugat01-ina-decoy",
                decoyText: "INA"),
        };
        EditorUtility.SetDirty(sequence);
        return sequence;
    }

    private static ChallengeUnitDefinition PlacementUnit(
        string unitId,
        string prompt,
        string evidenceContentId,
        string correctTokenId,
        string correctText,
        string decoyTokenId,
        string decoyText)
    {
        return new ChallengeUnitDefinition
        {
            unitId = unitId,
            mode = ChallengeMode.WordPlacement,
            prompt = prompt,
            evidenceContentId = evidenceContentId,
            tokens = new[]
            {
                new ChallengeTokenDefinition
                {
                    tokenId = correctTokenId,
                    displayText = correctText,
                    occurrenceId = correctTokenId,
                    role = ChallengeTokenRole.Focus,
                },
                new ChallengeTokenDefinition
                {
                    tokenId = decoyTokenId,
                    displayText = decoyText,
                    occurrenceId = decoyTokenId,
                },
            },
            slots = new[]
            {
                new ChallengeSlotDefinition
                {
                    slotId = unitId + "-slot",
                    expectedOccurrenceId = correctTokenId,
                },
            },
            candidateOccurrenceIds = new[] { correctTokenId, decoyTokenId },
            maxErrors = 3,
            heartPenalty = 1,
        };
    }

    private static void EnsureCampaignRoot(
        List<BaybayinCharacterSO> symbols, List<EraConfigSO> eras)
    {
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/Campaign"))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Campaign");

        var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignAssetPath);
        if (campaign == null)
        {
            campaign = ScriptableObject.CreateInstance<CampaignConfigSO>();
            AssetDatabase.CreateAsset(campaign, CampaignAssetPath);
        }

        campaign.manifest = CampaignIdentityManifest.CreateRevisedV1();
        campaign.tuning ??= new CampaignTuning();
        campaign.learningTuning = AssetDatabase.LoadAssetAtPath<LearningTuningSO>(LearningTuningPath);
        if (campaign.learningTuning == null)
            Debug.LogError("RevisedCampaignBootstrap: LearningTuning asset not found.");
        campaign.symbols = symbols;
        campaign.eras = eras;
        EditorUtility.SetDirty(campaign);
    }

    private static List<ContentRequirement> Requirements(
        ContentRequirementKind kind, int requiredSuccesses, params BaybayinCharacterSO[] characters)
    {
        var requirements = new List<ContentRequirement>(characters.Length);
        foreach (BaybayinCharacterSO character in characters)
        {
            requirements.Add(new ContentRequirement
            {
                kind = kind,
                requiredSuccesses = requiredSuccesses,
                symbolValue = Reference(character),
            });
        }

        return requirements;
    }

    private static SymbolValueReference Reference(BaybayinCharacterSO character)
    {
        return new SymbolValueReference
        {
            symbol = character,
            spokenValueId = "value." + character.stableId.Substring("symbol.".Length),
        };
    }

    /// <summary>
    /// SALIN-221: a reference that pins an explicit word-context spoken value instead of the
    /// symbol's primary one. Used by focus-word decompositions on the shared E/I and O/U glyphs.
    /// </summary>
    private static SymbolValueReference ContextReference(
        BaybayinCharacterSO character, string spokenValueId)
    {
        return new SymbolValueReference
        {
            symbol = character,
            spokenValueId = spokenValueId,
        };
    }

    private static BaybayinCharacterSO Find(List<BaybayinCharacterSO> symbols, string stableId)
    {
        foreach (BaybayinCharacterSO symbol in symbols)
        {
            if (symbol != null && symbol.stableId == stableId)
                return symbol;
        }

        Debug.LogError($"RevisedCampaignBootstrap: symbol {stableId} not found in catalog.");
        return null;
    }
}
