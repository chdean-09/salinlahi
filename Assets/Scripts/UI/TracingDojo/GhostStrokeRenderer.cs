using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class GhostStrokeRenderer : MonoBehaviour
{
    [SerializeField] private RectTransform _canvasArea;
    [SerializeField] private GameObject _dotPrefab;   // UI Image, 8x8, ghost alpha
    [SerializeField, Range(0f, 1f)] private float _ghostAlpha = 0.35f;

    private readonly List<GameObject> _activeDots = new List<GameObject>();

    public void Render(BaybayinCharacterSO character)
    {
        Clear();
        if (character == null) return;

        var template = Resources.Load<TextAsset>(
            $"Templates/{character.characterID}_template_01");
        if (template == null)
        {
            Debug.LogWarning(
                $"GhostStrokeRenderer: no template for {character.characterID}");
            return;
        }

        var points = Parse(template.text);
        var rect = _canvasArea.rect;

        foreach (var p in points)
        {
            var dot = Instantiate(_dotPrefab, _canvasArea);
            var rt = dot.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, p.x),
                Mathf.Lerp(rect.yMin, rect.yMax, p.y));
            var img = dot.GetComponent<UnityEngine.UI.Image>();
            var c = img.color; c.a = _ghostAlpha; img.color = c;
            _activeDots.Add(dot);
        }
    }

    public void Clear()
    {
        foreach (var d in _activeDots) Destroy(d);
        _activeDots.Clear();
    }

    private static List<Vector2> Parse(string text)
    {
        var list = new List<Vector2>();
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            var parts = trimmed.Split(',');
            if (parts.Length < 2) continue;
            if (float.TryParse(parts[0], NumberStyles.Float,
                               CultureInfo.InvariantCulture, out var x)
                && float.TryParse(parts[1], NumberStyles.Float,
                                  CultureInfo.InvariantCulture, out var y))
            {
                list.Add(new Vector2(x, y));
            }
        }
        return list;
    }
}
