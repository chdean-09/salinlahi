using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthBar : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _fillImage; // Assumes Image Type is 'Filled'
    
    [Header("Animation")]
    [SerializeField] private float _fadeDuration = 0.5f;

    [Header("Positioning")]
    [SerializeField] private Vector2 _bossWorldOffset = new Vector2(0f, 1.5f);
    [SerializeField] private Camera _gameplayCamera;

    private BossController _boss;
    private Transform _bossTransform;
    private int _totalRequiredGlyphs = 0;
    private float _targetFill = 0f;
    private float _currentFill = 0f;
    
    private Coroutine _fadeRoutine;

    private void Awake()
    {
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        SetFillWidth(1f);
    }

    private void OnEnable()
    {
        EventBus.OnBossStarted += HandleBossStarted;
        EventBus.OnBossDefeated += HandleBossDefeated;
        EventBus.OnBossIntermissionStarted += HandleIntermissionStarted;
        EventBus.OnBossPhaseStarted += HandlePhaseStarted;
        EventBus.OnBossPhaseVulnerable += HandlePhaseVulnerable;
        EventBus.OnBossPhaseAdsReturning += HandlePhaseReset;
    }

    private void OnDisable()
    {
        EventBus.OnBossStarted -= HandleBossStarted;
        EventBus.OnBossDefeated -= HandleBossDefeated;
        EventBus.OnBossIntermissionStarted -= HandleIntermissionStarted;
        EventBus.OnBossPhaseStarted -= HandlePhaseStarted;
        EventBus.OnBossPhaseVulnerable -= HandlePhaseVulnerable;
        EventBus.OnBossPhaseAdsReturning -= HandlePhaseReset;
        UnsubscribeFromBoss();
    }

    private void HandleBossStarted(BossConfigSO config)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentBoss != null)
        {
            _boss = GameManager.Instance.CurrentBoss;
            _bossTransform = _boss.transform;
            if (_gameplayCamera == null) _gameplayCamera = Camera.main;

            _boss.OnDrawnThisPhaseChanged += UpdateHealthBar;
            
            // Calculate total health
            _totalRequiredGlyphs = 0;
            if (config.phases != null)
            {
                foreach (var phase in config.phases)
                {
                    _totalRequiredGlyphs += CountNonNull(phase.requiredCharacters);
                }
            }

            _currentFill = 1f;
            _targetFill = 1f;
            SetFillWidth(1f);
            
            FadeTo(1f);
        }
    }

    private void HandlePhaseStarted(int phaseIndex)
    {
        FadeTo(1f);
        UpdateHealthBar();
    }

    private void HandlePhaseVulnerable(int phaseIndex)
    {
        FadeTo(1f);
        UpdateHealthBar();
    }

    private void HandlePhaseReset(int phaseIndex)
    {
        UpdateHealthBar();
    }

    private void HandleIntermissionStarted()
    {
        FadeTo(0f);
    }

    private void HandleBossDefeated()
    {
        UpdateHealthBar(); // To reach 0
        FadeTo(0f);
        UnsubscribeFromBoss();
    }

    private void UnsubscribeFromBoss()
    {
        if (_boss != null)
        {
            _boss.OnDrawnThisPhaseChanged -= UpdateHealthBar;
            _boss = null;
        }
    }

    private void UpdateHealthBar()
    {
        if (_boss == null || _boss.Config == null || _totalRequiredGlyphs <= 0) return;

        int currentDrawn = 0;
        for (int i = 0; i < _boss.Config.phases.Count; i++)
        {
            int phaseReqCount = CountNonNull(_boss.Config.phases[i].requiredCharacters);
            
            if (i < _boss.CurrentPhaseIndex)
            {
                currentDrawn += phaseReqCount;
            }
            else if (i == _boss.CurrentPhaseIndex)
            {
                currentDrawn += _boss.DrawnThisPhase.Count;
            }
        }

        _targetFill = 1f - Mathf.Clamp01((float)currentDrawn / _totalRequiredGlyphs);
    }

    private void Update()
    {
        if (_fillImage != null && Mathf.Abs(_currentFill - _targetFill) > 0.001f)
        {
            _currentFill = Mathf.Lerp(_currentFill, _targetFill, Time.deltaTime * 5f);
            SetFillWidth(_currentFill);
        }

        if (_bossTransform != null && _gameplayCamera != null && _canvasGroup != null && _canvasGroup.alpha > 0f)
        {
            Vector3 worldPos = _bossTransform.position + (Vector3)_bossWorldOffset;
            Vector3 screenPos = _gameplayCamera.WorldToScreenPoint(worldPos);
            
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera uiCamera = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas?.worldCamera;
            
            RectTransform parentRect = transform.parent as RectTransform;
            if (parentRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRect, screenPos, uiCamera, out Vector3 uiWorldPos))
            {
                transform.position = uiWorldPos;
            }
            else
            {
                transform.position = screenPos;
            }
        }
    }

    private void FadeTo(float targetAlpha)
    {
        if (_canvasGroup == null) return;
        
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
        }
        _fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    private System.Collections.IEnumerator FadeRoutine(float targetAlpha)
    {
        float startAlpha = _canvasGroup.alpha;
        float t = 0f;
        
        while (t < _fadeDuration)
        {
            t += Time.unscaledDeltaTime; // Use unscaled so it animates even if game pauses
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / _fadeDuration);
            yield return null;
        }
        
        _canvasGroup.alpha = targetAlpha;
        _fadeRoutine = null;
    }

    private void SetFillWidth(float t)
    {
        if (_fillImage == null) return;
        RectTransform rt = _fillImage.rectTransform;
        Vector2 max = rt.anchorMax;
        max.x = Mathf.Clamp01(t);
        rt.anchorMax = max;
        Vector2 sd = rt.sizeDelta;
        sd.x = 0f;
        rt.sizeDelta = sd;
    }

    private static int CountNonNull(List<BaybayinCharacterSO> list)
    {
        if (list == null) return 0;
        int n = 0;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null) n++;
        return n;
    }
}
