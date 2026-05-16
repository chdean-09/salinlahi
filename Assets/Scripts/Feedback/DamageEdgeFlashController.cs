using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class DamageEdgeFlashController : MonoBehaviour
{
    [Header("Flash")]
    [SerializeField] private Image[] _edgeImages;
    [SerializeField] private Color _flashColor = new Color(0.9f, 0.05f, 0.05f, 0.32f);
    [SerializeField] private float _fadeDuration = 0.2f;
    [SerializeField] private bool _autoConfigureEdgeGradients = true;

    private Coroutine _flashRoutine;

    private void Awake()
    {
        ValidateReferences();
        ConfigureEdgeGradients();
        ApplyAlpha(0f);
    }

    private void OnDisable()
    {
        ApplyAlpha(0f);
    }

    public void Flash()
    {
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        float duration = Mathf.Max(0.01f, _fadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyAlpha(Mathf.Lerp(_flashColor.a, 0f, t));
            yield return null;
        }

        ApplyAlpha(0f);
        _flashRoutine = null;
    }

    private void ApplyAlpha(float alpha)
    {
        if (_edgeImages == null)
            return;

        for (int i = 0; i < _edgeImages.Length; i++)
        {
            Image image = _edgeImages[i];
            if (image == null)
                continue;

            Color color = _flashColor;
            color.a = alpha;
            image.color = color;
            image.raycastTarget = false;
        }
    }

    private void ValidateReferences()
    {
        if (_edgeImages == null || _edgeImages.Length == 0)
            Debug.LogWarning("DamageEdgeFlashController has no edge images assigned.", this);
    }

    private void ConfigureEdgeGradients()
    {
        if (!_autoConfigureEdgeGradients || _edgeImages == null)
            return;

        for (int i = 0; i < _edgeImages.Length; i++)
        {
            Image image = _edgeImages[i];
            if (image == null)
                continue;

            EdgeGradient gradient = image.GetComponent<EdgeGradient>();
            if (gradient == null)
                gradient = image.gameObject.AddComponent<EdgeGradient>();

            gradient.edgeType = GetEdgeTypeForIndex(i);
        }
    }

    private static EdgeGradient.Edge GetEdgeTypeForIndex(int index)
    {
        switch (index)
        {
            case 0: return EdgeGradient.Edge.Top;
            case 1: return EdgeGradient.Edge.Bottom;
            case 2: return EdgeGradient.Edge.Left;
            case 3: return EdgeGradient.Edge.Right;
            default: return EdgeGradient.Edge.Top;
        }
    }
}

public class EdgeGradient : BaseMeshEffect
{
    public enum Edge { Top, Bottom, Left, Right }
    public Edge edgeType = Edge.Top;

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
            
            switch (edgeType)
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

            // Power of 2 makes the fade start steeper from the center to look more like a soft shadow
            alphaMultiplier *= alphaMultiplier;
            
            v.color = new Color32(v.color.r, v.color.g, v.color.b, (byte)(v.color.a * alphaMultiplier));
            vh.SetUIVertex(v, i);
        }
    }
}
