using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives a single level select button slot.
/// Displays the level's baked-in numbered scroll sprite,
/// tints it grey when locked, and forwards taps to scene-load.
/// </summary>
public class LevelButton : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private Button _button;
    [Tooltip("The scroll Image on this button. Sprite is set from LevelConfigSO.numberSprite.")]
    [SerializeField] private Image _scrollImage;

    [Header("State Visuals")]
    [Tooltip("Shown only when the level is locked (e.g. a lock icon overlay).")]
    [SerializeField] private GameObject _lockIcon;
    // SALIN-137 AC1: the completed-state visual. PREFERRED path is Inspector wiring, but
    // no LevelSelect.unity instance wires it today, so an unwired field is resolved at
    // runtime from the LevelButton prefab's existing `CompletionCheck` child — see
    // ResolveCompletionBadge. Without that fallback a completed level would render
    // identically to a merely unlocked one and AC1 would not hold in the real scene.
    [Tooltip("Shown only when the level is completed. Falls back to the prefab's CompletionCheck child.")]
    [SerializeField] private GameObject _completionBadge;

    [Header("Colors")]
    [SerializeField] private Color _unlockedColor = Color.white;
    [SerializeField] private Color _lockedColor   = new Color(0.55f, 0.55f, 0.55f, 1f);

    // SALIN-136: emphasis applied to the journey's next meaningful level. Uses only
    // already-wired components (the button transform), so no scene edits are needed.
    private static readonly Vector3 HighlightScale = new(1.08f, 1.08f, 1f);

    /// <summary>
    /// SALIN-137: name of the completed-state child authored on
    /// <c>Assets/Prefabs/UI/LevelButton.prefab</c>. Every LevelSelect.unity instance keeps
    /// it (only the prefab's `Label` child is removed per instance), so discovery by name
    /// reaches real, authored art instead of constructing any.
    /// </summary>
    private const string CompletionBadgeChildName = "CompletionCheck";

    /// <summary>
    /// SALIN-137: multiplied into the scroll tint when a level has no
    /// <see cref="LevelConfigSO.numberSprite"/>. Levels 6-15 have none, so their scrolls
    /// render as a neutral blank placeholder rather than an unmissable white block —
    /// while still carrying the unlocked/locked tint difference AC1 depends on.
    /// </summary>
    private static readonly Color PlaceholderTint = new(0.82f, 0.74f, 0.58f, 0.85f);

    private LevelConfigSO _config;
    private bool _isUnlocked;
    private Vector3 _baseScale = Vector3.one;
    private bool _baseScaleCaptured;
    private bool _completionBadgeLookupDone;
    private Action<LevelConfigSO> _lockedPressHandler;

    /// <summary>
    /// SALIN-137: the owner's handler for a press on a *locked* level. Level Select uses
    /// it to explain the prerequisite without leaving the screen. A plain C# delegate is
    /// deliberate — a serialized UnityEvent would need scene rewiring, matching the
    /// scene-edit-free precedent SALIN-136 set with <see cref="SetHighlighted"/>.
    /// </summary>
    public void SetLockedPressHandler(Action<LevelConfigSO> handler) => _lockedPressHandler = handler;

    /// <summary>
    /// Configures this button for the given level config and progress state.
    /// Safe to call repeatedly — listeners are deduplicated.
    /// </summary>
    public void Setup(LevelConfigSO config, bool isUnlocked, bool isCompleted)
    {
        _config     = config;
        _isUnlocked = isUnlocked;

        if (_scrollImage != null)
        {
            Color stateColor = isUnlocked ? _unlockedColor : _lockedColor;

            if (config.numberSprite != null)
            {
                _scrollImage.sprite = config.numberSprite;
            }
            else
            {
                // SALIN-137: buttons are REUSED across eras, so leaving the previous
                // sprite in place would render the WRONG level number (era 2 would show
                // era 1's numbered scrolls). Clearing is the only honest option until the
                // art for levels 6-15 exists; the placeholder tint keeps the empty slot
                // from reading as a glaring white block.
                _scrollImage.sprite = null;
                stateColor *= PlaceholderTint;
                DebugLogger.LogWarning(
                    $"LevelButton: {config.name} has no numberSprite assigned. " +
                    "Rendering a blank placeholder scroll.");
            }

            _scrollImage.color = stateColor;
        }

        if (_lockIcon != null)
            _lockIcon.SetActive(!isUnlocked);

        GameObject completionBadge = ResolveCompletionBadge();
        if (completionBadge != null)
            completionBadge.SetActive(isCompleted);

        if (_button != null)
        {
            // SALIN-137 AC2: a locked scroll must stay interactable so the press is
            // observable and the prerequisite can be explained. `OnPressed` gates entry
            // on `_isUnlocked` instead, so the player provably stays on Level Select.
            // Side effect: Unity's ColorTint disabled tint no longer applies to locked
            // scrolls — `_lockedColor` plus the LockOverlay child must carry that look.
            _button.interactable = true;
            _button.onClick.RemoveListener(OnPressed);
            _button.onClick.AddListener(OnPressed);
        }
    }

    /// <summary>
    /// SALIN-136: marks this button as the journey's next meaningful level.
    /// Safe to call repeatedly (buttons are reused across eras) — the base scale is
    /// captured once and restored when the highlight moves elsewhere.
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        if (!_baseScaleCaptured)
        {
            _baseScale = transform.localScale;
            _baseScaleCaptured = true;
        }

        transform.localScale = highlighted
            ? Vector3.Scale(_baseScale, HighlightScale)
            : _baseScale;
    }

    /// <summary>
    /// SALIN-137 AC1: returns the completed-state visual, preferring authored Inspector
    /// wiring and otherwise discovering the LevelButton prefab's existing
    /// <c>CompletionCheck</c> child by name.
    ///
    /// WHY A FALLBACK: none of the five LevelButton instances in
    /// <c>Assets/_Scenes/LevelSelect.unity</c> serialize <c>_completionBadge</c>, so the
    /// field deserializes null and a completed level would look identical to an unlocked
    /// one. This mirrors the no-Inspector-wiring precedent already used for the lock
    /// notice surface (<see cref="LevelLockNoticePanel"/>). It reuses authored art and
    /// never constructs any: if the child is absent there is nothing to show.
    ///
    /// OWED SCENE WORK: assigning <c>_completionBadge</c> in the Inspector makes this
    /// discovery inert.
    ///
    /// The lookup runs at most once per instance — buttons are re-Setup on every era
    /// change, and a failed deep search must not repeat on each render.
    /// </summary>
    private GameObject ResolveCompletionBadge()
    {
        if (_completionBadge != null)
            return _completionBadge;

        if (_completionBadgeLookupDone)
            return null;
        _completionBadgeLookupDone = true;

        // Direct child first — that is where the prefab authors it. Transform.Find also
        // matches inactive children, which CompletionCheck always is at rest.
        Transform found = transform.Find(CompletionBadgeChildName);

        if (found == null)
        {
            Transform[] descendants = GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != transform &&
                    string.Equals(descendants[i].name, CompletionBadgeChildName, StringComparison.Ordinal))
                {
                    found = descendants[i];
                    break;
                }
            }
        }

        if (found == null || (_lockIcon != null && found.gameObject == _lockIcon))
        {
            DebugLogger.LogWarning(
                $"LevelButton: no completion badge on '{name}'. Assign _completionBadge, or keep a " +
                $"'{CompletionBadgeChildName}' child, or completed levels will look unlocked.");
            return null;
        }

        _completionBadge = found.gameObject;
        return _completionBadge;
    }

    private void OnPressed()
    {
        if (_config == null) return;

        // SALIN-137 AC2: a locked press never enters gameplay. It reports back to the
        // owner so the prerequisite can be explained in place.
        if (!_isUnlocked)
        {
            // A refused press must not sound like an accepted one -- this path used to play the
            // same affirmative click as an unlocked level.
            AudioManager.Instance?.PlayLevelLockedDenied();
            DebugLogger.Log($"LevelButton: Level {_config.levelNumber} is locked");
            _lockedPressHandler?.Invoke(_config);
            return;
        }

        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log($"LevelButton: Level {_config.levelNumber} selected");

        if (ProgressManager.Instance == null || !ProgressManager.Instance.TrySetSelectedLevel(_config))
        {
            DebugLogger.LogWarning("LevelButton: Selected level could not be persisted.");
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.DiscardPausedRunSnapshot();
            GameManager.Instance.SetLevel(_config);
        }

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
            DebugLogger.LogError("LevelButton: SceneLoader not available. Cannot load Gameplay.");
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnPressed);
    }
}
