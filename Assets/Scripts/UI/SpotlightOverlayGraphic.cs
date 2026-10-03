using UnityEngine;
using UnityEngine.UI;

public sealed class SpotlightOverlayGraphic : MaskableGraphic
{
    private Rect _cutoutRect;
    private bool _hasCutout;

    public Rect CutoutRect => _cutoutRect;
    public bool HasCutout => _hasCutout;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void SetCutout(Rect cutoutRect)
    {
        _cutoutRect = NormalizeRect(cutoutRect);
        _hasCutout = _cutoutRect.width > 0f && _cutoutRect.height > 0f;
        SetVerticesDirty();
    }

    public void ClearCutout()
    {
        _cutoutRect = Rect.zero;
        _hasCutout = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect fullRect = rectTransform.rect;
        if (!_hasCutout)
        {
            AddRect(vh, fullRect);
            return;
        }

        Rect cutout = ClampRectToBounds(_cutoutRect, fullRect);
        if (cutout.width <= 0f || cutout.height <= 0f)
        {
            AddRect(vh, fullRect);
            return;
        }

        AddRect(vh, new Rect(fullRect.xMin, cutout.yMax, fullRect.width, fullRect.yMax - cutout.yMax));
        AddRect(vh, new Rect(fullRect.xMin, fullRect.yMin, fullRect.width, cutout.yMin - fullRect.yMin));
        AddRect(vh, new Rect(fullRect.xMin, cutout.yMin, cutout.xMin - fullRect.xMin, cutout.height));
        AddRect(vh, new Rect(cutout.xMax, cutout.yMin, fullRect.xMax - cutout.xMax, cutout.height));
    }

    private void AddRect(VertexHelper vh, Rect rect)
    {
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        int startIndex = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = new Vector3(rect.xMin, rect.yMin);
        vh.AddVert(vertex);
        vertex.position = new Vector3(rect.xMin, rect.yMax);
        vh.AddVert(vertex);
        vertex.position = new Vector3(rect.xMax, rect.yMax);
        vh.AddVert(vertex);
        vertex.position = new Vector3(rect.xMax, rect.yMin);
        vh.AddVert(vertex);

        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }

    private static Rect NormalizeRect(Rect rect)
    {
        float xMin = Mathf.Min(rect.xMin, rect.xMax);
        float xMax = Mathf.Max(rect.xMin, rect.xMax);
        float yMin = Mathf.Min(rect.yMin, rect.yMax);
        float yMax = Mathf.Max(rect.yMin, rect.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static Rect ClampRectToBounds(Rect rect, Rect bounds)
    {
        float xMin = Mathf.Clamp(rect.xMin, bounds.xMin, bounds.xMax);
        float xMax = Mathf.Clamp(rect.xMax, bounds.xMin, bounds.xMax);
        float yMin = Mathf.Clamp(rect.yMin, bounds.yMin, bounds.yMax);
        float yMax = Mathf.Clamp(rect.yMax, bounds.yMin, bounds.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}
