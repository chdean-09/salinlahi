using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Audits every TextMeshProUGUI and legacy Text in the build scenes and
/// Assets/Prefabs against the UITextScale floors, then optionally raises
/// undersized components in place so Unity writes the serialized diffs.
///
/// Report mode only logs; Enforce mode edits and saves. Run the report first and
/// eyeball the flagged OVERFLOW-RISK rows after enforcing: the tool raises font
/// size but cannot judge whether a rect that was authored for small text now clips.
/// </summary>
public static class FontReadabilityAudit
{
    // Repo root must stay clean: the report goes to the OS temp dir, never Assets/.
    private static string ReportFilePath => System.IO.Path.Combine(
        Application.temporaryCachePath, "font-readability-report.txt");
    private const float OverflowHeightRatio = 1.25f;

    // Deliberately small: name-based rules below cover most objects; this map is for
    // names whose role the rules would misjudge.
    private static readonly Dictionary<string, float> FloorOverrides = new()
    {
        { "TapCatcher", UITextScale.Body },
        { "TimerBarLabel", UITextScale.Caption },
        { "RejectFlash", UITextScale.Caption },
    };

    private sealed class Violation
    {
        public string Asset;
        public string Path;
        public string Kind;
        public float Size;
        public float SizeMin;
        public float SizeMax;
        public bool AutoSize;
        public float Floor;
        public bool OverflowRisk;
        public bool Applied;
    }

