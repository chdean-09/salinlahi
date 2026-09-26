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
    // OFF by default. ShouldShowDebugLabels already keeps these out of a release player, but that
    // fence says nothing about a normal play session in the Editor, which is where the game is
    // actually looked at: with this defaulted on — and authored 1 on the shared corruption shell —
    // raw internal ids ("Type: abo-ng-simula", "Draw: a (A)") rendered over every enemy in every
    // playtest and every screenshot. It stays a serialized per-prefab opt-in so anyone debugging
    // spawn identity can still switch it on for the prefab they care about.
    [SerializeField] private bool _showDebugLabels;
    [SerializeField] private Vector3 _labelBaseWorldOffset = new(0f, -1.9f, -0.1f);
    [SerializeField] private float _labelLineSpacingWorld = 0.45f;
    [SerializeField] private float _labelWorldScale = 0.22f;
    [SerializeField] private float _labelFontSize = 10f;
    [SerializeField] private Color _labelColor = Color.white;
    [Header("Walk Animation")]
    [SerializeField] private float _walkAnimationFps = 8f;

    private EnemyMover _mover;
    private EnemyHurtFeedback _hurtFeedback;
    private EnemyAbilityVisualPresenter _abilityVisualPresenter;
    private bool _hasBeenExternalAbilityVisualTarget;
    private EnemyArmorStateBinder _armorVisualBinder;
    private PhaserEnemy _phaserEnemy;
    private BossSummonTicker _summonTicker;
    // REWORK: SummonWaveOnPhaseStart removed — replaced by BossSummonTicker.
    private SpriteRenderer _renderer;
    private EnemyGlyphBadge _glyphBadge;
    private int _currentHealth;
    private BaybayinCharacterSO _runtimeCharacter;
    private Color _baseRendererColor = Color.white;
    // The shell's authored transform scale, captured once so EnemyDataSO.spriteScale can be applied per spawn.
    private Vector3 _shellBaseLocalScale = Vector3.one;
    private TextMeshPro _baybayinLabel;
    private TextMeshPro _enemyTypeLabel;
    private readonly Dictionary<object, BaybayinCharacterSO> _labelOverrides = new();
    private readonly HashSet<object> _glyphStainSources = new();
    /// <summary>
    /// Source-keyed resolution blocks, ref-counted the same way <see cref="_labelOverrides"/>
    /// ref-counts badge visual overrides: one entry per holding ability, so two abilities blocking
    /// the same enemy compose and releasing one does not release the other.
    /// </summary>
    private readonly HashSet<object> _resolutionBlocks = new();
    private int _walkFrameIndex;
    private float _walkFrameTimer;

    private readonly Dictionary<object, float> _speedBuffs = new Dictionary<object, float>();
    private bool _isDying;
    private Coroutine _deathRoutine;
    private static long _spawnSequenceCounter;
    private long _spawnSequence;
    private bool _isIntroductionSpawn;
    private IntroductionOutcome _introductionOutcome;

    public BaybayinCharacterSO Character => _runtimeCharacter != null ? _runtimeCharacter : _data?.assignedCharacter;
    public BaybayinCharacterSO VisualCharacter => ResolveVisualCharacter();
    public bool HasVisualCharacterOverride => _labelOverrides.Count > 0;
    public bool IsGlyphStained => _glyphStainSources.Count > 0;
    public EnemyGlyphBadge GlyphBadge => _glyphBadge;
    public EnemyAbilityVisualPresenter AbilityVisuals => _abilityVisualPresenter;
    public string EnemyID => _data?.enemyID;
    public EnemyDataSO Data => _data;
    public int CurrentHealth => _currentHealth;
    public bool IsDecoy => _data != null && _data.isDecoy;
    public bool IsDying => _isDying;
    public bool IsPhaserVisible => _phaserEnemy == null
        || _phaserEnemy.IsVisible
        || _data?.learningAbility == EnemyLearningAbility.MemoryFade;

    /// <summary>
    /// True while at least one ability holds a resolution block on this enemy. A blocked enemy is
    /// refused by <c>CombatResolver.IsEligibleCombatTarget</c> and by
    /// <c>ActiveClueDirector.IsEligibleClue</c>, so it can be neither marked nor damaged.
    /// <para>
    /// Deliberately <b>neutral</b>: it names the effect ("may this enemy be resolved right now?"),
    /// never the ability that caused it. Bakod (SALIN-286) is the first source; Kadena's chained
    /// invulnerability is meant to become the second by calling
    /// <see cref="AddResolutionBlock"/> only, without touching resolver code.
    /// </para>
    /// </summary>
    public bool IsResolutionBlocked => _resolutionBlocks.Count > 0;

    /// <summary>How many distinct sources currently hold this enemy blocked. Test/diagnostic seam.</summary>
    public int ResolutionBlockCount => _resolutionBlocks.Count;

    /// <summary>
    /// Monotonic per-spawn number. Stable while this enemy is alive and reassigned when a
    /// pooled enemy re-enters play. It is the deterministic tiebreaker for active clues.
    /// </summary>
    public long SpawnSequence => _spawnSequence;

    /// <summary>
    /// True when this spawn is the one that introduced its enemy type to the player — the type's
    /// first ever spawn campaign-wide, the spawn <see cref="EnemyIntroductionBeat"/> halts the field
    /// for and plays a card over.
    ///
    /// <para>
    /// Its consequence is that <b>this spawn's signature ability is inert</b>. The card states what
    /// the enemy does and a later spawn proves it, so the player has a clean board to notice the
    /// effect against; every Era 1 ability takes something away, and one that was already running
    /// the first time the player looked reads as a fault rather than as an enemy. The suppression is
    /// applied in <see cref="Initialize"/>, at the same place the ability components are configured,
    /// so the two decisions cannot drift apart.
    /// </para>
    ///
    /// <para>Reset per spawn: a pooled shell never inherits it.</para>
    /// </summary>
    public bool IsIntroductionSpawn => _isIntroductionSpawn;

    /// <summary>This spawn's introduction outcome. Read by tests and by the introduction beat.</summary>
    public IntroductionOutcome IntroductionOutcome => _introductionOutcome;
    // placeholder for now. will be replaced in salin 68
    public virtual bool IsBoss => false;
    public event Action<Enemy, int, int> HealthChanged;
    /// <summary>Raised only for a hit that leaves the enemy alive, before hurt feedback begins.</summary>
    public event Action<Enemy, int, int> NonLethalDamageTaken;
    /// <summary>Raised after initialization, manual walk-frame advancement, or an explicit reset.</summary>
    public event Action<Enemy, int> WalkFrameChanged;

    public int MaxHealth => _data != null ? _data.maxHealth : 0;
    public int CurrentWalkFrameIndex => _walkFrameIndex;
    public int WalkFrameCount => _data != null && _data.walkFrames != null ? _data.walkFrames.Length : 0;

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
        _abilityVisualPresenter = GetComponent<EnemyAbilityVisualPresenter>();
        _armorVisualBinder = GetComponent<EnemyArmorStateBinder>();

        if (_renderer != null)
            _baseRendererColor = _renderer.color;

        _shellBaseLocalScale = transform.localScale;

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
        NameLossEffectRegistry.Changed += HandleNameLossEffectChanged;
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

        // Awake resolves the badge once, but pool rigs (and tests) attach the
        // badge child after Awake has already run — re-resolve on every
        // initialize so Defeat's final-draw path can see it.
        if (_glyphBadge == null)
            _glyphBadge = GetComponentInChildren<EnemyGlyphBadge>(includeInactive: true);

        _runtimeCharacter = null;

        if (data == null)
        {
            DebugLogger.LogError("Enemy.Initialize: EnemyDataSO is null.");
            ActiveEnemyTracker.Instance?.Unregister(this);
            _mover?.Stop();
            _currentHealth = 0;
            _data = null;
            _abilityVisualPresenter?.StopAll();
            ResetRendererState();
            return false;
        }

        if (_mover == null)
        {
            DebugLogger.LogError($"Enemy.Initialize: Missing EnemyMover on '{name}'.");
            ActiveEnemyTracker.Instance?.Unregister(this);
            _currentHealth = 0;
            _data = null;
            _abilityVisualPresenter?.StopAll();
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
            _abilityVisualPresenter?.StopAll();
            ResetRendererState();
            return false;
        }

        if (_renderer == null)
            DebugLogger.LogWarning($"Enemy.Initialize: Missing SpriteRenderer on '{name}'. Enemy will still function.");

        _data = data;
        _currentHealth = _data.maxHealth;
        _abilityVisualPresenter?.StopAll();
        _labelOverrides.Clear();
        ClearGlyphStains();
        ClearResolutionBlocks();

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

        // Asked before the ability components are configured, because the outcome is also the
        // signal for suppression. Three outcomes, not two: see IntroductionDecision — an
        // ordinarily declined claim still arms, because that is the safe failure: the player
        // meets an ability with no card, rather than meeting an enemy whose ability is silently
        // switched off forever. A claim declined because a lesson is still pending is the
        // exception and suppresses instead — that decline is deliberate, not a beat that
        // couldn't be bothered.
        _introductionOutcome = EnemyIntroductionBeat.ResolveIntroduction(this, _data);
        _isIntroductionSpawn = _introductionOutcome == IntroductionOutcome.IntroduceAndSuppress
            || _introductionOutcome == IntroductionOutcome.IntroduceAndArm;

        // Signature abilities are data-driven so the prefab-less corruption roster can carry them
        // on the shared shell. A pooled shell is reused across types, so each ability component is
        // added on first need and then enabled or disabled per spawn to match the incoming data.
        EnsureAbilityComponent<PensionadoMover>(_data.zigzagAmplitude > 0f);
        EnsureAbilityComponent<KempeiScrambleController>(_data.stainsNearbyGlyphs);
        EnsureAbilityComponent<GlyphCoverController>(_data.coversOwnGlyph);
        EnsureAbilityComponent<MirrorDecoyController>(_data.spawnsMirrorDecoy);
        EnsureAbilityComponent<BakodShieldController>(_data.blocksEnemiesBehind);
        EnsureAbilityComponent<KadenaChainController>(_data.chainsNearestEnemy);
        EnsureAbilityComponent<HatiSplitController>(_data.splitsOnDefeat);
        EnsureAbilityComponent<AshFirstSlotController>(_data.ashesFirstSlot);
        EnsureAbilityComponent<NawalangMukhaNameLossController>(_data.removesNames);
        EnsureAbilityComponent<PhaserEnemy>(_data.isPhaser);
        EnsureAbilityComponent<EnemyLearningAbilityController>(_data.learningAbility != EnemyLearningAbility.None);
        EnsureAbilityComponent<EnemyAbilityVisualPresenter>(
            (_data.abilityVisuals != null && _data.abilityVisuals.Length > 0)
            || _hasBeenExternalAbilityVisualTarget);
        EnsureAbilityComponent<EnemyArmorStateBinder>(
            HasDataAbilityVisual(_data, EnemyAbilityVisualId.Armor));
        _abilityVisualPresenter = GetComponent<EnemyAbilityVisualPresenter>();
        _armorVisualBinder = GetComponent<EnemyArmorStateBinder>();

        EnemyLearningAbilityController learningAbility = GetComponent<EnemyLearningAbilityController>();
        if (learningAbility != null && learningAbility.enabled)
            learningAbility.ResetForSpawn();

        // Restated on EVERY spawn, not only introduction ones. The abilities clear their own flag in
        // OnEnable, but a pooled shell reused for the same enemy type stays enabled through the
        // reuse — EnsureAbilityComponent only toggles `enabled` — so OnEnable never fires and the
        // previous occupant's suppression would carry into a spawn that is meant to be armed.
        ApplyIntroductionSpawnSuppression(
            IntroductionDecision.SuppressesAbility(_introductionOutcome));

        // Resolved after the block above, because the component may have just been added, and
        // cleared for a non-phaser so a reused shell does not consult a disabled phaser when
        // answering IsPhaserVisible. PhaserEnemy.OnDisable restores full visibility either way.
        _phaserEnemy = _data.isPhaser ? GetComponent<PhaserEnemy>() : null;

        // A pooled shell keeps whatever scale its last occupant used, so always restate it from
        // the authored shell scale. A no-op for every enemy authored at spriteScale 1.
        Vector3 wantedScale = _shellBaseLocalScale * Mathf.Max(0.05f, _data.spriteScale);
        if (transform.localScale != wantedScale)
            transform.localScale = wantedScale;

        _walkFrameIndex = 0;
        _walkFrameTimer = 0f;
        if (_renderer != null)
        {
            if (_data.walkFrames != null && _data.walkFrames.Length > 0)
            {
                _renderer.sprite = _data.walkFrames[0];
            }

            _renderer.color = _baseRendererColor;
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
        if (_abilityVisualPresenter != null)
            _abilityVisualPresenter.Configure(_data, _renderer, _glyphBadge != null ? _glyphBadge.Renderer : null);
        // Resolve the legacy color feedback only after this spawn's visuals have been configured.
        // A pooled shell may still hold the previous occupant's armor definition at the point
        // where the base sprite color is reset above.
        ResetShieldBreakVisual();
        if (_armorVisualBinder != null && _armorVisualBinder.enabled)
            _armorVisualBinder.Bind(this, _abilityVisualPresenter);
        WalkFrameChanged?.Invoke(this, _walkFrameIndex);
        UpdateLabelLayout();
        HealthChanged?.Invoke(this, _currentHealth, _currentHealth);

        if (ShouldRaiseEnemyDiscoveryEvent(_data))
            EventBus.RaiseEnemyDiscovered(_data, this);

        // Raised after the badge has been laid out and refreshed, so a listener that changes
        // badge visibility is not overwritten by this spawn's own refresh.
        EventBus.RaiseEnemySpawned(this);

        // Started last, and separately from the claim above, because the card frames the enemy where
        // it stands: the spawner sets this enemy's field position and carried glyph only after
        // Initialize returns, so the beat waits a beat of its own before halting anything.
        if (_isIntroductionSpawn)
            EnemyIntroductionBeat.BeginIntroduction(this);

        return true;
    }

    /// <summary>
    /// Applies or lifts the introduction-spawn suppression on every signature ability this enemy
    /// could be carrying.
    ///
    /// <para>
    /// Applied uniformly to every suppressible signature ability rather than per-ability, and that is
    /// the point: one rule is reasonable about, and any exception ("name loss is gentle enough to fire
    /// immediately") becomes a per-enemy special case somebody has to rediscover later. Each ability
    /// only needs to know how to be a no-op; deciding <i>when</i> lives here.
    /// </para>
    ///
    /// <para>
    /// <b>Every ability with a visible effect belongs in this list.</b>
    /// <c>IntroductionDecision.IntroduceAndSuppress</c> promises the ability "is inert this spawn",
    /// and an ability missing from here quietly breaks that promise: the card explains what the
    /// enemy does while the enemy is already doing it. <see cref="KadenaChainController"/> is the
    /// deliberate omission — it carries no <c>SetSuppressedForIntroductionSpawn</c> of its own, so
    /// there is nothing here to call; adding one is its own change.
    /// </para>
    ///
    /// <para>
    /// Only the component that is actually enabled for this spawn is addressed. The shared corruption
    /// shell keeps every ability component attached and merely disabled for types that lack the
    /// ability, so calling into a disabled one would be asking an ability nobody has to stop doing
    /// something it was never doing.
    /// </para>
    /// </summary>
    private void ApplyIntroductionSpawnSuppression(bool suppressed)
    {
        AshFirstSlotController ash = GetComponent<AshFirstSlotController>();
        if (ash != null && ash.enabled)
            ash.SetSuppressedForIntroductionSpawn(suppressed);

        NawalangMukhaNameLossController nameLoss = GetComponent<NawalangMukhaNameLossController>();
        if (nameLoss != null && nameLoss.enabled)
            nameLoss.SetSuppressedForIntroductionSpawn(suppressed);

        KempeiScrambleController stain = GetComponent<KempeiScrambleController>();
        if (stain != null && stain.enabled)
            stain.SetSuppressedForIntroductionSpawn(suppressed);

        MirrorDecoyController decoy = GetComponent<MirrorDecoyController>();
        if (decoy != null && decoy.enabled)
            decoy.SetSuppressedForIntroductionSpawn(suppressed);

        GlyphCoverController cover = GetComponent<GlyphCoverController>();
        if (cover != null && cover.enabled)
            cover.ResetForSpawn(suppressed);

        BakodShieldController shield = GetComponent<BakodShieldController>();
        if (shield != null && shield.enabled)
            shield.SetSuppressedForIntroductionSpawn(suppressed);

        HatiSplitController split = GetComponent<HatiSplitController>();
        if (split != null && split.enabled)
            split.SetSuppressedForIntroductionSpawn(suppressed);

        EnemyLearningAbilityController learningAbility = GetComponent<EnemyLearningAbilityController>();
        if (learningAbility != null && learningAbility.enabled)
            learningAbility.SetSuppressedForIntroductionSpawn(suppressed);
    }

    private bool ShouldRaiseEnemyDiscoveryEvent(EnemyDataSO data)
    {
        return !IsBoss
            && !data.suppressDiscovery
            && EnemyDiscoveryProgress.NormalizeEnemyID(data) != null
            && !EnemyDiscoveryProgress.HasDiscovered(data);
    }

    /// <summary>
    /// Adds a data-driven ability component on first need and enables or disables it to match the
    /// current data, so a pooled shell reused for a different enemy type does not keep an ability.
    /// </summary>
    private void EnsureAbilityComponent<T>(bool wanted) where T : MonoBehaviour
    {
        T component = GetComponent<T>();
        if (component == null)
        {
            if (!wanted)
                return;
            component = gameObject.AddComponent<T>();
        }

        component.enabled = wanted;
    }

    private static bool HasDataAbilityVisual(EnemyDataSO data, EnemyAbilityVisualId id)
    {
        if (data == null || data.abilityVisuals == null)
            return false;

        for (int i = 0; i < data.abilityVisuals.Length; i++)
        {
            EnemyAbilityVisualDefinition definition = data.abilityVisuals[i];
            if (definition != null && definition.id == id && definition.activeSprite != null)
                return true;
        }

        return false;
    }

    public void ResetForPool()
    {
        try
        {
            _abilityVisualPresenter?.StopAll();
            _runtimeCharacter = null;
            _speedBuffs.Clear();
            _labelOverrides.Clear();
            ClearGlyphStains();
            ClearResolutionBlocks();
            _hurtFeedback?.ResetState();
            _isDying = false;
            // Per spawn, never per shell: the next occupant of this shell decides for itself whether
            // it is an introduction spawn, and a stale true would suppress its ability for nothing.
            _isIntroductionSpawn = false;
            _introductionOutcome = IntroductionOutcome.None;

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
            if (!HasArmorAbilityVisual() && ShouldTriggerShieldBreak(previousHealth))
                TriggerShieldBreakVisual();

            if (previousHealth == _data.maxHealth && _currentHealth < previousHealth)
                NonLethalDamageTaken?.Invoke(this, previousHealth, _currentHealth);

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

        if (!HasArmorAbilityVisual())
        {
            if (_data.maxHealth > 1 && _currentHealth < _data.maxHealth)
                TriggerShieldBreakVisual();
            else
                ResetShieldBreakVisual();
        }
    }

    private bool HasArmorAbilityVisual()
    {
        return _abilityVisualPresenter != null
            && _abilityVisualPresenter.HasVisual(EnemyAbilityVisualId.Armor);
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

        if (HasArmorAbilityVisual() || !_useShieldBreakColorFeedback || _data == null || _data.maxHealth <= 1)
            return;

        _renderer.color = _shieldIntactColor;
    }

    private void TriggerShieldBreakVisual()
    {
        if (_renderer == null || HasArmorAbilityVisual() || !_useShieldBreakColorFeedback)
            return;

        _renderer.color = _shieldBrokenColor;
    }

    // Call this to defeat the enemy and return it to the pool.
    public void Defeat()
    {
        if (_isDying) return;

        // Bakod's barrier has a specific break one-shot. Keep that one layer through a badge-only
        // defeat, while all other persistent ability art clears before any death presentation.
        BakodShieldController bakod = GetComponent<BakodShieldController>();
        bool hasBakodBreak = bakod != null && bakod.enabled && bakod.BeginDefeatVisual();
        if (hasBakodBreak)
            _abilityVisualPresenter?.StopAllExcept(EnemyAbilityVisualId.BakodBarrier);
        else
            _abilityVisualPresenter?.StopAll();

        BaybayinCharacterSO capturedCharacter = Character;

        // Hati splits the moment it falls, before its death visuals, so the pieces are on the
        // field while the source is still visibly breaking apart.
        if (_data != null && _data.splitsOnDefeat)
        {
            HatiSplitController split = GetComponent<HatiSplitController>();
            if (split != null && split.enabled)
                split.SpawnOnDefeat();
        }
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
        else if (hasBakodBreak)
        {
            _isDying = true;
            _hurtFeedback?.ResetState();
            _mover?.Stop();
            DisableContactCollider();
            GetComponent<GeneralAura>()?.ClearAllAffected();
            EventBus.RaiseEnemyDefeated(capturedCharacter);
            _deathRoutine = StartCoroutine(PlayBakodBarrierBreakThenReturn());
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
        while (_abilityVisualPresenter != null
               && _abilityVisualPresenter.IsExitPlaying(EnemyAbilityVisualId.BakodBarrier))
            yield return null;
        _deathRoutine = null;
        ReturnToPool();
    }

    private IEnumerator PlayBakodBarrierBreakThenReturn()
    {
        while (_abilityVisualPresenter != null
               && _abilityVisualPresenter.IsExitPlaying(EnemyAbilityVisualId.BakodBarrier))
            yield return null;
        _deathRoutine = null;
        ReturnToPool();
    }

    // Plays _data.deathFrames once on this enemy's SpriteRenderer. Used by the
    // normal Defeat path AND by BossController.RunOutro (which manages the
    // boss return-to-pool itself and just wants the visual played).
    public IEnumerator PlayDeathAnimationFrames()
    {
        _abilityVisualPresenter?.StopAll();
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
        _abilityVisualPresenter?.StopAll();

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

    /// <summary>
    /// Holds a visual ink stain on this enemy's badge without changing its real character. Sources
    /// are ref-counted like resolution blocks so two Mantsa enemies can overlap safely.
    /// </summary>
    public void SetGlyphStained(object source, bool stained)
    {
        if (source == null || _glyphBadge == null)
            return;

        bool wasStained = _glyphStainSources.Count > 0;
        bool changed = stained
            ? _glyphStainSources.Add(source)
            : _glyphStainSources.Remove(source);

        if (changed)
        {
            bool isStained = _glyphStainSources.Count > 0;
            _glyphBadge.SetStained(isStained);
            if (isStained)
            {
                object visualSource = FindAbilityVisualSource(
                    _glyphStainSources,
                    EnemyAbilityVisualId.MantsaStain);
                UpdateExternalAbilityVisual(
                    EnemyAbilityVisualId.MantsaStain,
                    visualSource,
                    active: visualSource != null,
                    animateRemoval: true);
            }
            else if (wasStained)
            {
                UpdateExternalAbilityVisual(
                    EnemyAbilityVisualId.MantsaStain,
                    source,
                    active: false,
                    animateRemoval: true);
            }
        }
    }

    private void ClearGlyphStains()
    {
        _glyphStainSources.Clear();
        _glyphBadge?.SetStained(false);
        _abilityVisualPresenter?.SetActive(EnemyAbilityVisualId.MantsaStain, false);
    }

    /// <summary>
    /// Holds this enemy unresolvable on behalf of <paramref name="source"/>. Idempotent per source,
    /// so an ability may re-assert its own block every tick without stacking. The enemy stays
    /// blocked until every source has released it.
    /// </summary>
    public void AddResolutionBlock(object source)
    {
        if (source == null)
            return;

        if (!_resolutionBlocks.Add(source))
            return;

        if (_resolutionBlocks.Count == 1)
            RefreshResolutionBlockTell(animateRemoval: false);
        else
            RefreshResolutionBlockAbilityVisual();
    }

    /// <summary>
    /// Releases <paramref name="source"/>'s hold. Safe to call for a source that never held one.
    /// </summary>
    public void RemoveResolutionBlock(object source)
    {
        if (source == null)
            return;

        if (_resolutionBlocks.Remove(source))
        {
            if (_resolutionBlocks.Count == 0)
                RefreshResolutionBlockTell(animateRemoval: true);
            else
                RefreshResolutionBlockAbilityVisual();
        }
    }

    /// <summary>
    /// Drops every hold at once. Used on the spawn and pool boundaries so a shell that was blocked
    /// when it left play never comes back still blocked — the permanently-unresolvable-enemy defect.
    /// </summary>
    private void ClearResolutionBlocks()
    {
        if (_resolutionBlocks.Count == 0)
            return;

        _resolutionBlocks.Clear();
        RefreshResolutionBlockTell(animateRemoval: false);
    }

    private void RefreshResolutionBlockTell(bool animateRemoval)
    {
        _glyphBadge?.SetResolutionBlocked(IsResolutionBlocked);
        RefreshResolutionBlockAbilityVisual(animateRemoval);
    }

    private void RefreshResolutionBlockAbilityVisual(bool animateRemoval = true)
    {
        object source = FindAbilityVisualSource(_resolutionBlocks, EnemyAbilityVisualId.BakodBlockedTarget);
        UpdateExternalAbilityVisual(
            EnemyAbilityVisualId.BakodBlockedTarget,
            source,
            source != null,
            animateRemoval);
    }

    private void UpdateExternalAbilityVisual(
        EnemyAbilityVisualId id,
        object source,
        bool active,
        bool animateRemoval)
    {
        EnemyAbilityVisualPresenter presenter = _abilityVisualPresenter;
        EnemyAbilityVisualDefinition definition = FindAbilityVisualDefinition(source, id);
        if (active && definition != null && _glyphBadge != null && _glyphBadge.Renderer != null)
        {
            _hasBeenExternalAbilityVisualTarget = true;
            if (presenter == null)
            {
                presenter = GetComponent<EnemyAbilityVisualPresenter>();
                if (presenter == null)
                    presenter = gameObject.AddComponent<EnemyAbilityVisualPresenter>();
                _abilityVisualPresenter = presenter;
            }

            // A pooled shell can retain the component after an enemy type without its own visuals
            // disabled it. External effects must wake that component before configuring the new
            // source's layer; setting enabled before Configure also guarantees OnEnable cleanup.
            if (!presenter.enabled)
                presenter.enabled = true;

            presenter.ConfigureExternalVisual(definition, _glyphBadge.Renderer);
            if (_phaserEnemy != null)
                presenter.SetVisibilityAlphaMultiplier(_phaserEnemy.CurrentVisibilityAlpha);
            presenter.SetActive(id, true);
            return;
        }

        if (presenter == null || !presenter.HasVisual(id))
            return;

        if (active)
            presenter.SetActive(id, true);
        else if (animateRemoval)
            presenter.PlayExit(id);
        else
            presenter.SetActive(id, false);
    }

    private static object FindAbilityVisualSource(
        HashSet<object> sources,
        EnemyAbilityVisualId id)
    {
        foreach (object source in sources)
        {
            if (FindAbilityVisualDefinition(source, id) != null)
                return source;
        }

        return null;
    }

    private static EnemyAbilityVisualDefinition FindAbilityVisualDefinition(
        object source,
        EnemyAbilityVisualId id)
    {
        MonoBehaviour sourceComponent = source as MonoBehaviour;
        Enemy sourceEnemy = sourceComponent != null ? sourceComponent.GetComponent<Enemy>() : null;
        EnemyDataSO sourceData = sourceEnemy != null ? sourceEnemy.Data : null;
        EnemyAbilityVisualDefinition[] definitions = sourceData != null ? sourceData.abilityVisuals : null;
        if (definitions == null)
            return null;

        for (int i = 0; i < definitions.Length; i++)
        {
            EnemyAbilityVisualDefinition definition = definitions[i];
            if (definition != null && definition.id == id && definition.activeSprite != null)
                return definition;
        }

        return null;
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
        NameLossEffectRegistry.Changed -= HandleNameLossEffectChanged;
        _mover?.Stop();
        _abilityVisualPresenter?.StopAll();
    }

    private void HandleNameLossEffectChanged()
    {
        RefreshDebugLabels();
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

        WalkFrameChanged?.Invoke(this, _walkFrameIndex);
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
        int previousFrame = _walkFrameIndex;

        while (_walkFrameTimer >= frameDuration)
        {
            _walkFrameTimer -= frameDuration;
            _walkFrameIndex = (_walkFrameIndex + 1) % frameCount;
        }

        _renderer.sprite = _data.walkFrames[_walkFrameIndex];
        if (previousFrame != _walkFrameIndex)
            WalkFrameChanged?.Invoke(this, _walkFrameIndex);
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
            bool showBaybayin = !IsBoss && !NameLossEffectRegistry.IsActive;
            _baybayinLabel.gameObject.SetActive(showBaybayin);
            if (showBaybayin)
                _baybayinLabel.text = BuildBaybayinLabelText();
        }

        if (_enemyTypeLabel != null)
        {
            bool showEnemyType = !NameLossEffectRegistry.IsActive;
            _enemyTypeLabel.gameObject.SetActive(showEnemyType);
            if (showEnemyType)
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
