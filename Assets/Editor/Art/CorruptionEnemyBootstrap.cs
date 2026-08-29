using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-206 / SALIN-176 — imports the corruption-enemy sprite sheets and authors their
/// EnemyDataSO assets.
///
/// Design source: CORE GAME MECHANICS.xlsx, ENEMIES tab. "Each Baybayin symbol has a corrupted
/// enemy form created by Paglimot. The enemy represents the opposite of the symbol's true lesson."
/// Seventeen symbols, seventeen enemies; six are delivered so far.
///
/// Import contract (validated on Mantsa first, see CorruptionSpriteImportSpike):
///   Sprite / Multiple, sliced into 4 frames of 1024x1024, center pivot, bilinear filtering,
///   alphaIsTransparency, no mipmaps, PPU 192 so 1024px spans 5.333 world units - identical to a
///   32x32 frame at the project-wide PPU of 6.
///
/// Deliberately NOT set here: moveSpeed and maxHealth are left at their SO defaults because the
/// workbook specifies no stats, and each enemy's signature ability is unimplemented - none of the
/// six maps onto an existing variant (sprinter, shielded, phaser, decoy, zigzagger, commander).
/// </summary>
public static class CorruptionEnemyBootstrap
{
    private const string SpriteFolder = "Assets/Art/Characters/Enemies/Corruption";
    private const string DataFolder = "Assets/ScriptableObjects/Enemies";
    private const int FrameSize = 1024;
    private const int Columns = 2;
    private const int Rows = 2;
    private const float TargetPixelsPerUnit = 192f;

    private sealed class Corruption
    {
        public string Key;          // file + asset stem
        public string EnemyId;
        public string DisplayName;
        public string SymbolId;
        public string Appearance;   // workbook "Appearance"
        public string Ability;      // workbook "Ability" - recorded, not implemented
    }

    private static readonly Corruption[] Roster =
    {
        new Corruption
        {
            Key = "abongsimula", EnemyId = "abo-ng-simula", DisplayName = "Abo ng Simula",
            SymbolId = "symbol.a",
            Appearance = "A small humanoid made of ash. Its face disappears whenever the wind blows.",
            Ability = "It covers the first symbol of a word with ash.",
        },
        new Corruption
        {
            Key = "iligaw", EnemyId = "iligaw", DisplayName = "Iligaw", SymbolId = "symbol.ei",
            Appearance = "A thin mirror-like creature with two faces. One face says E, while the "
                         + "other says I.",
            Ability = "It changes directions and creates false copies of the correct symbol.",
        },
        new Corruption
        {
            Key = "uhaw", EnemyId = "uhaw", DisplayName = "Uhaw", SymbolId = "symbol.ou",
            Appearance = "A hollow creature with a large empty mouth and a body made of dry soil.",
            Ability = "It drains sound from nearby words.",
        },
        new Corruption
        {
            Key = "bakod", EnemyId = "bakod", DisplayName = "Bakod", SymbolId = "symbol.ba",
            Appearance = "A wide stone creature shaped like a moving wall.",
            Ability = "It blocks paths, separates family members, and prevents Juan from moving "
                      + "forward.",
        },
        new Corruption
        {
            Key = "kadena", EnemyId = "kadena", DisplayName = "Kadena", SymbolId = "symbol.ka",
            Appearance = "A warrior made of chains and broken locks.",
            Ability = "It binds villagers and prevents them from helping Juan.",
        },
        new Corruption
        {
            Key = "mantsa", EnemyId = "mantsa", DisplayName = "Mantsa", SymbolId = "symbol.ma",
            Appearance = "A crawling mass of black ink that spreads across family portraits.",
            Ability = "It stains correct symbols and changes them into incorrect forms.",
        },
        new Corruption
        {
            Key = "nawalangmukha", EnemyId = "nawalang-mukha", DisplayName = "Nawalang Mukha",
            SymbolId = "symbol.na",
            Appearance = "A faceless figure carrying stolen nameplates.",
            Ability = "It removes names from characters and dialogue boxes.",
        },
        new Corruption
        {
            Key = "takip", EnemyId = "takip", DisplayName = "Takip", SymbolId = "symbol.ta",
            Appearance = "A cloaked creature with a large hand covering its single eye.",
            Ability = "It hides correct answers and covers important objects.",
        },
        new Corruption
        {
            Key = "salungat", EnemyId = "salungat", DisplayName = "Salungat", SymbolId = "symbol.sa",
            Appearance = "A two-headed creature whose heads constantly argue.",
            Ability = "It reverses commands and causes allies to attack the wrong target.",
        },
        new Corruption
        {
            Key = "labo", EnemyId = "labo", DisplayName = "Labo", SymbolId = "symbol.la",
            Appearance = "A floating figure surrounded by thick gray fog.",
            Ability = "It hides symbols, faces, paths, and environmental clues.",
        },
        new Corruption
        {
            Key = "ngatngat", EnemyId = "ngatngat", DisplayName = "Ngatngat", SymbolId = "symbol.nga",
            Appearance = "A swarm of ink moths with sharp paper-like wings.",
            Ability = "It eats letters, woven patterns, documents, and inscriptions.",
        },
        new Corruption
        {
            Key = "walangawa", EnemyId = "walang-awa", DisplayName = "Walang-Awa",
            SymbolId = "symbol.wa",
            Appearance = "An armored creature with an empty space where its heart should be.",
            Ability = "It weakens villagers and prevents Juan from healing or helping them.",
        },
        new Corruption
        {
            Key = "punit", EnemyId = "punit", DisplayName = "Punit", SymbolId = "symbol.pa",
            Appearance = "A beast formed from torn pages, broken cloth, and ripped letters.",
            Ability = "It tears sentences into separate pieces.",
        },
        new Corruption
        {
            // The workbook calls this Paglimot's strongest servant, guarding YA because it
            // completes MALAYA. It is delivered on the same 1024x1024 canvas as every other
            // creature, so it imports at the standard tier; whether it should render at boss
            // scale is an open decision.
            Key = "yaposngdilim", EnemyId = "yapos-ng-dilim", DisplayName = "Yapos ng Dilim",
            SymbolId = "symbol.ya",
            Appearance = "A tall shadow with long arms wrapped around a glowing child.",
            Ability = "It traps the final symbol of important words such as saya, haraya, and "
                      + "malaya.",
        },
    };

