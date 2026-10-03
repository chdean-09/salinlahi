using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the tracing guide under the player's strokes in the Tracing Dojo.
///
/// Renders <see cref="BaybayinCharacterSO.glyphOutlineSprite"/> — a bare glyph on a transparent
/// background, generated from the recognition templates, so the guide is exactly the shape the
/// recognizer scores against (SALIN-209).
///
/// It previously rendered displaySprite, which is a learning CARD: at 35% alpha the card's filled
/// panel washed across the whole tracing area instead of leaving an outline to trace over, and it
/// carried the romanised syllable along with it. displaySprite remains the fallback for any
/// character with no outline art, so nothing breaks if art is missing.
/// </summary>
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
        Sprite guide = ResolveGuideSprite(character);
        if (guide == null)
        {
            if (character != null)
                Debug.LogWarning(
                    $"GhostStrokeRenderer: no glyph art for {character.characterID}");
            Clear();
            return;
        }

        _ghostImage.sprite = guide;
        _ghostImage.color = new Color(1f, 1f, 1f, _ghostAlpha);
        _ghostImage.enabled = true;
    }

    /// <summary>Bare outline first; the learning card only if a character has no outline art yet.</summary>
    private static Sprite ResolveGuideSprite(BaybayinCharacterSO character)
    {
        if (character == null) return null;
        return character.glyphOutlineSprite != null ? character.glyphOutlineSprite : character.displaySprite;
    }

    public void Clear()
    {
        if (_ghostImage != null) _ghostImage.enabled = false;
    }
}
