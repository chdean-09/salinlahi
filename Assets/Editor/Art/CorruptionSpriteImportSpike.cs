using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validation spike for the corruption-enemy sprite sheets (2048x2048, a 2x2 grid of
/// 1024x1024 frames). Applies the proposed import contract to one representative sheet and
/// reports the measured result, so the approach can be judged before it is applied broadly.
///
/// Proposed contract, derived from the existing enemy pipeline:
///   - Sprite (2D and UI), Multiple, sliced into 4 frames of 1024x1024
///   - Pixels Per Unit 192, so 1024px spans 5.333 world units - identical to the 32x32
///     standard-enemy frame at the project-wide PPU of 6. Sprite size stays the size
///     hierarchy, exactly as it is today.
///   - Bilinear filtering and center pivot, matching every existing enemy sheet.
/// </summary>
public static class CorruptionSpriteImportSpike
{
    private const string SheetPath =
        "Assets/Art/Characters/Enemies/Corruption/sprite_enemy_mantsa_walk-Sheet.png";

    private const int FrameSize = 1024;
    private const int Columns = 2;
    private const int Rows = 2;

    // 1024 / 192 = 5.333 world units, matching a 32px frame at the project's PPU of 6.
    private const float TargetPixelsPerUnit = 192f;

    public static void Run()
    {
        var report = new StringBuilder();
        var importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[SPIKE] No TextureImporter at " + SheetPath);
            EditorApplication.Exit(2);
            return;
        }

        report.AppendLine("BEFORE");
        report.AppendLine(Describe(importer));

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = TargetPixelsPerUnit;
        importer.filterMode = FilterMode.Bilinear;   // matches every existing enemy sheet
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
                    name = "mantsa_walk_" + index.ToString("00"),
                    // Unity's texture origin is bottom-left, so invert the row for frame order.
                    rect = new Rect(column * FrameSize, (Rows - 1 - row) * FrameSize, FrameSize, FrameSize),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                });
            }
        }

#pragma warning disable CS0618 // SpriteMetaData is obsolete but avoids an asmdef dependency for this spike.
        importer.spritesheet = rects.ToArray();
#pragma warning restore CS0618

        importer.SaveAndReimport();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.AppendLine("AFTER");
        report.AppendLine(Describe(importer));

        report.AppendLine();
        report.AppendLine("MEASURED SPRITES");
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(SheetPath);
        int found = 0;
        foreach (Object asset in all)
        {
            if (asset is not Sprite sprite)
                continue;

            found++;
            Bounds b = sprite.bounds;
            report.AppendLine(
                $"  {sprite.name}: rect={sprite.rect.width}x{sprite.rect.height} " +
                $"ppu={sprite.pixelsPerUnit} worldSize={b.size.x:F3}x{b.size.y:F3} " +
                $"pivot=({sprite.pivot.x / sprite.rect.width:F3},{sprite.pivot.y / sprite.rect.height:F3})");
        }

        report.AppendLine($"  sprite count = {found} (expected {Columns * Rows})");

        string outPath = System.Environment.GetEnvironmentVariable("SPIKE_OUT") ?? "spike_report.txt";
        File.WriteAllText(outPath, report.ToString());
        Debug.Log("[SPIKE] wrote " + outPath);
        EditorApplication.Exit(found == Columns * Rows ? 0 : 3);
    }

    private static string Describe(TextureImporter importer)
    {
        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        return
            $"  textureType={importer.textureType} spriteImportMode={importer.spriteImportMode}\n" +
            $"  pixelsPerUnit={importer.spritePixelsPerUnit} filterMode={importer.filterMode}\n" +
            $"  alphaIsTransparency={importer.alphaIsTransparency} mipmaps={importer.mipmapEnabled}\n" +
            $"  maxTextureSize={importer.maxTextureSize} compression={importer.textureCompression}\n" +
            $"  spriteAlignment={settings.spriteAlignment} npotScale={importer.npotScale}";
    }
}
