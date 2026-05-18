using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Shows one Baybayin character icon above the boss during the Vulnerable
// active window, plus a "X / N" progress counter underneath. Lives on
// the Gameplay canvas. Acquires the BossController via GameManager.CurrentBoss
// in the OnBossStarted handler (spec §6).
public class BossGlyphQueueUI : MonoBehaviour
{
    [Header("Icon")]
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _counterText;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Positioning")]
    [SerializeField] private Vector2 _bossWorldOffset = new Vector2(0f, 1.5f);
    [SerializeField] private Camera _gameplayCamera;

    [Header("Visuals")]
    [SerializeField] private float _fadeDuration = 0.15f;
    [SerializeField] private Color _failFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float _failFlashDuration = 0.15f;

    private BossController _boss;
    private Transform _bossTransform;
    private Coroutine _flashRoutine;

    private void Awake()
    {
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        if (_gameplayCamera == null) _gameplayCamera = Camera.main;
    }

    private void OnEnable()
    {
        EventBus.OnBossStarted += HandleBossStarted;
        EventBus.OnBossVulnerable += HandleBossVulnerable;
        EventBus.OnBossDamaged += HandleHide;
        EventBus.OnBossVulnerabilityExpired += HandleHide;
        EventBus.OnBossDefeated += HandleBossDefeated;
        EventBus.OnDrawingFailed += HandleDrawingFailed;
    }

    private void OnDisable()
    {
        EventBus.OnBossStarted -= HandleBossStarted;
        EventBus.OnBossVulnerable -= HandleBossVulnerable;
        EventBus.OnBossDamaged -= HandleHide;
        EventBus.OnBossVulnerabilityExpired -= HandleHide;
        EventBus.OnBossDefeated -= HandleBossDefeated;
        EventBus.OnDrawingFailed -= HandleDrawingFailed;
        UnsubscribeFromBossInstance();
    }

    private void HandleBossStarted(BossConfigSO _)
    {
        UnsubscribeFromBossInstance();
        _boss = GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null;
        if (_boss == null) return;
        _bossTransform = _boss.transform;
        _boss.OnDrawnThisPhaseChanged += RefreshFromBoss;
    }

    private void HandleBossVulnerable(int phaseIndex)
    {
        if (_boss == null) return;
        RefreshFromBoss();
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
    }

    private void HandleHide(int phaseIndex)
    {
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
    }

    private void HandleBossDefeated()
    {
        UnsubscribeFromBossInstance();
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        _bossTransform = null;
    }

    private void HandleDrawingFailed()
    {
        if (_iconImage == null) return;
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FailFlash());
    }

    private System.Collections.IEnumerator FailFlash()
    {
        Color original = _iconImage.color;
        _iconImage.color = _failFlashColor;
        yield return new WaitForSeconds(_failFlashDuration);
        _iconImage.color = original;
        _flashRoutine = null;
    }

    private void RefreshFromBoss()
    {
        if (_boss == null) return;
        BaybayinCharacterSO so = _boss.CurrentExpectedCharacter;
        if (so != null && _iconImage != null)
        {
            _iconImage.sprite = so.displaySprite;
            _iconImage.enabled = true;
        }
        if (_counterText != null)
        {
            _counterText.text = $"{_boss.CorrectDrawsThisWindow} / {_boss.RequiredCharactersForCurrentPhase}";
        }
    }

    private void UnsubscribeFromBossInstance()
    {
        if (_boss != null)
            _boss.OnDrawnThisPhaseChanged -= RefreshFromBoss;
        _boss = null;
    }

    private void Update()
    {
        if (_bossTransform == null || _gameplayCamera == null || _canvasGroup == null
            || _canvasGroup.alpha <= 0f) return;
        Vector3 worldPos = _bossTransform.position + (Vector3)_bossWorldOffset;
        Vector3 screenPos = _gameplayCamera.WorldToScreenPoint(worldPos);
        transform.position = screenPos;
    }
}
