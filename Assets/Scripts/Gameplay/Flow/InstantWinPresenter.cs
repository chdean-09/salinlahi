using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The instant-win beat (level-01 design plan §2 beat B10). Plays when the last slot of the
/// target text fills, between <see cref="WaveManager"/> abandoning the authored wave list and
/// its ordinary CompleteRun.
///
/// THE BEAT, AND WHY ITS ORDER IS NOT NEGOTIABLE:
///   1. the slots join and the text flashes whole — the player's own last drawing is credited;
///   2. time freezes at a near-zero timeScale with the remaining enemies mid-stride;
///   3. the board HOLDS there, frozen, for a per-level duration;
///   4. the banner states that the text is whole and the wave no longer matters;
///   5. the remaining enemies dissolve into light — they are never slashed;
///   6. control returns to the caller, which completes the run.
///
/// STEP 3 IS THE ENTIRE POINT. The rule being taught is that filling the text wins the level
/// and surviving a wave is not a win condition. A player only learns that if they register
/// enemies ALIVE at the moment of victory, so the frozen board must be legible and must be
/// held long enough to read. Shorten it and the dissolve reads as an ordinary wave clear; skip
/// it and the beat teaches nothing that a fade to the victory screen would not.
///
/// Everything that follows from that is a constraint, not a style choice:
///   - the overlay NEVER dims the board and never blocks raycasts. WaveClearedScreenUI's
///     near-opaque black card is right for a screen whose subject is the card; here the
///     subject is the enemies behind it, and dimming them defeats the beat.
///   - the freeze is a near-zero timeScale, not zero. At exactly zero the enemies' walk
///     animation stops dead and the board reads as a paused game — a system state — rather
///     than as living enemies caught mid-stride.
///   - every wait in here is REALTIME (WaitForSecondsRealtime / Time.unscaledDeltaTime).
///     Scaled waits inside a 0.05 timeScale would run twenty times long, which is how a
///     1.5 s hold becomes a 30 s hang. Precedent: HeartLossDemoBeat.PostHitSlowMo.
///   - the enemies are returned to the pool, NOT killed. A kill would credit a drawing the
///     player never made, raise OnEnemyDefeated, and feed the level's own metrics.
///
/// NOT SCENE-WIRED, by the same reasoning as WaveClearedScreenUI (SALIN-232) and
/// LevelContentMissingPanel (SALIN-223): WaveManager finds one if a scene authored it and
/// builds one on demand otherwise, so no .unity asset has to be touched to ship the beat.
/// The tuning below therefore ships as C# defaults that already match the design plan's §6
/// table; authoring the component into a scene overrides them from the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class InstantWinPresenter : MonoBehaviour
{
    /// <summary>
    /// One level's frozen-hold duration. The hold is per-level by design, not a constant: the
    /// design plan's §6 guidance-fade table fades it out across the campaign
    /// (1.5 s / 1.0 s / 0.5 s / off / off for levels 1-5), because it is teaching scaffolding
    /// and a player who has already learned the win rule does not need to be stopped again.
    /// </summary>
    [System.Serializable]
    public sealed class LevelFrozenHold
    {
        [Tooltip("LevelConfigSO.levelNumber this hold applies to.")]
        public int levelNumber;

        [Tooltip("Seconds to hold the frozen board, in real time. 0 turns the hold off for "
                 + "this level, which is what the design plan's fade table asks for from "
                 + "level 4 on.")]
        [Min(0f)] public float holdSeconds;
    }

    [Header("Step 1 — the text flashes whole")]
    [Tooltip("Seconds per flash half-cycle (on, then off) of the completed target text.")]
    [Min(0f)] [SerializeField] private float _textFlashHalfCycleSeconds = 0.14f;

    [Tooltip("How many times the completed target text flashes before time freezes.")]
    [Min(0)] [SerializeField] private int _textFlashCount = 3;

    [Header("Step 2 — the freeze")]
    [Tooltip("timeScale held during the beat. Near-zero, never zero: at exactly zero the "
             + "enemies stop animating and the frozen board reads as a paused game rather "
             + "than as living enemies caught mid-stride.")]
    [Range(0.01f, 0.5f)] [SerializeField] private float _frozenTimeScale = 0.05f;

    [Header("Step 3 — the frozen hold, per level")]
    [Tooltip("Frozen-hold duration per level number. Defaults are the design plan's §6 fade "
             + "table. A level not listed here uses the fallback below.")]
    [SerializeField]
    private List<LevelFrozenHold> _frozenHoldByLevel = new List<LevelFrozenHold>
    {
        new LevelFrozenHold { levelNumber = 1, holdSeconds = 1.5f },
        new LevelFrozenHold { levelNumber = 2, holdSeconds = 1.0f },
        new LevelFrozenHold { levelNumber = 3, holdSeconds = 0.5f },
        new LevelFrozenHold { levelNumber = 4, holdSeconds = 0f },
        new LevelFrozenHold { levelNumber = 5, holdSeconds = 0f },
    };

    [Tooltip("Frozen-hold duration for any level number missing from the table above. Zero: "
             + "the hold is teaching scaffolding, and the table fades it to nothing by level "
             + "4, so an unlisted later level should not reintroduce it.")]
    [Min(0f)] [SerializeField] private float _fallbackFrozenHoldSeconds = 0f;

    [Header("Step 4 — the banner")]
    [Tooltip("Seconds the banner reads on its own, after the hold and before the dissolve "
             + "begins.")]
    [Min(0f)] [SerializeField] private float _bannerSeconds = 1.2f;

    [Header("Step 5 — the dissolve")]
    [Tooltip("Seconds the remaining enemies take to dissolve into light.")]
    [Min(0.01f)] [SerializeField] private float _dissolveSeconds = 0.9f;

    [Tooltip("Colour the enemies brighten into as they dissolve. Light, not fire: the beat "
             + "reads as the text reclaiming them, not as Juan finishing them off.")]
    [SerializeField] private Color _dissolveLightColor = new Color(1f, 0.98f, 0.86f, 1f);

    [Tooltip("Optional burst VFX spawned once per dissolving enemy. Left empty the dissolve "
             + "still plays as a brighten-and-fade on the enemy's own sprites, so the beat "
             + "never depends on scene or prefab wiring.")]
    [SerializeField] private GameObject _dissolveVfxPrefab;

    [Header("Presentation")]
    [Tooltip("Canvas sorting order. Below WaveClearedScreenUI's 300 so a completion screen "
             + "still draws over this beat rather than under it.")]
    [SerializeField] private int _canvasSortingOrder = 280;

    [Tooltip("Font size of the restored target text line.")]
    [Min(1f)] [SerializeField] private float _restoredTextFontSize = 72f;

    [Tooltip("Font size of the instant-win banner.")]
    [Min(1f)] [SerializeField] private float _bannerFontSize = 46f;

    private GameObject _overlayRoot;
    private TMP_Text _restoredTextLabel;
    private TMP_Text _bannerLabel;

    // Set while Play holds a dipped timeScale, so Cancel knows whether it owns the restore.
    private bool _timeScaleDipped;
    private float _timeScaleBeforeDip = 1f;

    // Set while Play holds drawing suppression, so only the beat that took it releases it.
    private bool _drawingSuppressed;

    private readonly List<Enemy> _dissolvingEnemies = new List<Enemy>();
    private readonly List<SpriteRenderer> _dissolvingSprites = new List<SpriteRenderer>();
    private readonly List<Color> _dissolvingSpriteColors = new List<Color>();
    private readonly List<TMP_Text> _dissolvingLabels = new List<TMP_Text>();
    private readonly List<Color> _dissolvingLabelColors = new List<Color>();
    private readonly List<GameObject> _dissolveVfxInstances = new List<GameObject>();

    /// <summary>True while the beat is on screen. Read by the caller and by tests.</summary>
    public bool IsPresenting { get; private set; }

    private void Awake()
    {
        if (!IsPresenting)
            HideOverlay();
    }

    private void OnDisable()
    {
        // A disabled or destroyed host never runs the coroutine's finally block, so the dipped
        // timeScale would outlive the beat and every later scene would crawl.
        Cancel();
    }

    /// <summary>
    /// Plays the whole instant-win beat and returns when the board is clear and normal time
    /// has been restored. The caller completes the run afterwards, through the same path a
    /// full clear uses.
    /// </summary>
    /// <param name="restorationSource">
    /// The HUD presenter, read only to compose the restored-text line and celebrate the final
    /// occurrence. It can render either the scene objective or the legacy focus-word projection.
    /// Null is tolerated: the beat still freezes, holds and dissolves, because the win rule is
    /// what it teaches and the text line is decoration on top.
    /// </param>
    /// <param name="levelNumber">
    /// LevelConfigSO.levelNumber, which selects this level's frozen-hold duration.
    /// </param>
    public IEnumerator Play(ActiveCluePresenter restorationSource, int levelNumber)
    {
        IsPresenting = true;

        try
        {
            EnsureOverlay();
            ShowOverlay(BuildRestoredTextLine(restorationSource));

            // Step 1 proper: flash the REAL target-text rail, not just this overlay's copy of the
            // line. The rail is the thing the player has been filling all level, so it is the only
            // surface where "the slots join and the text flashes whole" actually reads as their own
            // four drawings completing. The overlay line stays as the readable restatement beneath
            // it, and as the sole presentation on a level whose HUD has no rail to celebrate.
            if (restorationSource != null)
                restorationSource.CelebrateRestorationComplete();

            // Drawing closes for the duration of the beat. The level is already won, so no
            // further drawing can change its outcome — but an accepted drawing during the
            // frozen hold would SLASH one of the enemies the beat is about to dissolve, and
            // step 5 is the whole difference between "the text reclaimed them" and an
            // ordinary kill.
            SuppressDrawing(true);

            yield return FlashRestoredText();

            DipTimeScale();

            // THE HOLD. Realtime, so the dipped timeScale cannot stretch it.
            float hold = ResolveFrozenHoldSeconds(levelNumber);
            if (hold > 0f)
                yield return new WaitForSecondsRealtime(hold);

            if (_bannerLabel != null)
                _bannerLabel.text = InstantWinCopy.BannerLabel;

            if (_bannerSeconds > 0f)
                yield return new WaitForSecondsRealtime(_bannerSeconds);

            yield return DissolveRemainingEnemies();
        }
        finally
        {
            SuppressDrawing(false);
            RestoreTimeScale();
            HideOverlay();
            IsPresenting = false;
        }
    }

    /// <summary>
    /// Tears the beat down without finishing it: restores normal time, restores every sprite
    /// colour the dissolve had part-faded, and clears the overlay. Called when the run is
    /// aborted or lost underneath the beat, and from OnDisable.
    ///
    /// Deliberately does NOT return the part-dissolved enemies to the pool. The paths that
    /// cancel this beat (game over, attempt aborted) already return every active enemy
    /// themselves, and returning them twice would check the same instance out of the pool
    /// twice on the next spawn.
    /// </summary>
    public void Cancel()
    {
        RestoreDissolveVisuals();
        DestroyDissolveVfx();
        _dissolvingEnemies.Clear();
        SuppressDrawing(false);
        RestoreTimeScale();
        HideOverlay();
        IsPresenting = false;
    }

    /// <summary>
    /// This level's frozen-hold duration, from the serialized per-level table.
    /// Exposed so a test can pin the §6 fade table without replaying the beat.
    /// </summary>
    public float ResolveFrozenHoldSeconds(int levelNumber)
    {
        if (_frozenHoldByLevel != null)
        {
            for (int i = 0; i < _frozenHoldByLevel.Count; i++)
            {
                LevelFrozenHold entry = _frozenHoldByLevel[i];
                if (entry != null && entry.levelNumber == levelNumber)
                    return Mathf.Max(0f, entry.holdSeconds);
            }
        }

        return Mathf.Max(0f, _fallbackFrozenHoldSeconds);
    }

    private IEnumerator FlashRestoredText()
    {
        if (_restoredTextLabel == null || _textFlashCount <= 0 || _textFlashHalfCycleSeconds <= 0f)
            yield break;

        // Alpha rather than enabled/disabled: toggling the GameObject rebuilds the TMP mesh
        // every half-cycle, which shows up as a one-frame reflow on a long target text.
        for (int i = 0; i < _textFlashCount; i++)
        {
            SetRestoredTextAlpha(0.25f);
            yield return new WaitForSecondsRealtime(_textFlashHalfCycleSeconds);
            SetRestoredTextAlpha(1f);
            yield return new WaitForSecondsRealtime(_textFlashHalfCycleSeconds);
        }
    }

    private void SetRestoredTextAlpha(float alpha)
    {
        if (_restoredTextLabel == null)
            return;

        Color color = _restoredTextLabel.color;
        color.a = Mathf.Clamp01(alpha);
        _restoredTextLabel.color = color;
    }

    /// <summary>
    /// Takes and releases drawing suppression, tracking ownership so a beat that never took it
    /// cannot release someone else's. GameManager's latch is a plain bool shared with the flow
    /// machine's preview beats; the Defense phase has already released it by the time this beat
    /// can run, so taking it here cannot mask a hold that belongs to another beat.
    /// </summary>
    private void SuppressDrawing(bool suppressed)
    {
        if (suppressed == _drawingSuppressed)
            return;

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        _drawingSuppressed = suppressed;
        gameManager.SuppressDrawingInput(suppressed);
    }

    private void DipTimeScale()
    {
        if (_timeScaleDipped)
            return;

        _timeScaleBeforeDip = Time.timeScale;
        _timeScaleDipped = true;
        Time.timeScale = Mathf.Clamp(_frozenTimeScale, 0.01f, 0.5f);
    }

    private void RestoreTimeScale()
    {
        if (!_timeScaleDipped)
            return;

        _timeScaleDipped = false;

        // A zero recorded scale means the game was paused when the beat started, and restoring
        // zero would leave the player in a pause nothing can lift. Normal time is the only
        // safe answer. Same remedy as HeartLossDemoBeat.PostHitSlowMo.
        Time.timeScale = _timeScaleBeforeDip <= 0f ? 1f : _timeScaleBeforeDip;
    }

    /// <summary>
    /// Brightens every remaining enemy into light and fades it out, then returns it to the
    /// pool. No kill, no slash, no OnEnemyDefeated: the player drew four syllables and the
    /// level is over, and crediting them with kills they never made would also feed the
    /// level's own completion metrics.
    /// </summary>
    private IEnumerator DissolveRemainingEnemies()
    {
        CollectDissolveTargets();

        if (_dissolvingEnemies.Count == 0)
            yield break;

        SpawnDissolveVfx();

        float duration = Mathf.Max(0.01f, _dissolveSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            ApplyDissolveProgress(elapsed / duration);

            // Unscaled: the board is frozen at _frozenTimeScale and a scaled dissolve would
            // take twenty times as long as authored.
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyDissolveProgress(1f);

        // Restore the colours BEFORE returning: a pooled enemy keeps its renderer state, so a
        // faded sprite would come back invisible on the next level's first spawn.
        RestoreDissolveVisuals();
        DestroyDissolveVfx();
        ReturnDissolvedEnemies();
    }

    private void CollectDissolveTargets()
    {
        ClearDissolveBuffers();

        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null)
            return;

        List<Enemy> active = tracker.GetActiveEnemiesSnapshot();
        for (int i = 0; i < active.Count; i++)
        {
            Enemy enemy = active[i];

            // The boss is excluded on purpose. A boss level's completion is BossController's,
            // and dissolving the boss out from under it would end the encounter through a
            // path that never reports the boss defeated.
            if (enemy == null || enemy.IsBoss)
                continue;

            _dissolvingEnemies.Add(enemy);

            SpriteRenderer[] sprites = enemy.GetComponentsInChildren<SpriteRenderer>(true);
            for (int s = 0; s < sprites.Length; s++)
            {
                if (sprites[s] == null)
                    continue;

                _dissolvingSprites.Add(sprites[s]);
                _dissolvingSpriteColors.Add(sprites[s].color);
            }

            // The glyph badge's romanization label is world-space TMP, not a sprite. Left out
            // it would hang in the air after the body it belongs to has gone.
            TMP_Text[] labels = enemy.GetComponentsInChildren<TMP_Text>(true);
            for (int l = 0; l < labels.Length; l++)
            {
                if (labels[l] == null)
                    continue;

                _dissolvingLabels.Add(labels[l]);
                _dissolvingLabelColors.Add(labels[l].color);
            }
        }
    }

    private void ApplyDissolveProgress(float progress)
    {
        float t = Mathf.Clamp01(progress);

        // Brighten first, fade second, so the enemy visibly turns to light instead of simply
        // going transparent. The brighten completes at the halfway point.
        float toLight = Mathf.Clamp01(t * 2f);
        float alpha = 1f - t;

        for (int i = 0; i < _dissolvingSprites.Count; i++)
        {
            SpriteRenderer sprite = _dissolvingSprites[i];
            if (sprite == null)
                continue;

            Color original = _dissolvingSpriteColors[i];
            Color lit = Color.Lerp(original, _dissolveLightColor, toLight);
            lit.a = original.a * alpha;
            sprite.color = lit;
        }

        for (int i = 0; i < _dissolvingLabels.Count; i++)
        {
            TMP_Text label = _dissolvingLabels[i];
            if (label == null)
                continue;

            Color original = _dissolvingLabelColors[i];
            original.a *= alpha;
            label.color = original;
        }
    }

    private void RestoreDissolveVisuals()
    {
        for (int i = 0; i < _dissolvingSprites.Count; i++)
        {
            if (_dissolvingSprites[i] != null)
                _dissolvingSprites[i].color = _dissolvingSpriteColors[i];
        }

        for (int i = 0; i < _dissolvingLabels.Count; i++)
        {
            if (_dissolvingLabels[i] != null)
                _dissolvingLabels[i].color = _dissolvingLabelColors[i];
        }

        _dissolvingSprites.Clear();
        _dissolvingSpriteColors.Clear();
        _dissolvingLabels.Clear();
        _dissolvingLabelColors.Clear();
    }

    private void ReturnDissolvedEnemies()
    {
        EnemyPool pool = EnemyPool.Instance;
        for (int i = 0; i < _dissolvingEnemies.Count; i++)
        {
            Enemy enemy = _dissolvingEnemies[i];
            if (enemy == null)
                continue;

            if (pool != null)
                pool.Return(enemy);
            else
                enemy.gameObject.SetActive(false);
        }

        _dissolvingEnemies.Clear();
    }

    private void SpawnDissolveVfx()
    {
        if (_dissolveVfxPrefab == null)
            return;

        for (int i = 0; i < _dissolvingEnemies.Count; i++)
        {
            Enemy enemy = _dissolvingEnemies[i];
            if (enemy == null)
                continue;

            _dissolveVfxInstances.Add(
                Instantiate(_dissolveVfxPrefab, enemy.transform.position, Quaternion.identity));
        }
    }

    private void DestroyDissolveVfx()
    {
        // Destroyed explicitly rather than with Destroy(go, delay): that delay is scaled game
        // time, so at a 0.05 timeScale the instances would linger twenty times as long.
        for (int i = 0; i < _dissolveVfxInstances.Count; i++)
        {
            if (_dissolveVfxInstances[i] != null)
                Destroy(_dissolveVfxInstances[i]);
        }

        _dissolveVfxInstances.Clear();
    }

    private void ClearDissolveBuffers()
    {
        _dissolvingEnemies.Clear();
        _dissolvingSprites.Clear();
        _dissolvingSpriteColors.Clear();
        _dissolvingLabels.Clear();
        _dissolvingLabelColors.Clear();
        _dissolveVfxInstances.Clear();
    }

    /// <summary>
    /// The finished target text, read back from the restoration state the win was decided on
    /// rather than re-derived from config, so the line can never disagree with the HUD about
    /// which words were restored.
    /// </summary>
    private static string BuildRestoredTextLine(ActiveCluePresenter restorationSource)
    {
        if (restorationSource == null)
            return InstantWinCopy.RestoredTextUnavailableLabel;

        if (restorationSource.UsesRestorationObjectiveDefinition)
        {
            RestorationObjectiveDefinition definition =
                restorationSource.RestorationObjective?.State?.Definition;
            if (definition?.units != null)
            {
                var objectiveText = new System.Text.StringBuilder();
                for (int unitIndex = 0; unitIndex < definition.units.Count; unitIndex++)
                {
                    RestorationObjectiveUnit unit = definition.units[unitIndex];
                    if (unit?.tokens == null)
                        continue;

                    for (int tokenIndex = 0; tokenIndex < unit.tokens.Count; tokenIndex++)
                    {
                        RestorationObjectiveToken token = unit.tokens[tokenIndex];
                        if (token == null)
                            continue;

                        if (token.kind == RestorationTokenKind.Literal)
                            objectiveText.Append(token.literalText);
                        else
                        {
                            string label = SpokenValueResolver.ResolveLabel(
                                token.target?.symbol, token.SpokenValueId);
                            objectiveText.Append(string.IsNullOrWhiteSpace(label)
                                ? token.target?.symbol?.characterID ?? string.Empty
                                : label.ToUpperInvariant());
                        }
                    }

                    // Word objectives have separate units without literal separators, so keep
                    // their readable word boundary. Marked/hidden context units already author
                    // the exact sentence spacing in their literal tokens and must concatenate
                    // byte-for-byte here (for example, "u" + "NAng").
                    if (unitIndex < definition.units.Count - 1
                        && (definition.displayMode == RestorationDisplayMode.GuidedWords
                            || definition.displayMode == RestorationDisplayMode.ClueOnlyWords))
                    {
                        objectiveText.Append(' ');
                    }
                }

                if (objectiveText.Length > 0)
                    return objectiveText.ToString();
            }
        }

        IReadOnlyList<FocusWordDefinition> words = restorationSource.RestorationState.FocusWords;
        var labels = new List<string>(words.Count);
        for (int i = 0; i < words.Count; i++)
        {
            FocusWordDefinition word = words[i];
            if (word == null)
                continue;

            string label = !string.IsNullOrEmpty(word.displayLabel)
                ? word.displayLabel
                : word.latinSpelling;
            if (!string.IsNullOrEmpty(label))
                labels.Add(label);
        }

        return labels.Count == 0
            ? InstantWinCopy.RestoredTextUnavailableLabel
            : string.Join("  ", labels);
    }

    private void ShowOverlay(string restoredText)
    {
        if (_overlayRoot == null)
            return;

        if (_restoredTextLabel != null)
        {
            _restoredTextLabel.text = restoredText;
            SetRestoredTextAlpha(1f);
        }

        // Blank until step 4. The banner states the win rule, and a banner already on screen
        // during the hold tells the player the answer before the frozen board has asked the
        // question.
        if (_bannerLabel != null)
            _bannerLabel.text = string.Empty;

        _overlayRoot.SetActive(true);
    }

    private void HideOverlay()
    {
        if (_overlayRoot != null)
            _overlayRoot.SetActive(false);
    }

    private void EnsureOverlay()
    {
        if (_overlayRoot != null && _restoredTextLabel != null && _bannerLabel != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "[Runtime] InstantWinCanvas",
                typeof(Canvas), typeof(CanvasScaler));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transform.SetParent(canvas.transform, false);
        }
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, _canvasSortingOrder);

        _overlayRoot = gameObject;

        RectTransform overlayRect = gameObject.GetComponent<RectTransform>();
        if (overlayRect == null)
            overlayRect = gameObject.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;

        // NO Image, NO dim, NO raycast blocker — see the class banner. The frozen enemies have
        // to stay plainly visible through this overlay, and a CanvasGroup that swallowed input
        // would also swallow the pause button during the hold.
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (_restoredTextLabel == null)
        {
            _restoredTextLabel = CreateLabel(
                transform,
                "RestoredTextLabel",
                new Vector2(0.5f, 1f),
                new Vector2(0f, -170f),
                new Vector2(900f, 140f),
                _restoredTextFontSize);
        }

        if (_bannerLabel == null)
        {
            _bannerLabel = CreateLabel(
                transform,
                "InstantWinBanner",
                new Vector2(0.5f, 0f),
                new Vector2(0f, 220f),
                new Vector2(900f, 180f),
                _bannerFontSize);
        }
    }

    // The banner sits low and the restored text sits high so the middle band of the screen —
    // where the frozen enemies are — stays uncovered.
    private static TMP_Text CreateLabel(
        Transform parent,
        string name,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = string.Empty;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        label.color = Color.white;
        TutorialFontProvider.ApplyTo(label);
        return label;
    }
}
