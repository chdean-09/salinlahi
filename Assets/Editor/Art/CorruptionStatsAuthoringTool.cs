using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives the seventeen corruption enemies their stats, and their abilities where
/// an existing implemented mechanic matches the workbook narrative.
///
/// SALIN-206 imported the sheets and authored the data but left moveSpeed and
/// maxHealth at the SO defaults, so every corrupted form was an identical
/// 1.5-speed, 1 HP walker. That is why the roster could not replace the colonial
/// and Japanese enemies, which do carry mechanics.
///
/// PROVENANCE — read before changing a number. The workbook's ENEMIES tab gives
/// these creatures an appearance and a behaviour sentence but, unlike the
/// colonial roster, NO "Power:" line and NO stats. Every value below is derived
/// from the authored description plus the existing balance range (Pensionado
/// 0.9 slowest ... Guardia 2.25 fastest; HP 1 standard, Capitan/Shokan 2,
/// General 3) and is a PROPOSAL for team review, not transcribed design.
///
/// Level placement constrains the curve: Iligaw, Nawalang-Mukha, Abo ng Simula
/// and Mantsa are Level 1 (the tutorial) and Bakod is Level 2, so they stay
/// gentle and take no ability no matter how well the narrative would fit — a
/// phasing or glyph-scrambling enemy in the teaching levels would punish players
/// who are still learning to draw.
///
/// Abilities are only assigned where an ALREADY IMPLEMENTED mechanic is a
/// literal match and the enemy first appears at Level 6 or later (or nowhere
/// yet). Narratives that describe genuinely new systems — Hati splitting in two,
/// Gapos trapping the player's hands, Salungat reversing commands, Uhaw draining
/// sound, Yapos ng Dilim trapping a word's final symbol — are deliberately NOT
/// faked with a lookalike mechanic; they are listed in the PR as follow-up work.
/// </summary>
public static class CorruptionStatsAuthoringTool
{
    private const string EnemyFolder = "Assets/ScriptableObjects/Enemies";

    private enum Ability { None, Decoy, Phaser, Zigzag }

    private sealed class Spec
    {
        public string Asset;
        public float Speed;
        public int Health;
        public Ability Ability = Ability.None;
        public string Rationale;
    }

    // Ordered by first level of appearance so the curve reads top to bottom.
    private static readonly Spec[] Specs =
    {
        // ---- Level 1: the teaching level. Gentle, no abilities. ----
        new Spec { Asset = "EnemyData_AbongSimula",   Speed = 1.60f, Health = 1,
                   Rationale = "\"A small humanoid made of ash\" - light and quick, but Level 1 so its vanishing face stays cosmetic." },
        new Spec { Asset = "EnemyData_Iligaw",        Speed = 1.50f, Health = 1,
                   Rationale = "\"A thin mirror-like creature\" - baseline pace; its false-copy trick needs a new mechanic." },
        new Spec { Asset = "EnemyData_NawalangMukha", Speed = 1.45f, Health = 1,
                   Rationale = "\"A faceless figure carrying stolen nameplates\" - burdened, a shade under baseline." },
        new Spec { Asset = "EnemyData_Mantsa",        Speed = 1.15f, Health = 1,
                   Rationale = "\"A crawling mass of black ink that spreads\" - crawling, so clearly slow." },

        // ---- Level 2-3 ----
        new Spec { Asset = "EnemyData_Bakod",         Speed = 0.85f, Health = 1,
                   Rationale = "\"A wide stone creature shaped like a moving wall... prevents Juan from moving forward\" - the slowest in the roster, which carries the wall on its own. NOT 2 HP: Bakod is the only BA form and BA is Level 2's only syllable, so every enemy in that level's first two waves is a Bakod - 2 HP would double the required draws from 6 to 12 in the second level of the game, and a correct draw that does not kill reads to a learner as a failed draw. If the wall should tank, the lever is Level 2's wave composition, not this number." },
        new Spec { Asset = "EnemyData_Takip",         Speed = 1.30f, Health = 1,
                   Rationale = "\"A cloaked creature with a large hand covering its single eye\" - deliberate, unhurried; hiding answers needs a new mechanic." },

        // ---- Level 6 onward: abilities allowed. ----
        new Spec { Asset = "EnemyData_Ngatngat",      Speed = 1.90f, Health = 1,
                   Rationale = "\"A swarm of ink moths with sharp paper-like wings\" - a swarm of moths is the fastest thing in the roster, and the most fragile." },
        new Spec { Asset = "EnemyData_Punit",         Speed = 1.70f, Health = 1,
                   Rationale = "\"A beast formed from torn pages\" - light debris, quick and flimsy." },
        new Spec { Asset = "EnemyData_Salungat",      Speed = 1.50f, Health = 1, Ability = Ability.Decoy,
                   Rationale = "\"It reverses commands and causes allies to attack the wrong target\" - that IS the decoy contract: the enemy you are not supposed to draw." },
        new Spec { Asset = "EnemyData_Hati",          Speed = 1.40f, Health = 1,
                   Rationale = "\"A masked creature that splits into two smaller enemies\" - splitting needs a new spawn-on-death mechanic; stats only for now." },
        new Spec { Asset = "EnemyData_Labo",          Speed = 1.35f, Health = 1, Ability = Ability.Phaser,
                   Rationale = "\"A floating figure surrounded by thick gray fog. It hides symbols, faces, paths\" - fog that hides and reveals is exactly the phaser fade." },
        new Spec { Asset = "EnemyData_Kadena",        Speed = 1.05f, Health = 2,
                   Rationale = "\"A warrior made of chains and broken locks\" - armoured in chains: slow and takes two draws." },
        new Spec { Asset = "EnemyData_Gapos",         Speed = 1.00f, Health = 2,
                   Rationale = "\"A creature made of dark roots and ropes\" - rooted and heavy; trapping the player's hands needs a new input mechanic." },
        new Spec { Asset = "EnemyData_Walang-Awa",    Speed = 0.95f, Health = 3,
                   Rationale = "\"An armored creature with an empty space where its heart should be\" - the armoured one, and the only non-boss at 3 HP alongside General." },

        // ---- Not yet placed in any wave. ----
        new Spec { Asset = "EnemyData_Daan-Lihis",    Speed = 1.50f, Health = 1, Ability = Ability.Zigzag,
                   Rationale = "\"A long serpent... It changes the direction of paths\" - a serpent that changes direction is the zigzag mover, literally." },
        new Spec { Asset = "EnemyData_Uhaw",          Speed = 1.25f, Health = 2,
                   Rationale = "\"A hollow creature... body made of dry soil\" - heavy and dry; draining sound needs a new audio mechanic." },
        new Spec { Asset = "EnemyData_YaposngDilim",  Speed = 1.10f, Health = 3,
                   Rationale = "\"A tall shadow with long arms wrapped around a glowing child\" - the heaviest of the roster; trapping a word's final symbol needs a new mechanic." },
    };

