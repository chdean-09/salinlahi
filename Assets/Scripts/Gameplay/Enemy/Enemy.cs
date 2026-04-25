using UnityEngine;
#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
#endif
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Pool;

// Attach to Enemy prefab root. Holds data reference and returns itself to EnemyPool.
[RequireComponent(typeof(EnemyMover))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private EnemyDataSO _data;

    [Header("Shield Break Placeholder Visual")]
    [SerializeField] private bool _useShieldBreakColorFeedback;
    [SerializeField] private Color _shieldIntactColor = new(0f, 0.75f, 0.65f, 1f);
    [SerializeField] private Color _shieldBrokenColor = new(0.55f, 0.55f, 0.55f, 1f);

    [Header("Debug Enemy Labels")]
    [SerializeField] private bool _showDebugLabels = true;
    [SerializeField] private Vector3 _labelBaseWorldOffset = new(0f, -1.4f, -0.1f);
    [SerializeField] private float _labelLineSpacingWorld = 0.45f;
    [SerializeField] private float _labelWorldScale = 0.22f;
    [SerializeField] private float _labelFontSize = 10f;
    [SerializeField] private Color _labelColor = Color.white;
    [Header("Walk Animation")]
    [SerializeField] private float _walkAnimationFps = 8f;

    private EnemyMover _mover;
    private SpriteRenderer _renderer;
    private int _currentHealth;
    private BaybayinCharacterSO _runtimeCharacter;
    private Color _baseRendererColor = Color.white;
    private TextMeshPro _baybayinLabel;
    private TextMeshPro _enemyTypeLabel;
    private readonly Dictionary<object, BaybayinCharacterSO> _labelOverrides = new();
    private int _walkFrameIndex;
    private float _walkFrameTimer;

    public BaybayinCharacterSO Character => _runtimeCharacter != null ? _runtimeCharacter : _data?.assignedCharacter;
    public BaybayinCharacterSO VisualCharacter => ResolveVisualCharacter();
    public bool HasVisualCharacterOverride => _labelOverrides.Count > 0;
    public string EnemyID => _data?.enemyID;
    public EnemyDataSO Data => _data;
    public int CurrentHealth => _currentHealth;
    public bool IsDecoy => _data != null && _data.isDecoy;
    public event Action<Enemy, int, int> HealthChanged;

    private void Awake()
    {
        _mover = GetComponent<EnemyMover>();
        _renderer = GetComponent<SpriteRenderer>();

        if (_renderer != null)
            _baseRendererColor = _renderer.color;

        EnsureDebugLabels();
        RefreshDebugLabels();
    }

    private void OnEnable()
    {
        RefreshDebugLabels();
        UpdateLabelLayout();
    }

    public void AssignCharacter(BaybayinCharacterSO character)
    {
        _runtimeCharacter = character;
        RefreshDebugLabels();
    }

    // Called by EnemyPool when this enemy is retrieved from the pool.
    public bool Initialize(EnemyDataSO data)
    {
        _runtimeCharacter = null;

        if (data == null)
        {
            DebugLogger.LogError("Enemy.Initialize: EnemyDataSO is null.");
            ActiveEnemyTracker.Instance?.Unregister(this);
            _mover?.Stop();
            _currentHealth = 0;
            _data = null;
            ResetRendererState();
            return false;
        }

        if (_mover == null)
        {
            DebugLogger.LogError($"Enemy.Initialize: Missing EnemyMover on '{name}'.");
            ActiveEnemyTracker.Instance?.Unregister(this);
            _currentHealth = 0;
            _data = null;
            ResetRendererState();
            return false;
        }

        if (data.maxHealth <= 0)
        {
            DebugLogger.LogError($"Enemy.Initialize: Invalid maxHealth ({data.maxHealth}) for '{data.name}'.");
            ActiveEnemyTracker.Instance?.Unregister(this);
            _mover.Stop();
            _currentHealth = 0;
            _data = null;
            ResetRendererState();
            return false;
        }

        if (_renderer == null)
            DebugLogger.LogWarning($"Enemy.Initialize: Missing SpriteRenderer on '{name}'. Enemy will still function.");

        _data = data;
        _currentHealth = _data.maxHealth;
        _labelOverrides.Clear();

        _mover.Stop();
        _mover.SetSpeed(_data.moveSpeed);

        if (_renderer != null)
        {
            if (_data.walkFrames != null && _data.walkFrames.Length > 0)
            {
                _walkFrameIndex = 0;
                _walkFrameTimer = 0f;
                _renderer.sprite = _data.walkFrames[0];
            }

            _renderer.color = _baseRendererColor;
            ResetShieldBreakVisual();
        }

        ActiveEnemyTracker.Instance?.Register(this);
        RefreshDebugLabels();
        UpdateLabelLayout();
        HealthChanged?.Invoke(this, _currentHealth, _currentHealth);
        return true;
    }

    public bool Initialize(EnemyDataSO data, IObjectPool<Enemy> pool, BaybayinCharacterSO character)
    {
        bool initialized = Initialize(data);
        if (initialized)
            AssignCharacter(character);

        return initialized;
    }

    public void ResetForPool()
    {
        try
        {
            _runtimeCharacter = null;
            _data = null;
            _currentHealth = 0;
            _labelOverrides.Clear();

            if (_mover != null)
                _mover.Stop();
            else
                DebugLogger.LogWarning($"Enemy.ResetForPool: Missing EnemyMover on '{name}'.");

            ResetRendererState();
            RefreshDebugLabels();
        }
        catch (System.Exception ex)
        {
            DebugLogger.LogError($"Enemy.ResetForPool: Exception on '{name}': {ex.Message}");
        }
    }

    public void TakeDamage(int amount)
    {
        if (_data == null)
        {
            DebugLogger.LogWarning($"Enemy.TakeDamage: Enemy '{name}' has no data and cannot take damage.");
            return;
        }

        int previousHealth = _currentHealth;
        _currentHealth -= amount;
        HealthChanged?.Invoke(this, previousHealth, _currentHealth);
        DebugLogger.Log(
            $"Enemy [{Character?.characterID}] took {amount} damage. "
            + $"HP: {_currentHealth}");

        if (_currentHealth <= 0)
        {
            Defeat();
        }
        else if (ShouldTriggerShieldBreak(previousHealth))
        {
            TriggerShieldBreakVisual();
        }
    }

    public void RestoreCurrentHealth(int currentHealth)
    {
        if (_data == null)
            return;

        int previousHealth = _currentHealth;
        _currentHealth = Mathf.Clamp(currentHealth, 1, _data.maxHealth);
        HealthChanged?.Invoke(this, previousHealth, _currentHealth);

        if (_data.maxHealth > 1 && _currentHealth < _data.maxHealth)
            TriggerShieldBreakVisual();
        else
            ResetShieldBreakVisual();
    }

    private bool ShouldTriggerShieldBreak(int previousHealth)
    {
        return _data != null
            && _data.maxHealth > 1
            && previousHealth == _data.maxHealth
            && _currentHealth < previousHealth
            && _currentHealth > 0;
    }

    private void ResetShieldBreakVisual()
    {
        if (_renderer == null)
            return;

        if (!_useShieldBreakColorFeedback || _data == null || _data.maxHealth <= 1)
            return;

        _renderer.color = _shieldIntactColor;
    }

    private void TriggerShieldBreakVisual()
    {
        if (_renderer == null || !_useShieldBreakColorFeedback)
            return;

        _renderer.color = _shieldBrokenColor;
    }

    // Call this to defeat the enemy and return it to the pool.
    public void Defeat()
    {
        BaybayinCharacterSO capturedCharacter = Character;
        ReturnToPool();
        EventBus.RaiseEnemyDefeated(capturedCharacter);
    }

    public void ApplyDecoyPenalty()
    {
        ReturnToPool();
    }

    public void ApplyVisualCharacterOverride(object source, BaybayinCharacterSO visualCharacter)
    {
        if (source == null || visualCharacter == null)
            return;

        _labelOverrides[source] = visualCharacter;
        RefreshDebugLabels();
    }

    public void ClearVisualCharacterOverride(object source)
    {
        if (source == null)
            return;

        if (_labelOverrides.Remove(source))
            RefreshDebugLabels();
    }

    public void ReturnToPool()
    {
        EnemyPool pool = EnemyPool.Instance;
        if (pool != null)
        {
            pool.Return(this);
            return;
        }

        ActiveEnemyTracker.Instance?.Unregister(this);
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        _mover?.Stop();
    }

    private void Update()
    {
        AdvanceWalkAnimation();
    }

    private void LateUpdate()
    {
        if (ShouldShowDebugLabels())
            UpdateLabelLayout();
    }

    private void AdvanceWalkAnimation()
    {
        if (_renderer == null || _data == null || _data.walkFrames == null)
            return;

        int frameCount = _data.walkFrames.Length;
        if (frameCount == 0)
            return;

        if (frameCount == 1)
        {
            _renderer.sprite = _data.walkFrames[0];
            return;
        }

        if (_mover == null || !_mover.IsMoving || _walkAnimationFps <= 0f)
            return;

        float frameDuration = 1f / _walkAnimationFps;
        _walkFrameTimer += Time.deltaTime;

        while (_walkFrameTimer >= frameDuration)
        {
            _walkFrameTimer -= frameDuration;
            _walkFrameIndex = (_walkFrameIndex + 1) % frameCount;
        }

        _renderer.sprite = _data.walkFrames[_walkFrameIndex];
    }

    private void ResetRendererState()
    {
        if (_renderer == null)
            return;

        _renderer.color = _baseRendererColor;
    }

    private void EnsureDebugLabels()
    {
        if (!ShouldShowDebugLabels())
            return;

        if (_baybayinLabel == null)
            _baybayinLabel = CreateLabel("BaybayinLabel");

        if (_enemyTypeLabel == null)
            _enemyTypeLabel = CreateLabel("EnemyTypeLabel");
    }

    private TextMeshPro CreateLabel(string labelName)
    {
        Transform existing = transform.Find(labelName);
        GameObject labelGO = existing != null ? existing.gameObject : new GameObject(labelName);
        labelGO.transform.SetParent(transform, false);
        labelGO.transform.localPosition = Vector3.zero;
        labelGO.transform.localScale = Vector3.one;

        TextMeshPro tmp = labelGO.GetComponent<TextMeshPro>();
        if (tmp == null)
            tmp = labelGO.AddComponent<TextMeshPro>();

        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = false;
        tmp.fontSize = _labelFontSize;
        tmp.color = _labelColor;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;
        tmp.sortingOrder = 500;
        if (_renderer != null)
            tmp.sortingLayerID = _renderer.sortingLayerID;
        tmp.text = string.Empty;
        return tmp;
    }

    private void RefreshDebugLabels()
    {
        if (!ShouldShowDebugLabels())
        {
            if (_baybayinLabel != null) _baybayinLabel.gameObject.SetActive(false);
            if (_enemyTypeLabel != null) _enemyTypeLabel.gameObject.SetActive(false);
            return;
        }

        EnsureDebugLabels();

        if (_baybayinLabel != null)
        {
            _baybayinLabel.gameObject.SetActive(true);
            _baybayinLabel.text = BuildBaybayinLabelText();
        }

        if (_enemyTypeLabel != null)
        {
            _enemyTypeLabel.gameObject.SetActive(true);
            _enemyTypeLabel.text = $"Type: {BuildEnemyTypeText()}";
        }

        UpdateLabelLayout();
    }

    private string BuildBaybayinLabelText()
    {
        BaybayinCharacterSO character = ResolveVisualCharacter();
        if (character == null)
            return "Draw: (none)";

        string syllable = string.IsNullOrWhiteSpace(character.syllable) ? null : character.syllable.Trim().ToLowerInvariant();
        string id = string.IsNullOrWhiteSpace(character.characterID) ? null : character.characterID.Trim().ToUpperInvariant();

        if (!string.IsNullOrEmpty(syllable) && !string.IsNullOrEmpty(id))
            return $"Draw: {syllable} ({id})";

        if (!string.IsNullOrEmpty(syllable))
            return $"Draw: {syllable}";

        if (!string.IsNullOrEmpty(id))
            return $"Draw: {id}";

        return "Draw: (unknown)";
    }

    private BaybayinCharacterSO ResolveVisualCharacter()
    {
        BaybayinCharacterSO character = Character;
        if (_labelOverrides.Count > 0)
        {
            foreach (BaybayinCharacterSO overrideCharacter in _labelOverrides.Values)
            {
                if (overrideCharacter != null)
                {
                    character = overrideCharacter;
                    break;
                }
            }
        }

        return character;
    }

    private string BuildEnemyTypeText()
    {
        if (_data == null)
            return "unknown";

        if (!string.IsNullOrWhiteSpace(_data.enemyID))
            return _data.enemyID.Trim().ToLowerInvariant();

        return string.IsNullOrWhiteSpace(_data.name) ? "unknown" : _data.name.Trim();
    }

    private void UpdateLabelLayout()
    {
        if (_baybayinLabel == null || _enemyTypeLabel == null)
            return;

        Vector3 parentScale = transform.lossyScale;
        float invX = InverseOrOne(parentScale.x);
        float invY = InverseOrOne(parentScale.y);
        float invZ = InverseOrOne(parentScale.z);

        Vector3 baseLocalOffset = new Vector3(
            _labelBaseWorldOffset.x * invX,
            _labelBaseWorldOffset.y * invY,
            _labelBaseWorldOffset.z * invZ
        );

        Vector3 lineSpacingLocal = new Vector3(0f, -_labelLineSpacingWorld * invY, 0f);
        Vector3 worldStableLocalScale = new Vector3(
            _labelWorldScale * invX,
            _labelWorldScale * invY,
            _labelWorldScale * invZ
        );

        _baybayinLabel.transform.localPosition = baseLocalOffset;
        _enemyTypeLabel.transform.localPosition = baseLocalOffset + lineSpacingLocal;

        _baybayinLabel.transform.localScale = worldStableLocalScale;
        _enemyTypeLabel.transform.localScale = worldStableLocalScale;
    }

    private float InverseOrOne(float value)
    {
        if (Mathf.Approximately(value, 0f))
            return 1f;

        return 1f / value;
    }

    private bool ShouldShowDebugLabels()
    {
#if UNITY_EDITOR || SALINLAHI_SANDBOX
        return _showDebugLabels && (Application.isEditor || Debug.isDebugBuild);
#else
        return false;
#endif
    }
}
