using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum TraceAssistStrength
{
    Strong,
    Light,
    Hidden
}

public class BaybayinTraceGuideController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup _guideGroup;
    [SerializeField] private Image _glyphImage;
    [SerializeField] private Image _tracePathImage;
    [SerializeField] private Image _startMarkerImage;

    [Header("Presentation")]
    [SerializeField] private float _strongAlpha = 1f;
    [SerializeField] private float _lightAlpha = 0.45f;
    [SerializeField] private float _strongLoopSeconds = 1.35f;

    private Coroutine _animationRoutine;

    public TraceAssistStrength CurrentStrength { get; private set; } = TraceAssistStrength.Hidden;
    public bool IsVisible => CurrentStrength != TraceAssistStrength.Hidden;

    private void Awake()
    {
        DisableRaycasts();
        Hide();
    }

    private void OnDisable()
    {
        StopAnimation();
    }

    public void Show(BaybayinCharacterSO character, TraceAssistStrength strength)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        DisableRaycasts();

        if (strength == TraceAssistStrength.Hidden)
        {
            Hide();
            return;
        }

        CurrentStrength = strength;
        Sprite sprite = character != null ? character.displaySprite : null;
        ApplySprite(_glyphImage, sprite);
        ApplySprite(_tracePathImage, sprite);

        if (_guideGroup != null)
        {
            _guideGroup.alpha = strength == TraceAssistStrength.Strong ? _strongAlpha : _lightAlpha;
            _guideGroup.interactable = false;
            _guideGroup.blocksRaycasts = false;
            _guideGroup.gameObject.SetActive(true);
        }

        if (_tracePathImage != null)
        {
            _tracePathImage.enabled = sprite != null;
            _tracePathImage.type = Image.Type.Filled;
            _tracePathImage.fillMethod = Image.FillMethod.Radial360;
            _tracePathImage.fillOrigin = 2;
            _tracePathImage.fillClockwise = true;
            _tracePathImage.fillAmount = strength == TraceAssistStrength.Strong ? 0f : 1f;
        }

        if (_startMarkerImage != null)
        {
            _startMarkerImage.enabled = strength == TraceAssistStrength.Strong || strength == TraceAssistStrength.Light;
        }

        StopAnimation();

        if (strength == TraceAssistStrength.Strong)
            _animationRoutine = StartCoroutine(AnimateStrongGuide());
    }

    public void Hide()
    {
        StopAnimation();
        CurrentStrength = TraceAssistStrength.Hidden;

        if (_guideGroup != null)
        {
            _guideGroup.alpha = 0f;
            _guideGroup.interactable = false;
            _guideGroup.blocksRaycasts = false;
            _guideGroup.gameObject.SetActive(false);
        }

        if (_tracePathImage != null)
            _tracePathImage.fillAmount = 0f;

        if (_startMarkerImage != null)
            _startMarkerImage.enabled = false;
    }

    private IEnumerator AnimateStrongGuide()
    {
        if (_tracePathImage == null)
            yield break;

        float loopDuration = Mathf.Max(0.1f, _strongLoopSeconds);

        while (CurrentStrength == TraceAssistStrength.Strong)
        {
            float elapsed = 0f;
            while (elapsed < loopDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _tracePathImage.fillAmount = Mathf.Clamp01(elapsed / loopDuration);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.25f);
            _tracePathImage.fillAmount = 0f;
        }
    }

    private void StopAnimation()
    {
        if (_animationRoutine == null)
            return;

        StopCoroutine(_animationRoutine);
        _animationRoutine = null;
    }

    private void DisableRaycasts()
    {
        SetRaycastTarget(_glyphImage, false);
        SetRaycastTarget(_tracePathImage, false);
        SetRaycastTarget(_startMarkerImage, false);

        if (_guideGroup != null)
        {
            _guideGroup.interactable = false;
            _guideGroup.blocksRaycasts = false;
        }
    }

    private static void ApplySprite(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.raycastTarget = false;
    }

    private static void SetRaycastTarget(Image image, bool raycastTarget)
    {
        if (image != null)
            image.raycastTarget = raycastTarget;
    }
}
