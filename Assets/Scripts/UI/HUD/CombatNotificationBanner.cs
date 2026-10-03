using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reserves separate HUD bands for drawing feedback and mechanic reminders.</summary>
public sealed class CombatNotificationBanner : MonoBehaviour
{
    private TMP_Text _label;
    private CanvasGroup _group;

    public static void Configure(TMP_Text label, float bottom, CanvasGroup visibilityOwner = null)
    {
        if (label == null || label.GetComponentInParent<CombatNotificationBanner>() != null)
            return;

        Canvas canvas = label.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Transform parent = canvas.transform.Find("HUDLayer");
        if (parent == null) parent = canvas.transform;

        GameObject root = visibilityOwner != null ? visibilityOwner.gameObject
            : new GameObject(label.name + "Banner", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        if (root.GetComponent<CombatNotificationBanner>() == null)
            root.AddComponent<CombatNotificationBanner>();
        root.transform.SetParent(parent, false);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, bottom);
        rect.anchorMax = new Vector2(0.92f, bottom + 0.06f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        label.transform.SetParent(root.transform, false);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(16f, 8f);
        label.rectTransform.offsetMax = new Vector2(-16f, -8f);
        label.rectTransform.localScale = Vector3.one;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Truncate;
        label.enableAutoSizing = true;
        label.fontSizeMin = UITextScale.AutoSizeFloor;
        label.fontSizeMax = UITextScale.Secondary;
        label.color = Color.white;
        label.raycastTarget = false;
        TutorialFontProvider.ApplyTo(label);

        var banner = root.GetComponent<CombatNotificationBanner>();
        banner._label = label;
        Image background = root.GetComponent<Image>();
        if (background == null) background = root.AddComponent<Image>();
        background.color = ScrollPanelArt.FlatPanelColor;
        background.raycastTarget = false;
        banner._group = root.GetComponent<CanvasGroup>();
        banner._group.alpha = 0f;
        banner._group.blocksRaycasts = false;
        banner._group.interactable = false;
    }

    private void LateUpdate()
    {
        if (_group == null) return;
        bool visible = _label != null && _label.isActiveAndEnabled
            && !string.IsNullOrWhiteSpace(_label.text);
        _group.alpha = Mathf.MoveTowards(_group.alpha, visible ? 1f : 0f,
            Time.unscaledDeltaTime / 0.15f);
    }
}
