using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A sprite-free, mesh-drawn torn edge used by Punit's clue effect. Its jagged central strip sits
/// in the gap between the two moving clue fragments and fades away as the text repairs.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProceduralClueTearGraphic : MaskableGraphic
{
    private static readonly float[] EdgeOffsets = { 0f, -0.65f, 0.55f, -0.45f, 0.7f, -0.55f, 0.4f, -0.7f, 0.55f, -0.35f, 0f };

    private float _splitX;
    private float _topY;
    private float _bottomY;
    private float _gapWidth = 14f;
    private float _progress;

    public float Progress => _progress;
    public bool IsVisible => enabled && _progress > 0.001f;

    /// <summary>Sets the torn seam in this graphic's local coordinate space.</summary>
    public void SetTear(float splitX, float topY, float bottomY, float gapWidth, float progress)
    {
        _splitX = splitX;
        _topY = Mathf.Max(topY, bottomY);
        _bottomY = Mathf.Min(topY, bottomY);
        _gapWidth = Mathf.Max(4f, gapWidth);
        _progress = Mathf.Clamp01(progress);
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (_progress <= 0.001f || _topY - _bottomY <= 1f)
            return;

        const int segmentCount = 10;
        float halfGap = _gapWidth * 0.5f;
        float jaggedness = Mathf.Min(5f, halfGap * 0.55f) * _progress;
        Color32 dark = WithAlpha(new Color32(43, 28, 48, 225), _progress);
        Color32 parchment = WithAlpha(new Color32(239, 205, 128, 240), _progress);

        for (int i = 0; i < segmentCount; i++)
        {
            float y0 = Mathf.Lerp(_bottomY, _topY, i / (float)segmentCount);
            float y1 = Mathf.Lerp(_bottomY, _topY, (i + 1) / (float)segmentCount);
            float jag0 = EdgeOffsets[i] * jaggedness;
            float jag1 = EdgeOffsets[i + 1] * jaggedness;

            float left0 = _splitX - halfGap + jag0;
            float right0 = _splitX + halfGap - jag0 * 0.35f;
            float left1 = _splitX - halfGap + jag1;
            float right1 = _splitX + halfGap - jag1 * 0.35f;

            AddQuad(vertexHelper,
                new Vector2(left0, y0), new Vector2(right0, y0),
                new Vector2(left1, y1), new Vector2(right1, y1), dark);

            // Two warm ragged highlights make both torn faces readable against the translucent
            // dark center while staying inside the gap and clear of the glyphs.
            AddQuad(vertexHelper,
                new Vector2(left0, y0), new Vector2(left0 + 1.5f, y0),
                new Vector2(left1, y1), new Vector2(left1 + 1.5f, y1), parchment);
            AddQuad(vertexHelper,
                new Vector2(right0 - 1.5f, y0), new Vector2(right0, y0),
                new Vector2(right1 - 1.5f, y1), new Vector2(right1, y1), parchment);
        }
    }

    private static Color32 WithAlpha(Color32 color, float progress)
    {
        color.a = (byte)Mathf.RoundToInt(color.a * progress);
        return color;
    }

    private static void AddQuad(
        VertexHelper helper,
        Vector2 firstLeft,
        Vector2 firstRight,
        Vector2 secondLeft,
        Vector2 secondRight,
        Color32 color)
    {
        int start = helper.currentVertCount;
        AddVertex(helper, firstLeft, color);
        AddVertex(helper, firstRight, color);
        AddVertex(helper, secondRight, color);
        AddVertex(helper, secondLeft, color);
        helper.AddTriangle(start, start + 1, start + 2);
        helper.AddTriangle(start, start + 2, start + 3);
    }

    private static void AddVertex(VertexHelper helper, Vector2 position, Color32 color)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        helper.AddVert(vertex);
    }
}
