using UnityEngine;
using UnityEngine.UI;

public class GhostStrokeRenderer : MonoBehaviour
{
    [SerializeField] private RectTransform _canvasArea;
    [SerializeField, Range(0f, 1f)] private float _ghostAlpha = 0.35f;

    private Image _ghostImage;

    private void Awake()
    {
        var go = new GameObject(
            "GhostSprite", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_canvasArea, worldPositionStays: false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _ghostImage = go.GetComponent<Image>();
        _ghostImage.raycastTarget = false;
        _ghostImage.preserveAspect = true;
        _ghostImage.enabled = false;
    }

    public void Render(BaybayinCharacterSO character)
    {
        if (character == null || character.displaySprite == null)
        {
            if (character != null)
                Debug.LogWarning(
                    $"GhostStrokeRenderer: no displaySprite for {character.characterID}");
            Clear();
            return;
        }

        _ghostImage.sprite = character.displaySprite;
        _ghostImage.color = new Color(1f, 1f, 1f, _ghostAlpha);
        _ghostImage.enabled = true;
    }

    public void Clear()
    {
        if (_ghostImage != null) _ghostImage.enabled = false;
    }
}
