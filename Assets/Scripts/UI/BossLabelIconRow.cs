using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Renders a row of Baybayin character icons above the boss representing
// the current phase's required characters. Each icon greys out as the
// player draws it. Hides during intermission and outro.
// Lives on the Gameplay Canvas — set up in §16.
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
    private readonly Dictionary<BaybayinCharacterSO, Image> _iconByChar = new();
    private BossController _boss;
    private Transform _bossTransform;

    private void Awake()
    {
        if (_container == null) _container = (RectTransform)transform;
        if (_gameplayCamera == null) _gameplayCamera = Camera.main;
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        EventBus.OnBossStarted += HandleBossStarted;
        EventBus.OnBossPhaseStarted += HandlePhaseStarted;
        EventBus.OnBossPhaseCleared += HandlePhaseCleared;
        EventBus.OnBossIntermissionStarted += HideRow;
        EventBus.OnBossDefeated += HandleBossDefeated;
    }

    private void OnDisable()
    {
        EventBus.OnBossStarted -= HandleBossStarted;
        EventBus.OnBossPhaseStarted -= HandlePhaseStarted;
        EventBus.OnBossPhaseCleared -= HandlePhaseCleared;
        EventBus.OnBossIntermissionStarted -= HideRow;
        EventBus.OnBossDefeated -= HandleBossDefeated;
        UnsubscribeFromBossInstance();
    }

    private void HandleBossStarted(BossConfigSO config)
    {
        gameObject.SetActive(true);
        // Boss transform — locate via GameManager.CurrentBoss (set inside StartBoss).
        if (GameManager.Instance != null && GameManager.Instance.CurrentBoss != null)
            _bossTransform = GameManager.Instance.CurrentBoss.transform;
    }

    private void HandlePhaseStarted(int phaseIndex)
    {
        UnsubscribeFromBossInstance();

        _boss = GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null;
        if (_boss == null) return;

        _boss.OnDrawnThisPhaseChanged += RefreshIconStates;

        BuildIcons(_boss.RequiredCharacters);
        RefreshIconStates();
    }

    private void HandlePhaseCleared(int phaseIndex)
    {
        // Flash + hide. Per spec: row re-shows on the next OnBossPhaseStarted.
        HideRow();
    }

    private void HandleBossDefeated()
    {
        UnsubscribeFromBossInstance();
        ClearIcons();
        gameObject.SetActive(false);
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

    private void BuildIcons(IReadOnlyList<BaybayinCharacterSO> required)
    {
        ClearIcons();
        if (_iconPrefab == null || _container == null || required == null) return;

        int count = 0;
        for (int i = 0; i < required.Count; i++) if (required[i] != null) count++;

        // Center the row horizontally on the container.
        float totalWidth = count * _iconSize + Mathf.Max(0, count - 1) * _iconGap;
        float x = -totalWidth * 0.5f + _iconSize * 0.5f;

        for (int i = 0; i < required.Count; i++)
        {
            BaybayinCharacterSO so = required[i];
            if (so == null) continue;

            Image icon = Instantiate(_iconPrefab, _container);
            icon.gameObject.SetActive(true);
            icon.sprite = so.displaySprite;
            icon.color = Color.white;
            icon.preserveAspect = true;

            RectTransform rt = (RectTransform)icon.transform;
            rt.sizeDelta = new Vector2(_iconSize, _iconSize);
            rt.anchoredPosition = new Vector2(x, 0f);
            x += _iconSize + _iconGap;

            _spawnedIcons.Add(icon);
            _iconByChar[so] = icon;
        }
    }

    private void ClearIcons()
    {
        for (int i = 0; i < _spawnedIcons.Count; i++)
            if (_spawnedIcons[i] != null)
                Destroy(_spawnedIcons[i].gameObject);
        _spawnedIcons.Clear();
        _iconByChar.Clear();
    }

    private void RefreshIconStates()
    {
        if (_boss == null) return;

        IReadOnlyCollection<BaybayinCharacterSO> drawn = _boss.DrawnThisPhase;
        foreach (KeyValuePair<BaybayinCharacterSO, Image> kv in _iconByChar)
        {
            bool isDrawn = drawn != null && drawn.Contains(kv.Key);
            Color c = isDrawn ? _drawnTint : Color.white;
            c.a = isDrawn ? _drawnAlpha : 1f;
            kv.Value.color = c;
        }
    }

    private void Update()
    {
        // Follow the boss's screen position.
        if (_bossTransform == null || _gameplayCamera == null || _container == null) return;

        Vector3 worldPos = _bossTransform.position + (Vector3)_bossWorldOffset;
        Vector2 screenPos = _gameplayCamera.WorldToScreenPoint(worldPos);
        _container.position = new Vector3(screenPos.x, screenPos.y, _container.position.z);
    }
}
