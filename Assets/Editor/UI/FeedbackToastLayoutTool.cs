using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SALIN-211. Resizes the Tracing Dojo feedback toast for sentence-length wording.
///
/// The encouragement label was `_confidenceLabel` and rendered "83%" — three characters. SALIN-163
/// replaced the score with a full sentence and kept the scene binding through
/// [FormerlySerializedAs], but nobody resized the rect. In play the sentence wrapped onto five
/// narrow lines and ran over the verdict label above it.
/// </summary>
public static class FeedbackToastLayoutTool
{
    private const string ScenePath = "Assets/_Scenes/TracingDojo.unity";

    public static void Report() => Run(apply: false);
    public static void Fix()    => Run(apply: true);

    private static void Run(bool apply)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var log = new StringBuilder($"=== {ScenePath} (apply={apply}) ===\n");

        var toast = Object.FindFirstObjectByType<FeedbackToast>(FindObjectsInactive.Include);
        if (toast == null) { Debug.Log(log + "  no FeedbackToast found"); return; }

        var so = new SerializedObject(toast);
        var verdict = so.FindProperty("_verdictLabel").objectReferenceValue as TMP_Text;
        var encouragement = so.FindProperty("_encouragementLabel").objectReferenceValue as TMP_Text;

        log.AppendLine($"  toast root: {Path(toast.transform)}");
        Describe(log, "verdict", verdict);
        Describe(log, "encouragement", encouragement);

        if (apply && encouragement != null)
        {
            // Measured before: both labels were 200x50 at fontSize 42 -- geometry for "83%".
            // Verdict is top-anchored, encouragement bottom-anchored, so stretch each across the
            // toast and give the sentence real height instead of a 200px box.
            // The toast root was sized for two 50px labels stacked in a short box; with a real
            // sentence the two bands met in the middle. Give the root enough height first.
            var root = (RectTransform)toast.transform;
            if (root.rect.height < 132f)
                root.sizeDelta = new Vector2(root.sizeDelta.x, 132f);

            var rt = encouragement.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 8f);
            rt.sizeDelta = new Vector2(-32f, 62f);          // 16px inset each side

            encouragement.enableAutoSizing = true;           // longest wording still fits
            encouragement.fontSizeMin = 16f;
            encouragement.fontSizeMax = 28f;
            encouragement.enableWordWrapping = true;
            encouragement.alignment = TextAlignmentOptions.Bottom;
            encouragement.name = "EncouragementLabel";       // was ConfidenceLabel; binding is by fileID

            if (verdict != null)
            {
                var vrt = verdict.rectTransform;
                vrt.anchorMin = new Vector2(0f, 1f);
                vrt.anchorMax = new Vector2(1f, 1f);
                vrt.pivot = new Vector2(0.5f, 1f);
                vrt.anchoredPosition = new Vector2(0f, -8f);
                vrt.sizeDelta = new Vector2(-32f, 48f);
            }

            log.AppendLine("  -- after --");
            Describe(log, "verdict", verdict);
            Describe(log, "encouragement", encouragement);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("  scene saved");
        }

        Debug.Log(log.ToString());
        System.IO.File.WriteAllText("toast-layout-report.txt", log.ToString());
    }

    private static void Describe(StringBuilder sb, string name, TMP_Text t)
    {
        if (t == null) { sb.AppendLine($"  {name}: NULL"); return; }
        var rt = t.rectTransform;
        sb.AppendLine($"  {name,-14} '{Path(t.transform)}' anchoredPos={rt.anchoredPosition} " +
                      $"sizeDelta={rt.sizeDelta} anchors={rt.anchorMin}-{rt.anchorMax} " +
                      $"pivot={rt.pivot} fontSize={t.fontSize} autoSize={t.enableAutoSizing}");
    }

    private static string Path(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }
}
