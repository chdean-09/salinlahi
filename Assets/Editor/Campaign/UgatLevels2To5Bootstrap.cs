using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-204 — authors the Ugat Levels 2–5 configurations.
///
/// Source of truth is the approved workbook matrix
/// (docs/technical/TW-SPK-004-educational-content-matrix.xlsx, sheet "Verified 30 Focus-Word Slots"):
/// focus words, decompositions, cumulative pools and the per-level last-required syllable all come
/// from there. Meanings for Levels 2–3 come from docs/review/focus-word-checklist.md; Level 4 repeats
/// Level 1's INA/AMA; Level 5's come from the SALIN-205 narrative.
///
/// Modelled on Level1NarrativeBootstrap so symbols resolve by stableId instead of hardcoded GUIDs.
/// Media (context image, narration, dialogue, cutscene) is deliberately NOT set here — that is
/// SALIN-205's and SALIN-206's scope.
/// </summary>
public static class UgatLevels2To5Bootstrap
{
    private const string CampaignPath =
        "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

    private sealed class Slot
    {
        public string FocusId;
        public string Word;
        public string Meaning;
        public string[] Symbols;
    }

    private sealed class LevelPlan
    {
        public string LevelId;
        public Slot[] Slots;
        public string[] Pool;
        public string FinalRestorationSymbol;
    }

    // Pool is identical across Levels 2–5: every symbol introduced at or before that level.
    // Matches the workbook's "Cumulative Symbol Pool" column (A, E/I, BA, MA, NA, TA).
    private static readonly string[] UgatPool =
        { "symbol.a", "symbol.ei", "symbol.ba", "symbol.ma", "symbol.na", "symbol.ta" };

    private static readonly LevelPlan[] Plans =
    {
        new LevelPlan
        {
            LevelId = "level.ugat.02",
            Slots = new[]
            {
                new Slot { FocusId = "level.ugat.02.focus.01", Word = "BATA", Meaning = "child",
                           Symbols = new[] { "symbol.ba", "symbol.ta" } },
                new Slot { FocusId = "level.ugat.02.focus.02", Word = "MATA", Meaning = "eye",
                           Symbols = new[] { "symbol.ma", "symbol.ta" } },
            },
            Pool = UgatPool,
            FinalRestorationSymbol = "symbol.ta", // MATA -> TA
        },
        new LevelPlan
        {
            LevelId = "level.ugat.03",
            Slots = new[]
            {
                new Slot { FocusId = "level.ugat.03.focus.01", Word = "BATA", Meaning = "child",
                           Symbols = new[] { "symbol.ba", "symbol.ta" } },
                new Slot { FocusId = "level.ugat.03.focus.02", Word = "TAMA", Meaning = "correct",
                           Symbols = new[] { "symbol.ta", "symbol.ma" } },
            },
            Pool = UgatPool,
            FinalRestorationSymbol = "symbol.ma", // TAMA -> MA
        },
        new LevelPlan
        {
            LevelId = "level.ugat.04",
            Slots = new[]
            {
                new Slot { FocusId = "level.ugat.04.focus.01", Word = "INA", Meaning = "mother",
                           Symbols = new[] { "symbol.ei", "symbol.na" } },
                new Slot { FocusId = "level.ugat.04.focus.02", Word = "AMA", Meaning = "father",
                           Symbols = new[] { "symbol.a", "symbol.ma" } },
            },
            Pool = UgatPool,
            FinalRestorationSymbol = "symbol.ma", // AMA -> MA
        },
        new LevelPlan
        {
            LevelId = "level.ugat.05",
            Slots = new[]
            {
                new Slot { FocusId = "level.ugat.05.focus.01", Word = "IBA", Meaning = "different",
                           Symbols = new[] { "symbol.ei", "symbol.ba" } },
                new Slot { FocusId = "level.ugat.05.focus.02", Word = "MANA", Meaning = "inheritance",
                           Symbols = new[] { "symbol.ma", "symbol.na" } },
            },
            Pool = UgatPool,
            FinalRestorationSymbol = "symbol.na", // MANA -> NA
        },
    };

    [MenuItem("Salinlahi/Campaign/Author Ugat Levels 2-5")]
    public static void Run()
    {
        CampaignConfigSO campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignPath);
        if (campaign == null)
        {
            Debug.LogError("[SALIN-204] Could not load " + CampaignPath);
            return;
        }

        int authored = 0;
        foreach (LevelPlan plan in Plans)
        {
            if (!campaign.TryGetLevel(plan.LevelId, out LevelConfigSO level))
            {
                Debug.LogError("[SALIN-204] Level not found: " + plan.LevelId);
                continue;
            }

            level.focusWords = new List<FocusWordDefinition>();
            foreach (Slot slot in plan.Slots)
            {
                var focus = new FocusWordDefinition
                {
                    stableId = slot.FocusId,
                    latinSpelling = slot.Word,
                    displayLabel = slot.Word,
                    meaning = slot.Meaning,
                    decomposition = new List<SymbolValueReference>(),
                };

                foreach (string symbolId in slot.Symbols)
                    focus.decomposition.Add(Reference(campaign, symbolId));

                level.focusWords.Add(focus);
            }

            level.cumulativeSymbolPool = new List<SymbolValueReference>();
            foreach (string symbolId in plan.Pool)
                level.cumulativeSymbolPool.Add(Reference(campaign, symbolId));

            // Level 1's convention: one requirement per pooled symbol, per phase.
            level.learningRequirements = Requirements(campaign, plan.Pool, ContentRequirementKind.Instruction, 1);
            level.practiceRequirements = Requirements(campaign, plan.Pool, ContentRequirementKind.Practice, 2);
            level.masteryRequirements = Requirements(campaign, plan.Pool, ContentRequirementKind.Mastery, 1);

            level.finalRestorationValue = Reference(campaign, plan.FinalRestorationSymbol);

            EditorUtility.SetDirty(level);
            authored++;
            Debug.Log("[SALIN-204] Authored " + plan.LevelId +
                      " (" + plan.Slots[0].Word + " + " + plan.Slots[1].Word + ")");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SALIN-204] Authored " + authored + " level(s).");
    }

    private static List<ContentRequirement> Requirements(
        CampaignConfigSO campaign, IEnumerable<string> symbolIds,
        ContentRequirementKind kind, int requiredSuccesses)
    {
        var list = new List<ContentRequirement>();
        foreach (string symbolId in symbolIds)
        {
            list.Add(new ContentRequirement
            {
                kind = kind,
                symbolValue = Reference(campaign, symbolId),
                requiredSuccesses = requiredSuccesses,
            });
        }

        return list;
    }

    /// <summary>
    /// Resolves a symbol by stableId and pairs it with that symbol's single spoken value.
    /// </summary>
    private static SymbolValueReference Reference(CampaignConfigSO campaign, string symbolId)
    {
        if (!campaign.TryGetSymbol(symbolId, out BaybayinCharacterSO symbol))
            throw new InvalidOperationException("[SALIN-204] Unknown symbol: " + symbolId);

        if (symbol.spokenValues == null || symbol.spokenValues.Count != 1)
        {
            throw new InvalidOperationException(
                "[SALIN-204] Expected exactly one spoken value on " + symbolId +
                " but found " + (symbol.spokenValues == null ? 0 : symbol.spokenValues.Count) +
                ". Decompositions must name the intended value explicitly.");
        }

        return new SymbolValueReference
        {
            symbol = symbol,
            spokenValueId = symbol.spokenValues[0].stableId,
        };
    }
}
