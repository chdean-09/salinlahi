using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presents data-authored ability layers using Enemy's manual walk-frame index as the only loop
/// clock. Exit animations have their own one-shot clock because they must continue while hurt
/// feedback has paused the enemy's movement and walk animation.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class EnemyAbilityVisualPresenter : MonoBehaviour
{
    private sealed class Layer
    {
        public EnemyAbilityVisualDefinition definition;
        public SpriteRenderer anchor;
        public SpriteRenderer renderer;
        public bool activeRequested;
        public bool activationPlaying;
        public int activationFrameIndex;
        public float activationFrameTimer;
        public bool exitPlaying;
        public int exitFrameIndex;
        public float exitFrameTimer;
        public bool frameConfigurationValid;
    }

    private readonly Dictionary<EnemyAbilityVisualId, Layer> _layers = new();
    private readonly List<EnemyAbilityVisualId> _configuredIds = new();
    private readonly HashSet<EnemyAbilityVisualDefinition> _warnedInvalidDefinitions = new();
    private Enemy _enemy;
    private int _currentBaseFrame;
    private float _visibilityAlphaMultiplier = 1f;

    public bool HasVisual(EnemyAbilityVisualId id)
    {
        return _layers.TryGetValue(id, out Layer layer)
            && layer.definition != null
            && layer.definition.activeSprite != null
            && layer.anchor != null
            && layer.renderer != null;
    }

    public bool IsExitPlaying(EnemyAbilityVisualId id)
    {
        return _layers.TryGetValue(id, out Layer layer) && layer.exitPlaying;
    }

    public bool IsActivationPlaying(EnemyAbilityVisualId id)
    {
        return _layers.TryGetValue(id, out Layer layer) && layer.activationPlaying;
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();
        SubscribeToWalkFrames();
    }

    private void OnDisable()
    {
        if (_enemy != null)
            _enemy.WalkFrameChanged -= HandleWalkFrameChanged;
        StopAll();
    }

    private void HandleWalkFrameChanged(Enemy enemy, int frameIndex)
    {
        SyncBaseFrame(frameIndex);
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    private void LateUpdate()
    {
        // Enemy sorting can change at runtime (for example when a boss summon moves to a higher
        // render layer), so mirror the anchor without taking ownership of its animation clock.
        foreach (Layer layer in _layers.Values)
        {
            if (layer.renderer != null && layer.anchor != null)
                SyncLayerConfiguration(layer);
        }
    }

    /// <summary>Bind the pooled presenter to the visual definitions for this enemy spawn.</summary>
    public void Configure(EnemyDataSO data, SpriteRenderer bodyRenderer, SpriteRenderer glyphRenderer)
    {
        StopAll();
        if (_enemy != null)
            _enemy.WalkFrameChanged -= HandleWalkFrameChanged;
        _enemy = GetComponent<Enemy>();
        SubscribeToWalkFrames();
        _configuredIds.Clear();

        EnemyAbilityVisualDefinition[] definitions = data != null ? data.abilityVisuals : null;
        if (definitions != null)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                EnemyAbilityVisualDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                if (_configuredIds.Contains(definition.id))
                {
                    Debug.LogWarning($"Enemy '{name}' has duplicate {definition.id} visual definitions; later entries are ignored.", this);
                    continue;
                }

                SpriteRenderer anchor = definition.anchor == EnemyAbilityVisualAnchor.GlyphBadge
                    ? glyphRenderer
                    : bodyRenderer;
                if (anchor == null)
                {
                    Debug.LogWarning($"Enemy '{name}' has a {definition.id} visual but no {definition.anchor} renderer to anchor it to.", this);
                    continue;
                }

                Layer layer = GetOrCreateLayer(definition.id);
                layer.definition = definition;
                layer.anchor = anchor;
                layer.activeRequested = false;
                layer.activationPlaying = false;
                layer.activationFrameIndex = 0;
                layer.activationFrameTimer = 0f;
                layer.exitPlaying = false;
                layer.exitFrameIndex = 0;
                layer.exitFrameTimer = 0f;
                layer.frameConfigurationValid = ValidateFrameConfiguration(definition, data);
                ConfigureLayerTransform(layer);
                SyncLayerConfiguration(layer);
                Hide(layer);
                _configuredIds.Add(definition.id);
            }
        }

        foreach (KeyValuePair<EnemyAbilityVisualId, Layer> pair in _layers)
        {
            if (!_configuredIds.Contains(pair.Key))
            {
                pair.Value.definition = null;
                pair.Value.anchor = null;
                pair.Value.activeRequested = false;
                pair.Value.activationPlaying = false;
                pair.Value.exitPlaying = false;
                Hide(pair.Value);
            }
        }

        SyncBaseFrame(_enemy != null ? _enemy.CurrentWalkFrameIndex : 0);
    }

    private void SubscribeToWalkFrames()
    {
        if (_enemy == null || !isActiveAndEnabled)
            return;

        _enemy.WalkFrameChanged -= HandleWalkFrameChanged;
        _enemy.WalkFrameChanged += HandleWalkFrameChanged;
    }

    /// <summary>Set or clear a persistent ability layer, respecting its authored frame gate.</summary>
    public void SetActive(EnemyAbilityVisualId id, bool active)
    {
        if (!_layers.TryGetValue(id, out Layer layer) || layer.definition == null)
            return;

        if (layer.activeRequested == active && (!active || layer.exitPlaying == false))
        {
            RefreshActiveLayer(layer);
            return;
        }

        layer.activeRequested = active;
        layer.exitPlaying = false;
        layer.exitFrameIndex = 0;
        layer.exitFrameTimer = 0f;
        layer.activationPlaying = false;
        layer.activationFrameIndex = 0;
        layer.activationFrameTimer = 0f;
        if (active && layer.definition.activationFrames != null
            && layer.definition.activationFrames.Length > 0)
        {
            layer.activationPlaying = true;
            DrawActivationFrame(layer);
            return;
        }

        RefreshActiveLayer(layer);
    }

    /// <summary>
    /// Adds or refreshes a source-authored layer on this enemy, anchored to a renderer owned by
    /// another ability target. Used by Mantsa because its ink belongs on the affected badge, not
    /// on Mantsa's own body. It does not disturb this pooled enemy's other configured layers.
    /// </summary>
    public void ConfigureExternalVisual(EnemyAbilityVisualDefinition definition, SpriteRenderer anchor)
    {
        if (definition == null || anchor == null)
            return;

        // External layers can lazily add this component to an already-active pooled enemy. Do not
        // rely on Unity having delivered Awake/OnEnable before the source ability configures it.
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();
        SubscribeToWalkFrames();
        if (_enemy != null)
            _currentBaseFrame = _enemy.CurrentWalkFrameIndex;

        Layer layer = GetOrCreateLayer(definition.id);
        bool changedBinding = layer.definition != definition || layer.anchor != anchor;
        if (changedBinding)
        {
            layer.activeRequested = false;
            layer.activationPlaying = false;
            layer.activationFrameIndex = 0;
            layer.activationFrameTimer = 0f;
            layer.exitPlaying = false;
            layer.exitFrameIndex = 0;
            layer.exitFrameTimer = 0f;
        }

        layer.definition = definition;
        layer.anchor = anchor;
        layer.frameConfigurationValid = ValidateFrameConfiguration(definition, _enemy != null ? _enemy.Data : null);
        if (changedBinding)
            ConfigureLayerTransform(layer);
        SyncLayerConfiguration(layer);
        if (!layer.activeRequested && !layer.activationPlaying && !layer.exitPlaying)
            Hide(layer);
    }

    /// <summary>Play the configured removal frames once, independently of the walk loop.</summary>
    public void PlayExit(EnemyAbilityVisualId id)
    {
        if (!_layers.TryGetValue(id, out Layer layer) || layer.definition == null)
            return;

        Sprite[] frames = layer.definition.exitFrames;
        layer.activeRequested = false;
        layer.activationPlaying = false;
        layer.activationFrameIndex = 0;
        layer.activationFrameTimer = 0f;
        if (frames == null || frames.Length == 0)
        {
            layer.exitPlaying = false;
            Hide(layer);
            return;
        }

        // Do not restart an in-flight one-shot if two state notifications arrive in the same frame.
        if (layer.exitPlaying)
            return;

        layer.exitPlaying = true;
        layer.exitFrameIndex = 0;
        layer.exitFrameTimer = 0f;
        DrawExitFrame(layer);
    }

    /// <summary>Synchronize persistent layers with the frame currently shown by Enemy.</summary>
    public void SyncBaseFrame(int frameIndex)
    {
        _currentBaseFrame = frameIndex;
        foreach (Layer layer in _layers.Values)
            RefreshActiveLayer(layer);
    }

    /// <summary>
    /// Applies a renderer-wide visibility multiplier (for example, Phaser's fade) without
    /// replacing each ability layer's authored tint or opacity.
    /// </summary>
    public void SetVisibilityAlphaMultiplier(float multiplier)
    {
        _visibilityAlphaMultiplier = Mathf.Clamp01(multiplier);
        foreach (Layer layer in _layers.Values)
        {
            if (layer.exitPlaying)
                DrawExitFrame(layer);
            else if (layer.activationPlaying)
                DrawActivationFrame(layer);
            else
                RefreshActiveLayer(layer);
        }
    }

    /// <summary>Stop one-shot playback and immediately clear every pooled visual layer.</summary>
    public void StopAll()
    {
        foreach (Layer layer in _layers.Values)
            StopLayer(layer);
    }

    /// <summary>Stop every layer except the named one-shot, for ability-specific defeat effects.</summary>
    public void StopAllExcept(EnemyAbilityVisualId retainedId)
    {
        foreach (KeyValuePair<EnemyAbilityVisualId, Layer> pair in _layers)
        {
            if (pair.Key != retainedId)
                StopLayer(pair.Value);
        }
    }

    /// <summary>Explicit playback seam for deterministic edit-mode tests.</summary>
    public void Tick(float deltaTime)
    {
        float step = Mathf.Max(0f, deltaTime);
        foreach (Layer layer in _layers.Values)
        {
            if (layer.activationPlaying && layer.definition != null)
            {
                float activationFps = layer.definition.activationFramesPerSecond > 0f
                    ? layer.definition.activationFramesPerSecond
                    : 8f;
                float activationDuration = 1f / activationFps;
                layer.activationFrameTimer += step;

                while (layer.activationPlaying && layer.activationFrameTimer >= activationDuration)
                {
                    layer.activationFrameTimer -= activationDuration;
                    layer.activationFrameIndex++;
                    Sprite[] activationFrames = layer.definition.activationFrames;
                    if (activationFrames == null || layer.activationFrameIndex >= activationFrames.Length)
                    {
                        layer.activationPlaying = false;
                        RefreshActiveLayer(layer);
                        break;
                    }

                    DrawActivationFrame(layer);
                }
            }

            if (!layer.exitPlaying || layer.definition == null)
                continue;

            float fps = layer.definition.exitFramesPerSecond > 0f
                ? layer.definition.exitFramesPerSecond
                : 8f;
            float frameDuration = 1f / fps;
            layer.exitFrameTimer += step;

            while (layer.exitPlaying && layer.exitFrameTimer >= frameDuration)
            {
                layer.exitFrameTimer -= frameDuration;
                layer.exitFrameIndex++;
                Sprite[] frames = layer.definition.exitFrames;
                if (frames == null || layer.exitFrameIndex >= frames.Length)
                {
                    layer.exitPlaying = false;
                    Hide(layer);
                    break;
                }

                DrawExitFrame(layer);
            }
        }
    }

    private Layer GetOrCreateLayer(EnemyAbilityVisualId id)
    {
        if (_layers.TryGetValue(id, out Layer existing))
            return existing;

        GameObject layerObject = new GameObject($"AbilityVisual_{id}");
        layerObject.transform.SetParent(transform, false);
        SpriteRenderer renderer = layerObject.AddComponent<SpriteRenderer>();
        renderer.enabled = false;
        Layer layer = new Layer { renderer = renderer };
        _layers.Add(id, layer);
        return layer;
    }

    private void ConfigureLayerTransform(Layer layer)
    {
        if (layer.renderer == null || layer.anchor == null || layer.definition == null)
            return;

        Transform layerTransform = layer.renderer.transform;
        layerTransform.SetParent(layer.anchor.transform, false);
        layerTransform.localPosition = layer.definition.localOffset;
        layerTransform.localRotation = Quaternion.identity;
        layerTransform.localScale = layer.definition.localScale;
        layer.renderer.sprite = null;
        layer.renderer.enabled = false;
    }

    private void SyncLayerConfiguration(Layer layer)
    {
        if (layer.renderer == null || layer.anchor == null || layer.definition == null)
            return;

        layer.renderer.sortingLayerID = layer.anchor.sortingLayerID;
        layer.renderer.sortingOrder = layer.anchor.sortingOrder + layer.definition.sortingOrderOffset;
        layer.renderer.sharedMaterial = layer.definition.material != null
            ? layer.definition.material
            : layer.anchor.sharedMaterial;
    }

    private bool ValidateFrameConfiguration(EnemyAbilityVisualDefinition definition, EnemyDataSO data)
    {
        int frameCount = data != null && data.walkFrames != null ? data.walkFrames.Length : 0;
        bool valid = definition.frameMode switch
        {
            EnemyAbilityVisualFrameMode.FullLoop => true,
            EnemyAbilityVisualFrameMode.SingleFrame => definition.singleFrameIndex >= 0
                && definition.singleFrameIndex < frameCount,
            EnemyAbilityVisualFrameMode.FrameRange => definition.frameRangeStartIndex >= 0
                && definition.frameRangeEndIndex >= definition.frameRangeStartIndex
                && definition.frameRangeEndIndex < frameCount,
            _ => false
        };

        if (!valid)
        {
            string range = definition.frameMode == EnemyAbilityVisualFrameMode.SingleFrame
                ? definition.singleFrameIndex.ToString()
                : $"{definition.frameRangeStartIndex}..{definition.frameRangeEndIndex}";
            if (_warnedInvalidDefinitions.Add(definition))
            {
                Debug.LogWarning(
                    $"Enemy '{name}' {definition.id} visual has invalid {definition.frameMode} index/range {range} for {frameCount} walk frames; it will stay hidden.",
                    this);
            }
        }

        return valid;
    }

    private void RefreshActiveLayer(Layer layer)
    {
        if (layer.renderer == null || layer.definition == null)
            return;

        SyncLayerConfiguration(layer);
        if (layer.exitPlaying || layer.activationPlaying)
            return;

        bool visible = layer.activeRequested
            && layer.definition.activeSprite != null
            && layer.frameConfigurationValid
            && FrameMatches(layer.definition, _currentBaseFrame);
        if (!visible)
        {
            Hide(layer);
            return;
        }

        layer.renderer.sprite = layer.definition.activeSprite;
        layer.renderer.color = ResolveColor(layer.definition.activeTint, layer.definition.activeOpacity);
        layer.renderer.enabled = true;
    }

    private void DrawActivationFrame(Layer layer)
    {
        Sprite[] frames = layer.definition != null ? layer.definition.activationFrames : null;
        if (layer.renderer == null || frames == null
            || layer.activationFrameIndex < 0 || layer.activationFrameIndex >= frames.Length
            || frames[layer.activationFrameIndex] == null)
        {
            layer.activationPlaying = false;
            RefreshActiveLayer(layer);
            return;
        }

        SyncLayerConfiguration(layer);
        layer.renderer.sprite = frames[layer.activationFrameIndex];
        layer.renderer.color = ResolveColor(
            layer.definition.activationTint,
            layer.definition.activationOpacity);
        layer.renderer.enabled = true;
    }

    private bool FrameMatches(EnemyAbilityVisualDefinition definition, int frameIndex)
    {
        int frameCount = _enemy != null ? _enemy.WalkFrameCount : 0;
        if (frameIndex < 0 || frameIndex >= frameCount)
            return false;

        switch (definition.frameMode)
        {
            case EnemyAbilityVisualFrameMode.FullLoop:
                return true;
            case EnemyAbilityVisualFrameMode.SingleFrame:
                return frameIndex == definition.singleFrameIndex;
            case EnemyAbilityVisualFrameMode.FrameRange:
                return frameIndex >= definition.frameRangeStartIndex
                    && frameIndex <= definition.frameRangeEndIndex;
            default:
                return false;
        }
    }

    private void DrawExitFrame(Layer layer)
    {
        Sprite[] frames = layer.definition.exitFrames;
        if (frames == null || layer.exitFrameIndex < 0 || layer.exitFrameIndex >= frames.Length)
        {
            layer.exitPlaying = false;
            Hide(layer);
            return;
        }

        SyncLayerConfiguration(layer);
        Sprite frame = frames[layer.exitFrameIndex];
        if (frame == null)
        {
            Hide(layer);
            return;
        }

        layer.renderer.sprite = frame;
        layer.renderer.color = ResolveColor(layer.definition.exitTint, layer.definition.exitOpacity);
        layer.renderer.enabled = true;
    }

    private Color ResolveColor(Color tint, float opacity)
    {
        tint.a = Mathf.Clamp01(tint.a * Mathf.Clamp01(opacity) * _visibilityAlphaMultiplier);
        return tint;
    }

    private static void Hide(Layer layer)
    {
        if (layer.renderer == null)
            return;

        layer.renderer.enabled = false;
        layer.renderer.sprite = null;
    }

    private static void StopLayer(Layer layer)
    {
        layer.activeRequested = false;
        layer.activationPlaying = false;
        layer.activationFrameIndex = 0;
        layer.activationFrameTimer = 0f;
        layer.exitPlaying = false;
        layer.exitFrameIndex = 0;
        layer.exitFrameTimer = 0f;
        Hide(layer);
    }
}