    [MenuItem("Salinlahi/UI/Font Readability Report")]
    public static void Report()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("FontReadabilityAudit: exit Play Mode before scanning.");
            return;
        }
        Run(apply: false);
    }

    [MenuItem("Salinlahi/UI/Enforce Font Readability Floors")]
    public static void Enforce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("FontReadabilityAudit: exit Play Mode before enforcing.");
            return;
        }
        // Standard pre-modification gate: Save / Don't Save continue, Cancel aborts.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Run(apply: true);
    }



    private static void Run(bool apply)
    {
        var violations = new List<Violation>();
        int scanned = 0;

        // Scenes are opened additively so the user's current setup is never disturbed:
        // a scene that was already loaded stays loaded and is left untouched on close.
        ScanScenes(violations, ref scanned, apply);
        ScanPrefabs(violations, ref scanned, apply);

        EmitReport(violations, scanned, apply);
    }

    private static void ScanScenes(List<Violation> violations, ref int scanned, bool apply)
    {
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled)
                continue;

            bool alreadyLoaded = SceneManager.GetSceneByPath(entry.path).isLoaded;
            Scene scene = alreadyLoaded
                ? SceneManager.GetSceneByPath(entry.path)
                : EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);

            bool dirty = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // TextMeshProUGUI only — world-space TextMeshPro (debug labels on enemies,
                // world markers) is intentionally outside the readability floors.
                foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    scanned++;
                    dirty |= Evaluate(text, entry.path, violations, apply);
                }
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                {
                    scanned++;
                    dirty |= Evaluate(text, entry.path, violations, apply);
                }
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            if (!alreadyLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    private static void ScanPrefabs(List<Violation> violations, ref int scanned, bool apply)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    scanned++;
                    changed |= Evaluate(text, path, violations, apply);
                }
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                {
                    scanned++;
                    changed |= Evaluate(text, path, violations, apply);
                }


                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static bool Evaluate(TMP_Text text, string asset, List<Violation> violations, bool apply)
    {
        float floor = ResolveFloor(text.transform, text.gameObject.name);
        float targetMin = Mathf.Max(text.fontSizeMin, UITextScale.AutoSizeFloor);
        float targetMax = Mathf.Max(text.fontSizeMax, floor);
        float targetSize = Mathf.Max(text.fontSize, floor);

        bool violates = text.enableAutoSizing
            ? text.fontSizeMin < UITextScale.AutoSizeFloor || text.fontSizeMax < floor
            : text.fontSize < floor;
        if (!violates)
            return false;

        var v = new Violation
        {
            Asset = asset,
            Path = HierarchyPath(text.transform),
            Kind = "TMP",
            Size = text.fontSize,
            SizeMin = text.fontSizeMin,
            SizeMax = text.fontSizeMax,
            AutoSize = text.enableAutoSizing,
            Floor = floor,
            OverflowRisk = WouldOverflow(text.rectTransform, targetMax > targetSize ? targetMax : targetSize),
        };
        violations.Add(v);

        if (apply)
        {
            Undo.RecordObject(text, "Enforce Font Readability Floor");
            if (text.enableAutoSizing)
            {
                text.fontSizeMin = targetMin;
                text.fontSizeMax = targetMax;
            }
            text.fontSize = targetSize;
            EditorUtility.SetDirty(text);
            v.Applied = true;
        }
        return apply;
    }

    private static bool Evaluate(Text text, string asset, List<Violation> violations, bool apply)
    {
        float floor = ResolveFloor(text.transform, text.gameObject.name);
        if (text.fontSize >= floor)
            return false;

        var v = new Violation
        {
            Asset = asset,
            Path = HierarchyPath(text.transform),
            Kind = "uGUI",
            Size = text.fontSize,
            Floor = floor,
            OverflowRisk = WouldOverflow(text.rectTransform, floor),
        };
        violations.Add(v);

        if (apply)
        {
            Undo.RecordObject(text, "Enforce Font Readability Floor");
            text.fontSize = Mathf.RoundToInt(floor);
            EditorUtility.SetDirty(text);
            v.Applied = true;
        }
        return apply;
    }

    private static float ResolveFloor(Transform transform, string objectName)
    {
        if (FloorOverrides.TryGetValue(objectName, out float floor))
            return floor;
        if (transform.GetComponentInParent<Button>() != null)
            return UITextScale.Body;
        if (ContainsAny(objectName, "Title", "Banner", "Heading"))
            return UITextScale.Title;
        if (ContainsAny(objectName, "Body", "Content", "Speaker", "Feedback"))
            return UITextScale.Body;
        if (ContainsAny(objectName, "Label", "Counter", "Count", "Star", "Timer", "Subtitle", "Header"))
            return UITextScale.Secondary;
        return UITextScale.Caption;
    }

    private static bool ContainsAny(string name, params string[] tokens)
    {
        foreach (string token in tokens)
        {
            if (name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static bool WouldOverflow(RectTransform rect, float size)
    {
        return rect != null && rect.rect.height > 0f && rect.rect.height < size * OverflowHeightRatio;
    }

    private static string HierarchyPath(Transform transform)
    {
        var sb = new StringBuilder(transform.name);
        for (Transform p = transform.parent; p != null; p = p.parent)
            sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    private static void EmitReport(List<Violation> violations, int scanned, bool applied)
    {
        var sb = new StringBuilder();
        sb.AppendLine(applied ? "Font readability floors ENFORCED" : "Font readability report (no changes)");
        sb.AppendLine($"Floors: caption {UITextScale.Caption} / secondary {UITextScale.Secondary} / body {UITextScale.Body} / title {UITextScale.Title} / autosize min {UITextScale.AutoSizeFloor}");
        sb.AppendLine($"Scanned {scanned} text components; {violations.Count} below floor.");
        sb.AppendLine();
        foreach (Violation v in violations)
        {
            sb.AppendLine($"{v.Asset} | {v.Path} | {v.Kind} size={v.Size} min={v.SizeMin} max={v.SizeMax} auto={v.AutoSize} -> floor {v.Floor}"
                + (v.OverflowRisk ? "  OVERFLOW-RISK" : string.Empty)
                + (applied ? (v.Applied ? "  APPLIED" : "  SKIPPED") : string.Empty));
        }
        if (violations.Exists(v => v.OverflowRisk))
            sb.AppendLine().AppendLine("OVERFLOW-RISK rows: the rect was authored for smaller text — open the asset and confirm the label still fits.");

        string report = sb.ToString();
        Debug.Log(report);
        System.IO.File.WriteAllText(ReportFilePath, report);
    }
}