    [MenuItem("Salinlahi/Art/Author Corruption Enemies")]
    public static void Run()
    {
        var report = new StringBuilder();
        int imported = 0;
        int authored = 0;

        Dictionary<string, BaybayinCharacterSO> symbols = LoadSymbols();

        foreach (Corruption entry in Roster)
        {
            string sheetPath = SpriteFolder + "/sprite_enemy_" + entry.Key + "_walk-Sheet.png";
            if (!File.Exists(sheetPath))
            {
                report.AppendLine("MISSING SHEET: " + sheetPath);
                continue;
            }

            Sprite[] frames = ImportSheet(sheetPath, entry.Key);
            if (frames.Length != Columns * Rows)
            {
                report.AppendLine($"SLICE FAILED: {entry.Key} produced {frames.Length} sprites");
                continue;
            }

            imported++;

            if (!symbols.TryGetValue(entry.SymbolId, out BaybayinCharacterSO symbol))
            {
                report.AppendLine("UNKNOWN SYMBOL: " + entry.SymbolId + " for " + entry.DisplayName);
                continue;
            }

            string dataPath = DataFolder + "/EnemyData_" + entry.DisplayName.Replace(" ", "") + ".asset";
            EnemyDataSO data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(dataPath);
            bool created = data == null;
            if (created)
            {
                data = ScriptableObject.CreateInstance<EnemyDataSO>();
                AssetDatabase.CreateAsset(data, dataPath);
            }

            data.enemyID = entry.EnemyId;
            data.displayName = entry.DisplayName;
            data.description = entry.Appearance + " " + entry.Ability;
            data.walkFrames = frames;
            // The syllable IS this enemy's identity, per the workbook - so it is pinned here rather
            // than drawn from the level roster at runtime the way the colonial enemies are.
            data.assignedCharacter = symbol;

            EditorUtility.SetDirty(data);
            authored++;
            report.AppendLine(
                $"{(created ? "CREATED" : "UPDATED")} {dataPath}  " +
                $"enemyID={data.enemyID}  symbol={symbol.stableId}  frames={frames.Length}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine($"imported sheets = {imported}/{Roster.Length}");
        report.AppendLine($"authored data   = {authored}/{Roster.Length}");

        string outPath = Environment.GetEnvironmentVariable("CORRUPTION_OUT") ?? "corruption_report.txt";
        File.WriteAllText(outPath, report.ToString());
        Debug.Log("[SALIN-206] wrote " + outPath);
        EditorApplication.Exit(authored == Roster.Length ? 0 : 3);
    }

    private static Dictionary<string, BaybayinCharacterSO> LoadSymbols()
    {
        var map = new Dictionary<string, BaybayinCharacterSO>(StringComparer.Ordinal);
        foreach (string guid in AssetDatabase.FindAssets("t:BaybayinCharacterSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var symbol = AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(path);
            if (symbol != null && !string.IsNullOrEmpty(symbol.stableId))
                map[symbol.stableId] = symbol;
        }

        return map;
    }

    private static Sprite[] ImportSheet(string path, string key)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return Array.Empty<Sprite>();

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = TargetPixelsPerUnit;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;

        var rects = new List<SpriteMetaData>();
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                int index = row * Columns + column + 1;
                rects.Add(new SpriteMetaData
                {
                    name = key + "_walk_" + index.ToString("00"),
                    // Texture origin is bottom-left, so invert the row to keep frame order.
                    rect = new Rect(column * FrameSize, (Rows - 1 - row) * FrameSize, FrameSize, FrameSize),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                });
            }
        }

#pragma warning disable CS0618 // Avoids an asmdef dependency on Unity.2D.Sprite.Editor.
        importer.spritesheet = rects.ToArray();
#pragma warning restore CS0618

        importer.SaveAndReimport();

        var frames = new List<Sprite>();
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Sprite sprite)
                frames.Add(sprite);
        }

        frames.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return frames.ToArray();
    }
}
