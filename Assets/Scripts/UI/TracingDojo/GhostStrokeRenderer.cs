using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the tracing guide under the player's strokes in the Tracing Dojo.
///
/// KNOWN LIMITATION (measured 2026-08-31, SALIN-163): this renders
/// <see cref="BaybayinCharacterSO.displaySprite"/>, which is a learning CARD rather than a bare
/// glyph — a filled panel carrying the glyph and its romanised syllable. At 35% alpha the card's
/// background washes across the whole tracing area instead of leaving a faint outline to trace
/// over, and the romanisation rides along with it.
///
/// It was left as-is because the alternatives are no better: badgeSprite is a framed plate and
/// almanacSprite is another card. No bare-glyph art exists in the project, so fixing this properly
/// needs new art (a transparent-background glyph per character), not a different field.
///
/// The romanisation is defensible HERE — the player picked the character they are practising, so
/// nothing is given away. It is not defensible in gameplay, which is why
/// <c>TraceHintPresenter</c> deliberately uses badgeSprite instead.
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
