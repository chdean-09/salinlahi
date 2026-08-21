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

    // Visual symbol -> (character asset, introduction level). DA and RA are
    // contextual values of the single symbol.dara identity (Char_DA); Char_RA
    // stays a legacy asset outside the revised catalog. OU's introduction level
    // is provisional until the workbook matrix confirms it (SALIN-188/204).
    private static readonly (string SymbolId, string AssetName, string IntroLevelId)[] SymbolMap =
    {
        ("symbol.a", "Char_A", "level.ugat.01"),
        ("symbol.ei", "Char_EI", "level.ugat.01"),
        ("symbol.ba", "Char_BA", "level.ugat.02"),
        ("symbol.ma", "Char_MA", "level.ugat.01"),
        ("symbol.na", "Char_NA", "level.ugat.01"),
        ("symbol.ta", "Char_TA", "level.ugat.02"),
        ("symbol.ou", "Char_OU", "level.ugat.04"),
        ("symbol.ka", "Char_KA", "level.ugnayan.02"),
        ("symbol.ga", "Char_GA", "level.ugnayan.01"),
        ("symbol.sa", "Char_SA", "level.ugnayan.02"),
        ("symbol.wa", "Char_WA", "level.ugnayan.01"),
        ("symbol.ya", "Char_YA", "level.ugnayan.03"),
        ("symbol.dara", "Char_DA", "level.pamana.01"),
        ("symbol.ha", "Char_HA", "level.pamana.02"),
        ("symbol.la", "Char_LA", "level.pamana.01"),
        ("symbol.nga", "Char_NGA", "level.pamana.02"),
        ("symbol.pa", "Char_PA", "level.pamana.05"),
    };

    [MenuItem("Salinlahi/Campaign/Bootstrap Revised Campaign (Level 1)")]
    public static void Run()
    {
        List<BaybayinCharacterSO> symbols = BackfillSymbolCatalog();
        List<LevelConfigSO> levels = BackfillLevelIdentities();
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
            character.spokenValues = symbolId == ContentIdentity.RevisedDaraSymbolId
                ? new List<SpokenValueDefinition>
                {
                    SpokenValue("value.da", "da", character),
                    SpokenValue("value.ra", "ra", character),
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
            EditorUtility.SetDirty(character);
            symbols.Add(character);
        }

        return symbols;
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

    private static List<LevelConfigSO> BackfillLevelIdentities()
    {
        var levels = new List<LevelConfigSO>(ContentIdentity.RevisedLevelIds.Count);
        for (int index = 0; index < ContentIdentity.RevisedLevelIds.Count; index++)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                $"{LevelFolder}/Level{index + 1}_Config.asset");
            if (level == null)
            {
                Debug.LogError($"RevisedCampaignBootstrap: missing Level{index + 1}_Config asset.");
                continue;
            }

            level.stableId = ContentIdentity.RevisedLevelIds[index];
            level.levelNumber = index + 1;
            level.eraLocalOrder = (index % ContentIdentity.RevisedLevelsPerEra) + 1;
            EditorUtility.SetDirty(level);
            levels.Add(level);
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

        level.focusWords = new List<FocusWordDefinition>
        {
            new FocusWordDefinition
            {
                stableId = "level.ugat.01.focus.01",
                latinSpelling = "INA",
                displayLabel = "INA",
                meaning = "mother",
                decomposition = new List<SymbolValueReference>
                {
                    Reference(ei), Reference(na),
                },
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
            },
        };

        level.cumulativeSymbolPool = new List<SymbolValueReference>
        {
            Reference(ei), Reference(na), Reference(a), Reference(ma),
        };

        level.learningRequirements = Requirements(ContentRequirementKind.Instruction, 1, ei, na, a, ma);
        level.practiceRequirements = Requirements(ContentRequirementKind.Practice, 2, ei, na, a, ma);
        level.masteryRequirements = Requirements(ContentRequirementKind.Mastery, 1, ei, na, a, ma);

        // INA's closing syllable; provisional until the workbook matrix confirms
        // the Level 1 final restoration value (SALIN-188 review input).
        level.finalRestorationValue = Reference(na);
        level.rewardIds = new List<string> { "memory.ugat.01" };

        level.activeClueCombatEnabled = true;
        level.clueChannels = ClueChannels.Glyph;
        level.audioVisualFallback = ClueChannels.LatinText;
        level.challengePolicy = ChallengeTierPolicy.ForTier(1);
        level.challengeSequence = EnsureLevelOneChallengeSequence();

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
                prompt: "Ibalik ang INA sa alaala. (Restore INA to the memory.)",
                evidenceContentId: "level.ugat.01.focus.01",
                correctTokenId: "ugat01-ina",
                correctText: "INA",
                decoyTokenId: "ugat01-ama-decoy",
                decoyText: "AMA"),
            PlacementUnit(
                unitId: "ugat01-place-ama",
                prompt: "Ibalik ang AMA sa alaala. (Restore AMA to the memory.)",
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
