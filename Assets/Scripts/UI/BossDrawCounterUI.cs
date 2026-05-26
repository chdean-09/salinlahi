using UnityEngine;
using TMPro;

public class BossDrawCounterUI : MonoBehaviour
{
    [Header("Counter")]
    [SerializeField] private TextMeshProUGUI _counterText;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Positioning")]
    [Tooltip("Local offset from the boss's GlyphBadge transform. (0, -0.5) places the counter just below the badge.")]
    [SerializeField] private Vector2 _badgeRelativeOffset = new Vector2(0f, -0.5f);
    [SerializeField] private Camera _gameplayCamera;

    private BossController _boss;
    private Transform _badgeTransform;

    private void Awake()
    {
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        if (_gameplayCamera == null) _gameplayCamera = Camera.main;
    }

    private void OnEnable()
    {
        EventBus.OnBossStarted += HandleBossStarted;
        EventBus.OnBossVulnerabilityWindowActive += HandleVulnerabilityActive;
        EventBus.OnBossVulnerabilityExpired += HandleHide;
        EventBus.OnBossDamaged += HandleBossDamagedHide;
        EventBus.OnBossDefeated += HandleBossDefeated;
    }

    private void OnDisable()
    {
        EventBus.OnBossStarted -= HandleBossStarted;
        EventBus.OnBossVulnerabilityWindowActive -= HandleVulnerabilityActive;
        EventBus.OnBossVulnerabilityExpired -= HandleHide;
        EventBus.OnBossDamaged -= HandleBossDamagedHide;
        EventBus.OnBossDefeated -= HandleBossDefeated;
        UnsubscribeFromBossInstance();
    }

    private void HandleBossStarted(BossConfigSO _)
    {
        UnsubscribeFromBossInstance();
        _boss = GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null;
        if (_boss == null) return;
        EnemyGlyphBadge badge = _boss.GetComponent<Enemy>()?.GlyphBadge;
        _badgeTransform = badge != null ? badge.transform : _boss.transform;
        _boss.OnDrawnThisPhaseChanged += RefreshFromBoss;
    }

    private void HandleVulnerabilityActive(int phaseIndex)
    {
        if (_boss == null) return;
        RefreshFromBoss();
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
    }

    private void HandleHide(int phaseIndex)
    {
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
    }

    private void HandleBossDamagedHide(int phaseIndex, int hpRemaining) => HandleHide(phaseIndex);

    private void HandleBossDefeated()
    {
        UnsubscribeFromBossInstance();
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        _badgeTransform = null;
    }

    private void RefreshFromBoss()
    {
        if (_boss == null || _counterText == null) return;
        _counterText.text = $"{_boss.CorrectDrawsThisWindow} / {_boss.RequiredCharactersForCurrentPhase}";
    }

    private void UnsubscribeFromBossInstance()
    {
        if (_boss != null)
            _boss.OnDrawnThisPhaseChanged -= RefreshFromBoss;
        _boss = null;
    }

    private void Update()
    {
        if (_badgeTransform == null || _gameplayCamera == null || _canvasGroup == null
            || _canvasGroup.alpha <= 0f) return;
        Vector3 worldPos = _badgeTransform.position + (Vector3)_badgeRelativeOffset;
        transform.position = _gameplayCamera.WorldToScreenPoint(worldPos);
    }
}
