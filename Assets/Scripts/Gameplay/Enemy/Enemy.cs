using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR || SALINLAHI_SANDBOX
using Salinlahi.Debug.Sandbox;
#endif
using TMPro;

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
    private EnemyHurtFeedback _hurtFeedback;
    private PhaserEnemy _phaserEnemy;
    private BossSummonTicker _summonTicker;
    // REWORK: SummonWaveOnPhaseStart removed — replaced by BossSummonTicker.
    private SpriteRenderer _renderer;
    private EnemyGlyphBadge _glyphBadge;
    private int _currentHealth;
    private BaybayinCharacterSO _runtimeCharacter;
    private Color _baseRendererColor = Color.white;
    private TextMeshPro _baybayinLabel;
    private TextMeshPro _enemyTypeLabel;
    private readonly Dictionary<object, BaybayinCharacterSO> _labelOverrides = new();
    private int _walkFrameIndex;
    private float _walkFrameTimer;

    private readonly Dictionary<object, float> _speedBuffs = new Dictionary<object, float>();
    private bool _isDying;
    private Coroutine _deathRoutine;
    private static long _spawnSequenceCounter;
    private long _spawnSequence;

    public BaybayinCharacterSO Character => _runtimeCharacter != null ? _runtimeCharacter : _data?.assignedCharacter;
    public BaybayinCharacterSO VisualCharacter => ResolveVisualCharacter();
    public bool HasVisualCharacterOverride => _labelOverrides.Count > 0;
    public EnemyGlyphBadge GlyphBadge => _glyphBadge;
    public string EnemyID => _data?.enemyID;
    public EnemyDataSO Data => _data;
    public int CurrentHealth => _currentHealth;
    public bool IsDecoy => _data != null && _data.isDecoy;
    public bool IsDying => _isDying;
    public bool IsPhaserVisible => _phaserEnemy == null || _phaserEnemy.IsVisible;
    /// <summary>
    /// Monotonic per-spawn number. Stable while this enemy is alive and reassigned when a
    /// pooled enemy re-enters play. It is the deterministic tiebreaker for active clues.
    /// </summary>
    public long SpawnSequence => _spawnSequence;
    // placeholder for now. will be replaced in salin 68
    public virtual bool IsBoss => false;
    public event Action<Enemy, int, int> HealthChanged;

    public int MaxHealth => _data != null ? _data.maxHealth : 0;

    public float EffectiveSpeed
    {
        get
        {
            if (_data == null) return 0f;
            float speed = _data.moveSpeed * _data.baseSpeedMultiplier;
            foreach (var kv in _speedBuffs) speed *= kv.Value;
            return speed;
        }
    }

    public void ApplySpeedBuff(object source, float multiplier)
    {
        _speedBuffs[source] = multiplier;
        PushSpeedToMover();
    }

    public void ClearSpeedBuff(object source)
    {
        if (_speedBuffs.Remove(source))
            PushSpeedToMover();
    }

    private void PushSpeedToMover()
    {
        // Buff/debuff recalculations must not flip _active. Otherwise a periodic
        // aura tick would resume a mover that hurt feedback just paused.
        if (_mover != null) _mover.UpdateSpeedValue(EffectiveSpeed);
    }

    // protected virtual so subclasses (e.g., BossEnemy) can override and chain
    // via base.Awake(). Unity's message dispatcher shadows a base private Awake
    // when a subclass declares its own — making _summonTicker / _hurtFeedback
    // silently null on the boss if base.Awake() isn't called.
    protected virtual void Awake()
    {
        _mover = GetComponent<EnemyMover>();
        _hurtFeedback = GetComponent<EnemyHurtFeedback>();
        _phaserEnemy = GetComponent<PhaserEnemy>();
        _summonTicker = GetComponent<BossSummonTicker>();
        _renderer = GetComponent<SpriteRenderer>();
        _glyphBadge = GetComponentInChildren<EnemyGlyphBadge>(includeInactive: true);

        if (_renderer != null)
            _baseRendererColor = _renderer.color;

        EnsureDebugLabels();
        RefreshDebugLabels();
    }

    protected virtual void OnEnable()
    {
        // Reset on every pool reuse — a previous run as a boss summon may have
        // bumped this above the boss layer. BossEnemy.OnEnable overrides this
        // back to RenderOrder.Boss after calling base.
        if (_renderer != null)
            _renderer.sortingOrder = RenderOrder.EnemyDefault;

        RefreshDebugLabels();
        UpdateLabelLayout();
    }

    public void AssignCharacter(BaybayinCharacterSO character)
    {
        _runtimeCharacter = character;
        RefreshDebugLabels();
        _glyphBadge?.Refresh();
    }

    // Called by EnemyPool when this enemy is retrieved from the pool.
    public bool Initialize(EnemyDataSO data)
    {
        if (_mover == null)
            _mover = GetComponent<EnemyMover>();
        if (_phaserEnemy == null)
            _phaserEnemy = GetComponent<PhaserEnemy>();

        if (_renderer == null)
            _renderer = GetComponent<SpriteRenderer>();

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

        if (_data.useHurtFeedback && _hurtFeedback == null)
        {
            _hurtFeedback = GetComponent<EnemyHurtFeedback>();
            if (_hurtFeedback == null)
            {
                _hurtFeedback = gameObject.AddComponent<EnemyHurtFeedback>();
                DebugLogger.LogWarning(
                    $"Enemy.Initialize: Added missing EnemyHurtFeedback on '{name}' for '{_data.enemyID}'.");
            }
        }

        _mover.Stop();
        _mover.SetSpeed(EffectiveSpeed);

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

        _spawnSequence = ++_spawnSequenceCounter;
        ActiveEnemyTracker.Instance?.Register(this);
        _phaserEnemy?.RefreshPhaserState();
        RefreshDebugLabels();
        if (_glyphBadge != null)
        {
            _glyphBadge.ApplyLayout();
            _glyphBadge.Refresh();
        }
        UpdateLabelLayout();
        HealthChanged?.Invoke(this, _currentHealth, _currentHealth);

        if (ShouldRaiseEnemyDiscoveryEvent(_data))
            EventBus.RaiseEnemyDiscovered(_data, this);

        // Raised after the badge has been laid out and refreshed, so a listener that changes
        // badge visibility is not overwritten by this spawn's own refresh.
        EventBus.RaiseEnemySpawned(this);

        return true;
    }

    private bool ShouldRaiseEnemyDiscoveryEvent(EnemyDataSO data)
    {
        return !IsBoss
            && EnemyDiscoveryProgress.NormalizeEnemyID(data) != null
            && !EnemyDiscoveryProgress.HasDiscovered(data);
    }

    public void ResetForPool()
    {
        try
        {
            _runtimeCharacter = null;
            _speedBuffs.Clear();
            _labelOverrides.Clear();
            _hurtFeedback?.ResetState();
            _isDying = false;

            if (_deathRoutine != null)
            {
                StopCoroutine(_deathRoutine);
                _deathRoutine = null;
            }

            Collider2D contactCollider = GetComponent<Collider2D>();
            if (contactCollider != null) contactCollider.enabled = true;

            _data = null;
            _currentHealth = 0;

            if (_mover != null)
                _mover.Stop();
            else
                DebugLogger.LogWarning($"Enemy.ResetForPool: Missing EnemyMover on '{name}'.");

            // Park far off-screen (very high Y) so that if this enemy is
            // re-registered by Initialize() before the spawner sets its final
            // position, FindClosestToBase will never accidentally select it
            // over an enemy that has already moved partway down the field.
            transform.position = new Vector3(0f, 9999f, 0f);

            ResetRendererState();
            RefreshDebugLabels();
            _glyphBadge?.ResetForPool();
        }
        catch (System.Exception ex)
        {
            DebugLogger.LogError($"Enemy.ResetForPool: Exception on '{name}': {ex.Message}");
        }
    }

    public virtual void TakeDamage(int amount)
    {
        if (_isDying) return;

        if (_data == null)
        {
            DebugLogger.LogWarning($"Enemy.TakeDamage: Enemy '{name}' has no data and cannot take damage.");
            return;
        }

        if (_data.isPhaser && !IsPhaserVisible)
            return;

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
        else
        {
            if (ShouldTriggerShieldBreak(previousHealth))
                TriggerShieldBreakVisual();

            if (_data.useHurtFeedback && _hurtFeedback == null)
            {
                DebugLogger.LogWarning(
                    $"Enemy.TakeDamage: '{name}' ({_data.enemyID}) has useHurtFeedback enabled but no EnemyHurtFeedback component.");
            }

            _hurtFeedback?.OnHurt();
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
        if (_isDying) return;

        BaybayinCharacterSO capturedCharacter = Character;
        bool hasDeathAnimation = _data != null
            && _data.deathFrames != null
            && _data.deathFrames.Length > 0;

        bool hasBadgeFinalDraw = _glyphBadge != null
            && _glyphBadge.Config != null
            && _glyphBadge.isActiveAndEnabled;

        if (hasDeathAnimation)
        {
            // Freeze and fire defeat immediately, but keep this enemy registered
            // until the death animation returns it to the pool. Wave-clear checks
            // therefore wait for visible death animations to finish. EnemyPool.Return
            // owns the single ActiveEnemyTracker.Unregister call.
            _isDying = true;
            _glyphBadge?.PlayFinalDraw();
            // Cancel any in-flight hurt feedback before the death path takes over.
            // Otherwise its pause-window resume (or shake offset) could fight
            // the death animation by reactivating the mover or shifting the sprite.
            _hurtFeedback?.ResetState();
            _mover?.Stop();
            DisableContactCollider();
            // Clear any aura this enemy is projecting before the death animation starts,
            // so affected enemies drop the buff in the same frame as defeat.
            GetComponent<GeneralAura>()?.ClearAllAffected();
            EventBus.RaiseEnemyDefeated(capturedCharacter);
            _deathRoutine = StartCoroutine(PlayDeathAnimationThenReturn());
        }
        else if (hasBadgeFinalDraw)
        {
            // No death frames, but a badge final-draw can still play. Mark dying
            // and disable the contact collider so the enemy is not re-targeted
            // while the badge animation plays. ReturnToPool is delayed until the
            // final-draw coroutine completes (otherwise OnDisable.ResetForPool
            // would stop the coroutine before the animation renders).
            _isDying = true;
            _glyphBadge.PlayFinalDraw();
            _hurtFeedback?.ResetState();
            _mover?.Stop();
            DisableContactCollider();
            GetComponent<GeneralAura>()?.ClearAllAffected();
            EventBus.RaiseEnemyDefeated(capturedCharacter);
            _deathRoutine = StartCoroutine(PlayBadgeFinalDrawThenReturn());
        }
        else
        {
            ReturnToPool();
            EventBus.RaiseEnemyDefeated(capturedCharacter);
        }
    }

    private IEnumerator PlayDeathAnimationThenReturn()
    {
        yield return PlayDeathAnimationFrames();
        _deathRoutine = null;
        ReturnToPool();
    }

    private IEnumerator PlayBadgeFinalDrawThenReturn()
    {
        while (_glyphBadge != null && _glyphBadge.IsPlayingFinalDraw)
            yield return null;
        _deathRoutine = null;
        ReturnToPool();
    }

    // Plays _data.deathFrames once on this enemy's SpriteRenderer. Used by the
    // normal Defeat path AND by BossController.RunOutro (which manages the
    // boss return-to-pool itself and just wants the visual played).
    public IEnumerator PlayDeathAnimationFrames()
    {
        Sprite[] frames = _data != null ? _data.deathFrames : null;
        if (_renderer == null || frames == null || frames.Length == 0)
            yield break;

        float fps = _data.deathAnimationFps > 0f
            ? _data.deathAnimationFps
            : _walkAnimationFps;
        if (fps <= 0f) fps = 8f;
        float frameDuration = 1f / fps;

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null) _renderer.sprite = frames[i];
            float elapsed = 0f;
            while (elapsed < frameDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    private void DisableContactCollider()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }

    public void ApplyDecoyPenalty()
    {
        // Mark dying immediately so a second recognized draw of this decoy's
        // character cannot find it as an eligible target during the reject
        // animation. Without this guard, CombatResolver.ResolveMatchedEnemy
        // would re-enter and raise another OnBaseHit before the pool return.
        if (_isDying) return;
        _isDying = true;

        _mover?.Stop();
        DisableContactCollider();

        bool canPlayReject = _glyphBadge != null
            && _glyphBadge.Config != null
            && _glyphBadge.isActiveAndEnabled
            && gameObject.activeInHierarchy;

        if (canPlayReject && _deathRoutine == null)
        {
            _deathRoutine = StartCoroutine(PlayRejectThenReturn());
        }
        else
        {
            ReturnToPool();
        }
    }

    private IEnumerator PlayRejectThenReturn()
    {
        yield return _glyphBadge.PlayDecoyReject();
        _deathRoutine = null;
        ReturnToPool();
    }

    public void ApplyVisualCharacterOverride(object source, BaybayinCharacterSO visualCharacter)
    {
        if (source == null || visualCharacter == null)
            return;

        // Visual overrides are Enemy-instance-local only and must not be mirrored into HUD/boss icon UI.
        _labelOverrides[source] = visualCharacter;
        RefreshDebugLabels();
        _glyphBadge?.Refresh();
    }

    public void ClearVisualCharacterOverride(object source)
    {
        if (source == null)
            return;

        if (_labelOverrides.Remove(source))
        {
            RefreshDebugLabels();
            _glyphBadge?.Refresh();
        }
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

    protected virtual void OnDisable()
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

    /// <summary>
    /// Resets the walk animation to frame 0. Called when the boss cleanly returns
    /// to the walk cycle after a tell animation completes.
    /// </summary>
    public void ResetWalkAnimation()
    {
        _walkFrameIndex = 0;
        _walkFrameTimer = 0f;
        if (_renderer != null && _data != null
            && _data.walkFrames != null && _data.walkFrames.Length > 0
            && _data.walkFrames[0] != null)
        {
            _renderer.sprite = _data.walkFrames[0];
        }
    }

    private void AdvanceWalkAnimation()
    {
        if (_hurtFeedback != null && _hurtFeedback.IsPlayingHurtAnimation)
            return;

        // Suppress the walk loop while the boss summon tell is on-screen —
        // otherwise the walk frames overwrite the tell on Pacing movement,
        // because Pace sets EnemyMover.IsMoving=true via SetExternallyMoving.
        if (_summonTicker != null && _summonTicker.IsPlayingSummonAnimation)
            return;

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
        // Avoid edit-mode material instantiation warnings in tests.
        if (Application.isPlaying)
        {
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;
        }
        tmp.sortingOrder = RenderOrder.EnemyDebugLabel;
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
            // Bosses don't have a single assigned character — required draws
            // are surfaced by BossDrawCounterUI. Suppressing the per-enemy
            // label avoids the misleading "Draw: (none)" readout.
            bool showBaybayin = !IsBoss;
            _baybayinLabel.gameObject.SetActive(showBaybayin);
            if (showBaybayin)
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