    [MenuItem("Salinlahi/Art/Author Corruption Stats")]
    public static void Run()
    {
        int authored = 0;
        var abilityTargets = new List<(EnemyDataSO data, Ability ability)>();

        foreach (Spec spec in Specs)
        {
            string path = $"{EnemyFolder}/{spec.Asset}.asset";
            var so = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
            if (so == null)
            {
                Debug.LogError($"[CorruptionStats] Missing asset: {path}");
                continue;
            }

            so.moveSpeed = spec.Speed;
            so.maxHealth = spec.Health;

            // Decoy is pure data — Maestro carries no extra component — so it can
            // be switched on here. Phaser and zigzag need a component, which means
            // a prefab and an EnemyPool registration; those are reported below.
            if (spec.Ability == Ability.Decoy)
            {
                so.isDecoy = true;
                // A decoy the player must NOT draw should not also punish them by
                // walking into the base, matching Maestro.
                so.dealsContactDamage = false;
            }
            else if (spec.Ability != Ability.None)
            {
                abilityTargets.Add((so, spec.Ability));
            }

            EditorUtility.SetDirty(so);
            authored++;
            Debug.Log($"[CorruptionStats] {spec.Asset}: speed={spec.Speed} hp={spec.Health} ability={spec.Ability} — {spec.Rationale}");
        }

        foreach ((EnemyDataSO data, Ability ability) in abilityTargets)
        {
            if (ability == Ability.Phaser)
            {
                data.isPhaser = true;
                // Fraile's cadence, slowed slightly: fog should linger rather than blink.
                data.phaserInterval = 1f;
                data.phaserInitialVisibleDelayMin = 0.6f;
                data.phaserInitialVisibleDelayMax = 1.2f;
                data.phaserVisibleHoldMin = 2.2f;
                data.phaserVisibleHoldMax = 3.2f;
                data.phaserInvisibleHoldMin = 0.7f;
                data.phaserInvisibleHoldMax = 1.2f;
                data.phaserFadeOutDuration = 0.8f;
                data.phaserFadeInDuration = 0.12f;
                data.phaserFadeOutPulseCount = 1;
                data.phaserFadeOutPulseAmplitude = 0.3f;
            }
            else if (ability == Ability.Zigzag)
            {
                // Pensionado's values: a proven-readable weave.
                data.zigzagAmplitude = 1.2f;
                data.zigzagFrequency = 0.5f;
            }
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[CorruptionStats] Authored {authored} enemies.");
        foreach ((EnemyDataSO data, Ability ability) in abilityTargets)
        {
            string component = ability == Ability.Phaser ? "PhaserEnemy" : "PensionadoMover";
            Debug.LogWarning(
                $"[CorruptionStats] {data.name} wants {ability}: its data is set, but the mechanic "
                + $"also needs a prefab carrying {component} plus an EnemyPool registration for "
                + $"enemyID '{data.enemyID}'. Run 'Salinlahi/Art/Register Corruption Prefabs'.");
        }
    }
}
