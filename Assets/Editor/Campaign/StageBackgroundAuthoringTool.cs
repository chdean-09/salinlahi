using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the three StageBackgroundSO assets from the manifests that
/// scripts/art/generate_stage_backgrounds.py writes next to its sprites, and wires each
/// onto its era theme. Idempotent: existing assets are mutated in place so their GUIDs
/// and references survive (CreateAsset would reissue them).
///
/// The manifest, not this file, is where element weights live, so the Python
/// generator and Unity agree by construction.
/// </summary>
public static class StageBackgroundAuthoringTool
{
    private const string TilesetRoot = "Assets/Art/Environment/Tileset";
    private const string ThemesDir = "Assets/ScriptableObjects/Themes";

    // Stage folder -> the era theme asset that should show it. The theme names are the
    // legacy colonial-era names; the campaign eras Ugat/Ugnayan/Pamana map onto them in
    // order, and the walls (map01/02/03) already do.
    private static readonly (string stage, string theme)[] Wiring =
    {
        ("Ugat", "EraTheme_Spanish"),
        ("Ugnayan", "EraTheme_Japanese"),
        ("Pamana", "EraTheme_American"),
    };

    [Serializable] private class ManifestScatter { public string file; public float weight; public bool flippable; }

    [Serializable]
    private class Manifest
    {
        public string stage;
        public string[] ground;
        public string[] worn;
        public ManifestScatter[] scatter;
        public string[] marginLeft;
        public string[] marginRight;
        public float scatterPerTile;
        public int scatterMinDistancePx;
    }

    [MenuItem("Salinlahi/Content/Author Stage Backgrounds")]
    public static void Author()
    {
        foreach ((string stage, string theme) in Wiring)
        {
            string manifestPath = $"{TilesetRoot}/{stage}/manifest.json";
            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"STAGEBG: no manifest at {manifestPath}; run scripts/art/generate_stage_backgrounds.py first.");
                continue;
            }
            Manifest m = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            string assetPath = $"{ThemesDir}/StageBackground_{stage}.asset";
            var so = AssetDatabase.LoadAssetAtPath<StageBackgroundSO>(assetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<StageBackgroundSO>();
                AssetDatabase.CreateAsset(so, assetPath);
            }

            so.groundTiles = LoadSprites(stage, m.ground);
            so.wornTiles = LoadSprites(stage, m.worn);
            so.marginLeft = LoadSprites(stage, m.marginLeft);
            so.marginRight = LoadSprites(stage, m.marginRight);
            so.scatterPerTile = m.scatterPerTile;
            so.scatterMinDistancePx = m.scatterMinDistancePx;
            so.scatter = new StageBackgroundSO.ScatterEntry[m.scatter.Length];
            for (int i = 0; i < m.scatter.Length; i++)
            {
                so.scatter[i] = new StageBackgroundSO.ScatterEntry
                {
                    sprite = LoadSprite(stage, m.scatter[i].file),
                    weight = m.scatter[i].weight,
                    flippable = m.scatter[i].flippable,
                };
            }
            EditorUtility.SetDirty(so);

            var era = AssetDatabase.LoadAssetAtPath<EraThemeSO>($"{ThemesDir}/{theme}.asset");
            if (era == null)
            {
                Debug.LogError($"STAGEBG: theme {theme} not found; {stage} built but not wired.");
            }
            else
            {
                era.stageBackground = so;
                EditorUtility.SetDirty(era);
            }

            Debug.Log($"STAGEBG: {stage} ground={so.groundTiles.Length} worn={so.wornTiles.Length} scatter={so.scatter.Length} " +
                      $"margins={so.marginLeft.Length}+{so.marginRight.Length} complete={so.IsComplete} -> {theme}");
        }
        AssetDatabase.SaveAssets();
    }

    private static Sprite[] LoadSprites(string stage, string[] files)
    {
        if (files == null) return Array.Empty<Sprite>();
        var result = new Sprite[files.Length];
        for (int i = 0; i < files.Length; i++) result[i] = LoadSprite(stage, files[i]);
        return result;
    }

    private static Sprite LoadSprite(string stage, string file)
    {
        string path = $"{TilesetRoot}/{stage}/{file}";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Debug.LogError($"STAGEBG: sprite missing or not imported as a sprite: {path}");
        return sprite;
    }
}
