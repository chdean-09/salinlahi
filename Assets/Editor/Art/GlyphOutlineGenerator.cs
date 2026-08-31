using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-209. Generates bare-glyph outline sprites for the tracing guides.
///
/// Every existing sprite on BaybayinCharacterSO is a composed card or framed plate:
/// displaySprite is a learning card carrying the romanised syllable, almanacSprite is an Almanac
/// card, badgeSprite is a scroll-framed plate. Nothing in the project is just the glyph, so the
/// Tracing Dojo guide renders a filled panel over the drawing area and the gameplay trace hint has
/// to settle for a bordered plate.
///
/// Rather than wait on hand-authored art, these are derived from the recognition templates in
/// Resources/Templates — the very point clouds DollarPRecognizer scores against. That makes the
/// guide show exactly the shape the player is being asked to produce, which authored art could
/// only approximate.
///
/// Templates are authored y-up, so Y is flipped into image space. Aspect ratio is preserved
/// rather than stretched to the canvas: HA is essentially one-dimensional (bounding-box aspect
/// 5.53-12.77) and stretching it to a square is precisely the bug that made it unrecognisable.
/// </summary>
public static class GlyphOutlineGenerator
{
    private const string TemplateFolder = "Assets/Resources/Templates";
    private const string OutputFolder   = "Assets/Art/UI/GlyphOutlines";
    private const int    Size           = 256;
    private const float  PaddingFraction = 0.12f;
    private const float  StrokeRadiusPx  = 7.0f;
    private const float  FeatherPx       = 1.6f;

    [MenuItem("Salinlahi/SALIN-209/Generate Glyph Outlines")]
    public static void GenerateAll() => Generate(null);

    /// <summary>Renders only the given IDs. Used to eyeball quality before committing to all 18.</summary>
    public static void GenerateSample() => Generate(new[] { "BA", "HA", "A", "NGA" });

    private static void Generate(string[] onlyIds)
    {
        Directory.CreateDirectory(OutputFolder);
        var log = new StringBuilder("=== glyph outlines ===\n");

        foreach (string id in CharacterIds())
        {
            if (onlyIds != null && System.Array.IndexOf(onlyIds, id) < 0) continue;

            List<List<Vector2>> strokes = LoadFirstTemplate(id);
            if (strokes == null || strokes.Count == 0)
            {
                log.AppendLine($"  {id,-4} NO TEMPLATE — skipped");
                continue;
            }

            Texture2D tex = Render(strokes, out float aspect);
            string path = $"{OutputFolder}/{id}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            int points = 0;
            foreach (var s in strokes) points += s.Count;
            log.AppendLine($"  {id,-4} strokes={strokes.Count} points={points} aspect={aspect:F2} -> {path}");
        }

        AssetDatabase.Refresh();
        Debug.Log(log.ToString());
        File.WriteAllText("glyph-outline-report.txt", log.ToString());
    }

