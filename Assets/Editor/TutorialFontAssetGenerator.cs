using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

public static class TutorialFontAssetGenerator
{
    private const string SourceFontPath = "Assets/Art/UI/Fonts/VT323-Regular.ttf";
    private const string OutputDirectory = "Assets/Resources/Fonts";
    private const string OutputAssetPath = OutputDirectory + "/TutorialFont.asset";

    [MenuItem("Salinlahi/Tutorial/Generate VT323 Font Asset")]
    public static void Generate()
    {
        if (!File.Exists(SourceFontPath))
        {
            EditorUtility.DisplayDialog(
                "Font Asset Generator",
                $"Source font not found:\n{SourceFontPath}",
                "OK");
            return;
        }

        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog(
                "Font Asset Generator",
                "TMP Essential Resources are not imported. Please import them first.",
                "OK");
            return;
        }

        if (!Directory.Exists(OutputDirectory))
            Directory.CreateDirectory(OutputDirectory);

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            EditorUtility.DisplayDialog(
                "Font Asset Generator",
                "Failed to load font asset from path. Try reimporting the .ttf first.",
                "OK");
            return;
        }

        // Delete existing asset if present
        if (File.Exists(OutputAssetPath))
        {
            AssetDatabase.DeleteAsset(OutputAssetPath);
            AssetDatabase.Refresh();
        }

        // Create the font asset using the official API (handles all internal state correctly)
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            samplingPointSize: 90,
            atlasPadding: 9,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 1024,
            atlasHeight: 1024,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            EditorUtility.DisplayDialog(
                "Font Asset Generator",
                "Failed to create TMP Font Asset.",
                "OK");
            return;
        }

        // Create the main asset
        AssetDatabase.CreateAsset(fontAsset, OutputAssetPath);

        // Add atlas textures as sub-assets so they persist
        for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
        {
            Texture2D tex = fontAsset.atlasTextures[i];
            if (tex != null)
            {
                tex.name = "VT323 Atlas";
                AssetDatabase.AddObjectToAsset(tex, fontAsset);
            }
        }

        // Add material as sub-asset so it persists
        if (fontAsset.material != null)
        {
            fontAsset.material.name = "VT323 Atlas Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Font Asset Generator",
            $"Successfully created tutorial font asset at:\n{OutputAssetPath}",
            "OK");

        Debug.Log($"[Salinlahi] Created tutorial font asset: {OutputAssetPath}");
    }
}
