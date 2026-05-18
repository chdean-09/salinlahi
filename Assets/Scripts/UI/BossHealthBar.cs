using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// REWORK: refactored in Task 9 — health bar now tracks HPRemaining/phase count
// instead of DrawnThisPhase/requiredCharacters from the old model.
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
    private int _totalPhases = 0;
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
        EventBus.OnBossPhaseStarted += HandlePhaseStarted;
        // REWORK: refactored in Task 9 — rewired to new events
        EventBus.OnBossVulnerable += HandlePhaseVulnerable;
        EventBus.OnBossDamaged += HandleBossDamaged;
        // REWORK: refactored in Task 9 — OnBossPhaseAdsReturning removed; vulnerability
        // expiry resets are handled via OnBossVulnerabilityExpired when Task 9 lands.
        // EventBus.OnBossPhaseAdsReturning += HandlePhaseReset;
    }

    private void OnDisable()
    {
        EventBus.OnBossStarted -= HandleBossStarted;
        EventBus.OnBossDefeated -= HandleBossDefeated;
        EventBus.OnBossPhaseStarted -= HandlePhaseStarted;
        EventBus.OnBossVulnerable -= HandlePhaseVulnerable;
        EventBus.OnBossDamaged -= HandleBossDamaged;
        // REWORK: refactored in Task 9 — see OnEnable comment.
        // EventBus.OnBossPhaseAdsReturning -= HandlePhaseReset;
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

            _totalPhases = config.phases != null ? config.phases.Count : 0;

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

    private void HandleBossDamaged(int phaseIndex, int hpRemaining)
    {
        UpdateHealthBar();
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
        if (_boss == null || _boss.Config == null || _totalPhases <= 0) return;

        _targetFill = Mathf.Clamp01((float)_boss.HPRemaining / _totalPhases);
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
}