    private static IEnumerable<string> CharacterIds()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:BaybayinCharacterSO",
                     new[] { "Assets/ScriptableObjects/Characters" }))
        {
            var c = AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && !string.IsNullOrWhiteSpace(c.characterID)) yield return c.characterID;
        }
    }

    /// <summary>Template 01 is the reference capture; later variants exist to widen recognition.</summary>
    private static List<List<Vector2>> LoadFirstTemplate(string id)
    {
        string path = $"{TemplateFolder}/{id}_template_01.txt";
        if (!File.Exists(path))
        {
            foreach (string candidate in Directory.GetFiles(TemplateFolder, $"{id}_template_*.txt"))
            { path = candidate; break; }
            if (!File.Exists(path)) return null;
        }
        return StrokeTextParser.ParseStrokes(File.ReadAllText(path));
    }

    private static Texture2D Render(List<List<Vector2>> strokes, out float aspect)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var s in strokes)
            foreach (Vector2 p in s)
            {
                if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
            }

        float w = Mathf.Max(maxX - minX, 1e-5f);
        float h = Mathf.Max(maxY - minY, 1e-5f);
        aspect = Mathf.Max(w, h) / Mathf.Max(Mathf.Min(w, h), 1e-5f);

        // Uniform scale keeps the true proportions; a per-axis fit would flatten HA into a square.
        float pad = Size * PaddingFraction;
        float usable = Size - 2f * pad;
        float scale = usable / Mathf.Max(w, h);
        float offX = pad + (usable - w * scale) * 0.5f;
        float offY = pad + (usable - h * scale) * 0.5f;

        var px = new Color32[Size * Size];   // starts fully transparent

        foreach (var stroke in strokes)
        {
            for (int i = 0; i < stroke.Count; i++)
            {
                Vector2 a = ToPixel(stroke[i], minX, minY, scale, offX, offY);
                Vector2 b = i + 1 < stroke.Count
                    ? ToPixel(stroke[i + 1], minX, minY, scale, offX, offY)
                    : a;
                StampSegment(px, a, b);
            }
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static Vector2 ToPixel(Vector2 p, float minX, float minY, float scale, float offX, float offY)
    {
        float x = offX + (p.x - minX) * scale;
        float y = offY + (p.y - minY) * scale;
        return new Vector2(x, Size - 1 - y);          // templates are y-up; images are y-down
    }

    /// <summary>Distance-to-segment stamp, limited to the segment's neighbourhood so this stays fast.</summary>
    private static void StampSegment(Color32[] px, Vector2 a, Vector2 b)
    {
        float r = StrokeRadiusPx + FeatherPx;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - r));
        int x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + r));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - r));
        int y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + r));

        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float d = DistanceToSegment(new Vector2(x + 0.5f, y + 0.5f), a, b);
            float alpha = Mathf.Clamp01((StrokeRadiusPx + FeatherPx - d) / Mathf.Max(FeatherPx, 1e-4f));
            if (alpha <= 0f) continue;

            int idx = y * Size + x;
            byte existing = px[idx].a;
            byte value = (byte)Mathf.RoundToInt(alpha * 255f);
            if (value > existing) px[idx] = new Color32(255, 255, 255, value);   // white, tint at use site
        }
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-8f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }
}

/// <summary>
/// SALIN-209 second pass: configure the generated PNGs as sprites and assign them to
/// <c>BaybayinCharacterSO.glyphOutlineSprite</c>. Split from generation so the art can be
/// regenerated without re-touching the character assets.
/// </summary>
public static class GlyphOutlineImporter
{
    private const string OutputFolder = "Assets/Art/UI/GlyphOutlines";

    [MenuItem("Salinlahi/SALIN-209/Import And Assign Glyph Outlines")]
    public static void Apply()
    {
        var log = new System.Text.StringBuilder("=== glyph outline import ===\n");
        int configured = 0, assigned = 0, missing = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:BaybayinCharacterSO",
                     new[] { "Assets/ScriptableObjects/Characters" }))
        {
            var character = AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (character == null || string.IsNullOrWhiteSpace(character.characterID)) continue;

            string path = $"{OutputFolder}/{character.characterID}.png";
            if (!System.IO.File.Exists(path))
            {
                log.AppendLine($"  {character.characterID,-4} NO OUTLINE PNG");
                missing++;
                continue;
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;   // a guide, not pixel art
                importer.SaveAndReimport();
                configured++;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var so = new SerializedObject(character);
            so.FindProperty("glyphOutlineSprite").objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(character);
            if (sprite != null) assigned++;

            log.AppendLine($"  {character.characterID,-4} assigned={(sprite != null)}");
        }

        AssetDatabase.SaveAssets();
        log.AppendLine($"  -- configured={configured} assigned={assigned} missingPng={missing}");
        Debug.Log(log.ToString());
        System.IO.File.WriteAllText("glyph-import-report.txt", log.ToString());
    }
}
