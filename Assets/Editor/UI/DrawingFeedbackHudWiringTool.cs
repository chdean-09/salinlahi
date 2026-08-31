using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SALIN-163 scene wiring. Creates the supportive-feedback message label and the trace-hint
/// prompt in each gameplay HUD and binds them to <see cref="DrawingFeedback"/>.
///
/// The C# side shipped with both references optional and null-guarded, so the logic was correct
/// but invisible: nothing in either scene rendered the wording, and the hint offer existed only
/// as state. This tool supplies the missing half.
///
/// Two constraints drove the layout:
///  - Both graphics sit over the play column, where the player draws. Every graphic created here
///    sets raycastTarget = false. A full-width label with raycasts left on would silently swallow
///    drawing input, which no unit test would catch.
///  - MassClearBadge already occupies the top band under FullScreenOverlay. The message is placed
///    below it, and the tool asserts the two rects do not overlap rather than trusting the offsets.
/// </summary>
public static class DrawingFeedbackHudWiringTool
{
    private static readonly string[] Scenes =
    {
        "Assets/_Scenes/Gameplay.unity",
        "Assets/_Scenes/Level_01_Tutorial.unity",
    };

    private const string MessageName = "FeedbackMessage";
    private const string PromptName  = "TraceHintPrompt";

    // Below MassClearBadge (top band, 140 tall) with clear separation.
    private const float MessageTopOffset = -320f;
    private const float MessageHeight    = 90f;

    [MenuItem("Salinlahi/SALIN-163/Wire Drawing Feedback HUD")]
    public static void Wire()
    {
        var log = new StringBuilder();

        foreach (string scenePath in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            log.AppendLine($"=== {scenePath} ===");

            var feedback = Object.FindFirstObjectByType<DrawingFeedback>(FindObjectsInactive.Include);
            if (feedback == null) { log.AppendLine("  no DrawingFeedback — skipped"); continue; }

            var overlay = GameObject.Find("HUDCanvas/FullScreenOverlay");
            if (overlay == null) { log.AppendLine("  no HUDCanvas/FullScreenOverlay — skipped"); continue; }

            TMP_FontAsset font = FindFont();

            // Prompt first so it renders BEHIND the message: it is an emphasis pill, not a control.
            var prompt = EnsureChild(overlay.transform, PromptName);
            var promptRect = prompt.GetComponent<RectTransform>();
            Anchor(promptRect, MessageTopOffset + 6f, MessageHeight - 12f, 0.06f);
            var pill = Ensure<Image>(prompt);
            pill.color = new Color(0.10f, 0.08f, 0.02f, 0.72f);
            pill.raycastTarget = false;                 // must never eat drawing input

            var message = EnsureChild(overlay.transform, MessageName);
            var messageRect = message.GetComponent<RectTransform>();
            Anchor(messageRect, MessageTopOffset, MessageHeight, 0f);
            var label = Ensure<TextMeshProUGUI>(message);
            if (font != null) label.font = font;
            label.fontSize = 40f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.text = string.Empty;                  // nothing until the player draws
            label.raycastTarget = false;                // must never eat drawing input

            // Awake() also hides it, but an inactive default keeps the scene honest when opened.
            prompt.SetActive(false);

            var so = new SerializedObject(feedback);
            so.FindProperty("_messageLabel").objectReferenceValue = label;
            so.FindProperty("_traceHintPrompt").objectReferenceValue = prompt;
            so.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine($"  wired _messageLabel    -> {MessageName} (raycastTarget={label.raycastTarget})");
            log.AppendLine($"  wired _traceHintPrompt -> {PromptName} (active={prompt.activeSelf}, raycastTarget={pill.raycastTarget})");
            log.AppendLine(OverlapReport(overlay.transform, messageRect));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("  scene saved");
        }

        Debug.Log(log.ToString());
        System.IO.File.WriteAllText("hud-wiring-report.txt", log.ToString());
    }

    private static TMP_FontAsset FindFont()
    {
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.font != null && t.font.name.Contains("LiberationSans")) return t.font;
        return null;
    }

    /// <summary>Top-anchored, full width, inset horizontally by <paramref name="sideInset"/> of the width.</summary>
    private static void Anchor(RectTransform rt, float topOffset, float height, float sideInset)
    {
        rt.anchorMin = new Vector2(sideInset, 1f);
        rt.anchorMax = new Vector2(1f - sideInset, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, topOffset);
        rt.sizeDelta = new Vector2(0f, height);
        rt.localScale = Vector3.one;
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    /// <summary>Guards the one failure mode tests cannot see: the new label landing on existing HUD.</summary>
    private static string OverlapReport(Transform overlay, RectTransform mine)
    {
        var sb = new StringBuilder("  overlap check vs siblings:");
        Rect a = WorldRect(mine);
        bool any = false;
        foreach (Transform sib in overlay)
        {
            if (sib == mine.transform || sib.name == PromptName) continue;
            var srt = sib as RectTransform;
            if (srt == null || !sib.gameObject.activeSelf) continue;
            Rect b = WorldRect(srt);
            if (a.Overlaps(b)) { sb.Append($"\n    OVERLAPS {sib.name} {b}"); any = true; }
        }
        if (!any) sb.Append(" none ✓");
        sb.Append($"\n    {MessageName} worldRect={a}");
        return sb.ToString();
    }

    private static Rect WorldRect(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return new Rect(c[0].x, c[0].y, c[2].x - c[0].x, c[2].y - c[0].y);
    }
}
