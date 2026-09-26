using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Unity-aware importer and data-asset wiring for the supplied Walang-Awa armor frames.</summary>
public static class EnemyAbilityVisualAssetSetup
{
    private const string OutputFolder = "Assets/Art/VFX/EnemyAbilities/WalangAwaArmor";
    private const string EnemyDataPath = "Assets/ScriptableObjects/Enemies/EnemyData_Walang-Awa.asset";
    private const string FrameNamePrefix = "armor-break-layer-1711-";
    private const int FrameCount = 8;
    private const string BakodOutputFolder = "Assets/Art/VFX/EnemyAbilities/BakodBarrier";
    private const string MantsaOutputFolder = "Assets/Art/VFX/EnemyAbilities/MantsaStain";
    private const string AboOutputFolder = "Assets/Art/VFX/EnemyAbilities/AboClueCover";
    private const string TakipOutputFolder = "Assets/Art/VFX/EnemyAbilities/TakipGlyphCover";
    private const string TakipDataPath = "Assets/ScriptableObjects/Enemies/EnemyData_Takip.asset";
    private const string TakipFrameNamePrefix = "cover-reveal-glyph-2110-";

    [MenuItem("Salinlahi/Art/Import And Assign Walang-Awa Armor Overlay")]
    public static void ImportAndAssignWalangAwaArmorOverlay()
    {
        EnsureAssetFolder(OutputFolder);
        var sprites = new Sprite[FrameCount];

        for (int i = 0; i < FrameCount; i++)
        {
            string path = $"{OutputFolder}/{FrameNamePrefix}{i + 1:00}.png";
            if (!File.Exists(path))
            {
                Debug.LogError($"Walang-Awa armor frame is missing: {path}. Extract the eight PNGs from the supplied ZIP first.");
                return;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"Unity could not find a texture importer for {path}.");
                return;
            }

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !importer.alphaIsTransparency
                || importer.mipmapEnabled
                || importer.filterMode != FilterMode.Point
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.spritePixelsPerUnit != 96f
                || textureSettings.textureType != TextureImporterType.Sprite
                || textureSettings.spriteMode != (int)SpriteImportMode.Single
                || !textureSettings.alphaIsTransparency
                || textureSettings.mipmapEnabled
                || textureSettings.filterMode != FilterMode.Point
                || textureSettings.spritePixelsPerUnit != 96f
                || textureSettings.spriteAlignment != (int)SpriteAlignment.Center
                || textureSettings.spritePivot != new Vector2(0.5f, 0.5f)
                || importer.crunchedCompression;

            textureSettings.textureType = TextureImporterType.Sprite;
            textureSettings.spriteMode = (int)SpriteImportMode.Single;
            textureSettings.alphaIsTransparency = true;
            textureSettings.mipmapEnabled = false;
            textureSettings.filterMode = FilterMode.Point;
            textureSettings.spritePixelsPerUnit = 96f;
            textureSettings.spriteAlignment = (int)SpriteAlignment.Center;
            textureSettings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(textureSettings);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.spritePixelsPerUnit = 96f;
            if (changed)
                importer.SaveAndReimport();

            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprites[i] == null)
            {
                Debug.LogError($"Unity did not import {path} as a Sprite.");
                return;
            }
        }

        EnemyDataSO data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(EnemyDataPath);
        if (data == null)
        {
            Debug.LogError($"Could not load Walang-Awa EnemyDataSO at {EnemyDataPath}.");
            return;
        }

        var definitions = new List<EnemyAbilityVisualDefinition>();
        if (data.abilityVisuals != null)
        {
            for (int i = 0; i < data.abilityVisuals.Length; i++)
            {
                EnemyAbilityVisualDefinition existing = data.abilityVisuals[i];
                if (existing != null && existing.id != EnemyAbilityVisualId.Armor)
                    definitions.Add(existing);
            }
        }

        definitions.Add(new EnemyAbilityVisualDefinition
        {
            id = EnemyAbilityVisualId.Armor,
            anchor = EnemyAbilityVisualAnchor.EnemyBody,
            frameMode = EnemyAbilityVisualFrameMode.SingleFrame,
            singleFrameIndex = 0,
            activeSprite = sprites[0],
            activeTint = Color.white,
            activeOpacity = 0.45f,
            exitFrames = sprites,
            exitFramesPerSecond = 8f,
            exitTint = Color.white,
            exitOpacity = 0.85f,
            material = null,
            localOffset = Vector3.zero,
            localScale = Vector3.one,
            sortingOrderOffset = 1
        });

        data.abilityVisuals = definitions.ToArray();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"Imported {FrameCount} Walang-Awa armor sprites and assigned frame 01 as the frame-zero overlay and frames 01–08 as its 8 fps removal animation.");
    }

    [MenuItem("Salinlahi/Art/Import And Assign Bakod, Mantsa, And Abo Ability Art")]
    public static void ImportAndAssignRemainingAbilityArt()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Sprite[] bakodFrames = ImportSpriteSequence(
            BakodOutputFolder,
            "cracking-barrier-break-1652-",
            8);
        Sprite[] mantsaFrames = ImportSpriteSequence(
            MantsaOutputFolder,
            "splash-paint-clear-1622-",
            7);
        Sprite[] ashFrames = ImportSpriteSequence(
            AboOutputFolder,
            "ash-settle-cover-1609-",
            8);
        if (bakodFrames == null || mantsaFrames == null || ashFrames == null)
            return;

        EnemyDataSO bakodData = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
            "Assets/ScriptableObjects/Enemies/EnemyData_Bakod.asset");
        EnemyDataSO mantsaData = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
            "Assets/ScriptableObjects/Enemies/EnemyData_Mantsa.asset");
        EnemyDataSO aboData = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
            "Assets/ScriptableObjects/Enemies/EnemyData_AbongSimula.asset");
        if (bakodData == null || mantsaData == null || aboData == null)
        {
            Debug.LogError("Could not load one or more Bakod, Mantsa, or Abo ng Simula EnemyDataSO assets.");
            return;
        }

        ReplaceAbilityVisual(bakodData, new EnemyAbilityVisualDefinition
        {
            id = EnemyAbilityVisualId.BakodBarrier,
            anchor = EnemyAbilityVisualAnchor.EnemyBody,
            frameMode = EnemyAbilityVisualFrameMode.FullLoop,
            activeSprite = bakodFrames[0],
            activeTint = Color.white,
            activeOpacity = 1f,
            exitFrames = bakodFrames,
            exitFramesPerSecond = 8f,
            exitTint = Color.white,
            exitOpacity = 1f,
            localOffset = Vector3.zero,
            localScale = Vector3.one,
            sortingOrderOffset = 1
        });
        ReplaceAbilityVisual(bakodData, new EnemyAbilityVisualDefinition
        {
            id = EnemyAbilityVisualId.BakodBlockedTarget,
            anchor = EnemyAbilityVisualAnchor.GlyphBadge,
            frameMode = EnemyAbilityVisualFrameMode.FullLoop,
            activeSprite = bakodFrames[0],
            activeTint = Color.white,
            activeOpacity = 0.95f,
            exitFrames = bakodFrames,
            exitFramesPerSecond = 8f,
            exitTint = Color.white,
            exitOpacity = 1f,
            localOffset = Vector3.zero,
            localScale = new Vector3(0.9f, 0.9f, 1f),
            sortingOrderOffset = 1
        });

        ReplaceAbilityVisual(mantsaData, new EnemyAbilityVisualDefinition
        {
            id = EnemyAbilityVisualId.MantsaStain,
            anchor = EnemyAbilityVisualAnchor.GlyphBadge,
            frameMode = EnemyAbilityVisualFrameMode.FullLoop,
            activeSprite = mantsaFrames[3],
            activeTint = Color.white,
            activeOpacity = 0.72f,
            activationFrames = new[] { mantsaFrames[0], mantsaFrames[1], mantsaFrames[2], mantsaFrames[3] },
            activationFramesPerSecond = 12f,
            activationTint = Color.white,
            activationOpacity = 0.88f,
            exitFrames = new[] { mantsaFrames[4], mantsaFrames[5], mantsaFrames[6] },
            exitFramesPerSecond = 12f,
            exitTint = Color.white,
            exitOpacity = 0.88f,
            localOffset = Vector3.zero,
            localScale = new Vector3(0.32f, 0.32f, 1f),
            sortingOrderOffset = 1
        });

        ReplaceHudVisual(aboData, new EnemyHudAbilityVisualDefinition
        {
            id = EnemyHudAbilityVisualId.AshClueFirstSlot,
            activeSprite = ashFrames[3],
            activationFrames = new[] { ashFrames[0], ashFrames[1], ashFrames[2], ashFrames[3] },
            activationFramesPerSecond = 16f,
            activationOpacity = 1f,
            exitFrames = new[] { ashFrames[4], ashFrames[5], ashFrames[6], ashFrames[7] },
            exitFramesPerSecond = 12f,
            exitOpacity = 1f,
            slotSizeMultiplier = new Vector2(2.5f, 2.5f),
            localOffset = Vector2.zero
        });

        EditorUtility.SetDirty(bakodData);
        EditorUtility.SetDirty(mantsaData);
        EditorUtility.SetDirty(aboData);
        AssetDatabase.SaveAssets();
        Debug.Log("Imported and assigned Bakod's barrier, Mantsa's glyph stain, and Abo ng Simula's first-clue-slot ash animations.");
    }

    [MenuItem("Salinlahi/Art/Import And Assign Takip Glyph Cover")]
    public static void ImportAndAssignTakipGlyphCover()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Sprite[] sprites = ImportSpriteSequence(
            TakipOutputFolder,
            TakipFrameNamePrefix,
            8);
        if (sprites == null)
            return;

        EnemyDataSO data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(TakipDataPath);
        if (data == null)
        {
            Debug.LogError($"Could not load Takip EnemyDataSO at {TakipDataPath}.");
            return;
        }

        ReplaceAbilityVisual(data, new EnemyAbilityVisualDefinition
        {
            id = EnemyAbilityVisualId.GlyphCover,
            anchor = EnemyAbilityVisualAnchor.GlyphBadge,
            frameMode = EnemyAbilityVisualFrameMode.FullLoop,
            activeSprite = sprites[0],
            activeTint = Color.white,
            activeOpacity = 1f,
            activationFrames = new[] { sprites[4], sprites[5], sprites[6], sprites[7] },
            activationFramesPerSecond = 8f,
            activationTint = Color.white,
            activationOpacity = 1f,
            exitFrames = new[] { sprites[0], sprites[1], sprites[2], sprites[3] },
            exitFramesPerSecond = 8f,
            exitTint = Color.white,
            exitOpacity = 1f,
            localOffset = new Vector3(0f, 0.36f, 0f),
            localScale = new Vector3(0.38f, 0.38f, 1f),
            sortingOrderOffset = 1
        });

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("Imported and assigned Takip's closed glyph cover, closing frames 05–08, and opening frames 01–04.");
    }

    private static Sprite[] ImportSpriteSequence(string folder, string prefix, int count)
    {
        EnsureAssetFolder(folder);
        var sprites = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            string path = $"{folder}/{prefix}{i + 1:00}.png";
            if (!File.Exists(path))
            {
                Debug.LogError($"Ability art frame is missing: {path}. Extract only the numbered PNG frames from the supplied ZIP first.");
                return null;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"Unity could not find a texture importer for {path}.");
                return null;
            }

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !importer.alphaIsTransparency
                || importer.mipmapEnabled
                || importer.filterMode != FilterMode.Point
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.spritePixelsPerUnit != 96f
                || importer.crunchedCompression
                || textureSettings.spriteAlignment != (int)SpriteAlignment.Center
                || textureSettings.spritePivot != new Vector2(0.5f, 0.5f);

            textureSettings.textureType = TextureImporterType.Sprite;
            textureSettings.spriteMode = (int)SpriteImportMode.Single;
            textureSettings.alphaIsTransparency = true;
            textureSettings.mipmapEnabled = false;
            textureSettings.filterMode = FilterMode.Point;
            textureSettings.spritePixelsPerUnit = 96f;
            textureSettings.spriteAlignment = (int)SpriteAlignment.Center;
            textureSettings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(textureSettings);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.spritePixelsPerUnit = 96f;
            if (changed)
                importer.SaveAndReimport();

            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprites[i] == null)
            {
                Debug.LogError($"Unity did not import {path} as a Sprite.");
                return null;
            }
        }

        return sprites;
    }

    private static void ReplaceAbilityVisual(EnemyDataSO data, EnemyAbilityVisualDefinition replacement)
    {
        var definitions = new List<EnemyAbilityVisualDefinition>();
        if (data.abilityVisuals != null)
        {
            for (int i = 0; i < data.abilityVisuals.Length; i++)
            {
                EnemyAbilityVisualDefinition existing = data.abilityVisuals[i];
                if (existing != null && existing.id != replacement.id)
                    definitions.Add(existing);
            }
        }

        definitions.Add(replacement);
        data.abilityVisuals = definitions.ToArray();
    }

    private static void ReplaceHudVisual(EnemyDataSO data, EnemyHudAbilityVisualDefinition replacement)
    {
        var definitions = new List<EnemyHudAbilityVisualDefinition>();
        if (data.hudAbilityVisuals != null)
        {
            for (int i = 0; i < data.hudAbilityVisuals.Length; i++)
            {
                EnemyHudAbilityVisualDefinition existing = data.hudAbilityVisuals[i];
                if (existing != null && existing.id != replacement.id)
                    definitions.Add(existing);
            }
        }

        definitions.Add(replacement);
        data.hudAbilityVisuals = definitions.ToArray();
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        string[] segments = assetFolder.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = $"{current}/{segments[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }
}
