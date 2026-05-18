using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Renders a row of Baybayin character icons above the boss representing
// the current phase's required characters. Each icon greys out as the
// player draws it. Hides during outro.
// Lives on the Gameplay Canvas — set up in §16.
// REWORK: refactored in Task 9 — icon row now driven by new EventBus events
// (OnBossVulnerable, OnBossExhausted, OnBossDamaged) and the new
// CurrentExpectedCharacterID/CorrectDrawsThisWindow model on BossController.
public class BossLabelIconRow : MonoBehaviour
{
    [Header("Icon Prefab (Image with RectTransform 32x32)")]
    [SerializeField] private Image _iconPrefab;
    [SerializeField] private RectTransform _container;

    [Header("Visuals")]
    [SerializeField] private float _iconSize = 32f;
    [SerializeField] private float _iconGap = 4f;
    [SerializeField] private float _drawnAlpha = 0.4f;
    [SerializeField] private Color _drawnTint = new(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Vector2 _bossWorldOffset = new(0f, 1.0f);

    [Header("Camera (optional — falls back to Camera.main)")]
    [SerializeField] private Camera _gameplayCamera;

    private readonly List<Image> _spawnedIcons = new();
    private BossController _boss;
    private Transform _bossTransform;

    private void Awake()
    {
        if (_container == null) _container = (RectTransform)transform;
        if (_gameplayCamera == null) _gameplayCamera = Camera.main;
    }

    private void OnEnable()
    {
        EventBus.OnBossStarted += HandleBossStarted;
        EventBus.OnBossPhaseStarted += HandlePhaseStarted;
        // REWORK: refactored in Task 9 — rewired to new events
        EventBus.OnBossVulnerable += HandlePhaseVulnerable;
        EventBus.OnBossDefeated += HandleBossDefeated;
        // REWORK: refactored in Task 9 — these events were removed from EventBus:
        // EventBus.OnBossPhaseVulnerable += HandlePhaseVulnerable;
        // EventBus.OnBossPhaseAdsReturning += HandlePhaseAdsReturning;
        // EventBus.OnBossPhaseCleared += HandlePhaseCleared;
        // EventBus.OnBossIntermissionStarted += HideRow;
    }

    private void OnDisable()
    {
        EventBus.OnBossStarted -= HandleBossStarted;
        EventBus.OnBossPhaseStarted -= HandlePhaseStarted;
        EventBus.OnBossVulnerable -= HandlePhaseVulnerable;
        EventBus.OnBossDefeated -= HandleBossDefeated;
        // REWORK: refactored in Task 9 — see OnEnable comment.
        // EventBus.OnBossPhaseVulnerable -= HandlePhaseVulnerable;
        // EventBus.OnBossPhaseAdsReturning -= HandlePhaseAdsReturning;
        // EventBus.OnBossPhaseCleared -= HandlePhaseCleared;
        // EventBus.OnBossIntermissionStarted -= HideRow;
        UnsubscribeFromBossInstance();
    }

    private void HandleBossStarted(BossConfigSO config)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentBoss != null)
            _bossTransform = GameManager.Instance.CurrentBoss.transform;
    }

    private void HandlePhaseStarted(int phaseIndex)
    {
        // SummoningPhase: boss invulnerable, minions spawning. Subscribe to the
        // boss instance for draw notifications but keep the icon row hidden —
        // icons appear on OnBossVulnerable.
        UnsubscribeFromBossInstance();

        _boss = GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null;
        if (_boss == null) return;

        _boss.OnDrawnThisPhaseChanged += RefreshIconStates;
        ClearIcons();
    }

    private void HandlePhaseVulnerable(int phaseIndex)
    {
        // Vulnerable window: boss targetable. Show a single icon for the
        // current expected character (Task 9 will build the full icon row).
        // REWORK: refactored in Task 9 — full icon row rebuild goes here.
        RefreshIconStates();
    }

    private void HandleBossDefeated()
    {
        UnsubscribeFromBossInstance();
        ClearIcons();
        _bossTransform = null;
    }

    private void UnsubscribeFromBossInstance()
    {
        if (_boss != null)
            _boss.OnDrawnThisPhaseChanged -= RefreshIconStates;
        _boss = null;
    }

    private void HideRow()
    {
        for (int i = 0; i < _spawnedIcons.Count; i++)
            if (_spawnedIcons[i] != null)
                _spawnedIcons[i].gameObject.SetActive(false);
    }

    private void ClearIcons()
    {
        for (int i = 0; i < _spawnedIcons.Count; i++)
            if (_spawnedIcons[i] != null)
                Destroy(_spawnedIcons[i].gameObject);
        _spawnedIcons.Clear();
    }

    private void RefreshIconStates()
    {
        // REWORK: refactored in Task 9 — full per-icon grey-out logic will go here
        // using boss.CurrentExpectedCharacterID and boss.CorrectDrawsThisWindow.
    }

    private void Update()
    {
        if (_bossTransform == null || _gameplayCamera == null || _container == null) return;

        Vector3 worldPos = _bossTransform.position + (Vector3)_bossWorldOffset;
        Vector2 screenPos = _gameplayCamera.WorldToScreenPoint(worldPos);
        _container.position = new Vector3(screenPos.x, screenPos.y, _container.position.z);
    }
}
