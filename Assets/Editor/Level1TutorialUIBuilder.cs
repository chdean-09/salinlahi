using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates and configures tutorial-specific UI elements (guide visuals, text, buttons).
/// Used by Level1TutorialSceneBuilder.
/// </summary>
public static class Level1TutorialUIBuilder
{
    public static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        Vector2 anchor,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject go = new(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 96f);
        rect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.text = string.Empty;
        return text;
    }

    public static Button CreateButton(Transform parent)
    {
        GameObject go = new("SkipButton");
        Undo.RegisterCreatedObjectUndo(go, "Create Skip Button");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(160f, 56f);
        rect.anchoredPosition = new Vector2(-32f, -32f);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.65f);
        Button button = go.AddComponent<Button>();

        TextMeshProUGUI label = CreateText(go.transform, "Label", new Vector2(0.5f, 0.5f), 26, TextAlignmentOptions.Center);
        label.text = "Skip";
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        go.SetActive(false);
        return button;
    }

    public static Image CreateGuideSpriteImage(Transform parent)
    {
        GameObject go = new("GuideSpriteImage");
        Undo.RegisterCreatedObjectUndo(go, "Create Guide Sprite Image");
        go.transform.SetParent(parent, false);
        go.SetActive(false);

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = new Color(1f, 1f, 1f, 0.9f);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 120f);
        rect.sizeDelta = new Vector2(180f, 180f);

        return image;
    }

    public static LineRenderer EnsureGuidePathRenderer(Transform parent)
    {
        GameObject go = new("GuidePathRenderer");
        Undo.RegisterCreatedObjectUndo(go, "Create Guide Path Renderer");
        go.transform.SetParent(parent, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.positionCount = 0;
        lr.enabled = false;

        Material mat = new(Shader.Find("Sprites/Default"));
        if (mat != null)
        {
            mat.color = new Color(0.49f, 0.09f, 0.56f, 1f); // #7d168f
            lr.material = mat;
        }

        return lr;
    }

    public static Transform EnsureGuideDot(Transform parent, string name, Color color)
    {
        GameObject go = new(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);
        go.SetActive(false);

        Image image = go.AddComponent<Image>();
        image.color = color;
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(26f, 26f);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        return go.transform;
    }

    public static Transform EnsureGuideArrow(Transform parent)
    {
        GameObject go = new("DirectionArrow");
        Undo.RegisterCreatedObjectUndo(go, "Create Direction Arrow");
        go.transform.SetParent(parent, false);
        go.SetActive(false);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.09f, 0.54f, 0.29f, 1f); // #168a4a
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(40f, 20f);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        return go.transform;
    }

    public static void RepairGuideTextPositions(Transform guideRoot)
    {
        Transform prompt = guideRoot.Find("PromptText");
        if (prompt != null && prompt.TryGetComponent(out RectTransform promptRect))
        {
            Undo.RecordObject(promptRect, "Repair PromptText Position");
            promptRect.anchorMin = new Vector2(0.5f, 0.32f);
            promptRect.anchorMax = new Vector2(0.5f, 0.32f);
            EditorUtility.SetDirty(promptRect);
        }

        Transform feedback = guideRoot.Find("FeedbackText");
        if (feedback != null && feedback.TryGetComponent(out RectTransform feedbackRect))
        {
            Undo.RecordObject(feedbackRect, "Repair FeedbackText Position");
            feedbackRect.anchorMin = new Vector2(0.5f, 0.24f);
            feedbackRect.anchorMax = new Vector2(0.5f, 0.24f);
            EditorUtility.SetDirty(feedbackRect);
        }
    }
}
