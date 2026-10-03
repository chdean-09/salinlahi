using UnityEngine;
using UnityEngine.UI;

public class EdgeGradient : BaseMeshEffect
{
    public enum Edge { Top, Bottom, Left, Right }
    [SerializeField] private Edge _edgeType = Edge.Top;

    public Edge EdgeType
    {
        get => _edgeType;
        set
        {
            if (_edgeType == value)
                return;

            _edgeType = value;
            if (graphic != null)
                graphic.SetVerticesDirty();
        }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) return;

        Rect rect = rt.rect;
        UIVertex v = new UIVertex();

        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);

            float alphaMultiplier = 1f;

            switch (_edgeType)
            {
                case Edge.Top:
                    alphaMultiplier = Mathf.InverseLerp(rect.yMin, rect.yMax, v.position.y);
                    break;
                case Edge.Bottom:
                    alphaMultiplier = Mathf.InverseLerp(rect.yMax, rect.yMin, v.position.y);
                    break;
                case Edge.Left:
                    alphaMultiplier = Mathf.InverseLerp(rect.xMax, rect.xMin, v.position.x);
                    break;
                case Edge.Right:
                    alphaMultiplier = Mathf.InverseLerp(rect.xMin, rect.xMax, v.position.x);
                    break;
            }

            // Power of 2 makes the fade start steeper from the center to look more like a soft shadow.
            alphaMultiplier *= alphaMultiplier;

            v.color = new Color32(v.color.r, v.color.g, v.color.b, (byte)(v.color.a * alphaMultiplier));
            vh.SetUIVertex(v, i);
        }
    }
}
