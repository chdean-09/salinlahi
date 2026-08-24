using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the active clue across its configured channels and reports whether the answer was
/// visible, which separates recognition from recall in learning evidence.
/// </summary>
[DisallowMultipleComponent]
public sealed class ActiveCluePresenter : MonoBehaviour
{
    [Header("HUD Clue Panel")]
    [SerializeField] private GameObject _cluePanelRoot;
    [SerializeField] private TextMeshProUGUI _clueText;
    [SerializeField] private Image _clueImage;
    [SerializeField] private GameObject _replayAudioButton;

    [Header("Active Clue Mark")]
    [Tooltip("Optional authored marker for the active enemy. A procedural ring is built when empty.")]
    [SerializeField] private GameObject _activeClueMarkPrefab;
    [SerializeField] private Vector2 _activeClueMarkOffset = Vector2.zero;
    [SerializeField] private float _activeClueMarkScale = 1.6f;

    [Header("Word Restoration Cue")]
    [Tooltip("Optional authored label for the at-accept word-restoration cue. "
             + "A runtime label is built when empty.")]
    [SerializeField] private TextMeshProUGUI _wordRestoredText;
    [Tooltip("How long the restored word stays on screen, in unscaled seconds.")]
    [SerializeField, Min(0f)] private float _wordRestoredDurationSeconds = 1.4f;

    /// <summary>
    /// Suppresses a clue announcement that lands on top of one CombatResolver just made.
    /// AudioManager uses PlayOneShot, so pronunciation clips overlap rather than interrupt.
    /// </summary>
    private const float PronunciationDebounceSeconds = 0.5f;

    /// <summary>Prefix on the at-accept cue, matching the victory summary's "Restored:" surface.</summary>
    private const string WordRestoredPrefix = "Restored: ";

    private ClueChannels _resolvedChannels = ClueChannels.Glyph;
    private Enemy _currentClue;
    private LevelConfigSO _level;
    private ActiveClueDirector _subscribedDirector;
    private Button _replayAudioButtonComponent;
    private float _lastPronunciationTime = float.NegativeInfinity;
    private GameObject _activeClueMark;
    private Sprite _runtimeMarkSprite;
    private GameObject _runtimeWordRestoredObject;
    private Coroutine _wordRestoredRoutine;
    private int _wordRestoredCueCount;
    private string _lastWordRestoredMessage;

    /// <summary>Reused by HandleActiveClueChanged so badge sweeps do not allocate per clue.</summary>
    private readonly System.Collections.Generic.List<Enemy> _badgeSweepBuffer =
        new System.Collections.Generic.List<Enemy>();

    /// <summary>
    /// True only when this level actually arms clue combat. Guards every presentation side
    /// effect, so a legacy level's glyph badges are never touched.
    /// </summary>
    private bool IsClueCombatArmed => _level != null && _level.activeClueCombatEnabled;

    public ClueChannels ResolvedChannels => _resolvedChannels;

    /// <summary>True when the glyph itself is on screen, making the attempt recognition.</summary>
    public bool AnswerWasVisible =>
        (_resolvedChannels & ClueChannels.Glyph) != ClueChannels.None;

    /// <summary>
    /// The channel-independent mark riding on the active enemy, or null while nothing is
    /// marked. Only ever created for a level that arms clue combat.
    /// </summary>
    public GameObject ActiveClueMark => _activeClueMark;

    /// <summary>
    /// The at-accept word-restoration label, or null until an accepted draw first needs one.
    /// Authored wiring wins; otherwise a runtime label is built on the HUD canvas.
    /// </summary>
    public TextMeshProUGUI WordRestoredLabel => _wordRestoredText;

    /// <summary>
    /// How many word-restoration cues this presenter has raised. Exists so a test can assert
    /// "exactly once per accepted draw" without reaching into coroutine or canvas state.
    /// </summary>
    public int WordRestoredCueCount => _wordRestoredCueCount;

    /// <summary>The text of the most recent word-restoration cue, or null before the first.</summary>
    public string LastWordRestoredMessage => _lastWordRestoredMessage;

    private void OnEnable()
    {
        SubscribeToDirector();
        BindReplayAudioButton();
        EventBus.OnPronunciationRequested += HandlePronunciationRequested;
        EventBus.OnEnemySpawned += HandleEnemySpawned;
    }

    private void Start()
    {
        SubscribeToDirector();

        if (_level == null && GameManager.Instance != null)
            ApplyLevel(GameManager.Instance.CurrentLevel);

        if (_subscribedDirector != null)
            HandleActiveClueChanged(null, _subscribedDirector.CurrentClue);
    }

    private void OnDisable()
    {
        EventBus.OnPronunciationRequested -= HandlePronunciationRequested;
        EventBus.OnEnemySpawned -= HandleEnemySpawned;
        DestroyActiveClueMark();
        DestroyRuntimeWordRestoredLabel();

        if (_subscribedDirector != null)
        {
            _subscribedDirector.OnActiveClueChanged -= HandleActiveClueChanged;
            _subscribedDirector.OnActiveClueResolved -= HandleActiveClueResolved;
        }
        _subscribedDirector = null;

        if (_replayAudioButtonComponent != null)
            _replayAudioButtonComponent.onClick.RemoveListener(ReplayAudio);
        _replayAudioButtonComponent = null;
    }

    /// <summary>Resolves this level's channels, including the visual audio fallback.</summary>
    public void ApplyLevel(LevelConfigSO level)
    {
        _level = level;
        _resolvedChannels = level == null
            ? ClueChannels.Glyph
            : ClueChannelResolver.Resolve(level.clueChannels, level.audioVisualFallback);

        // An Inspector-wired presenter runs OnEnable and Start before LevelFlowController
        // creates the director, so both earlier attempts found Instance null. Without this
        // the authored HUD path would silently never present a clue.
        SubscribeToDirector();

        if (Application.isPlaying && level != null && level.activeClueCombatEnabled)
            EnsureRuntimePanel();
        BindReplayAudioButton();

        if (_subscribedDirector != null)
            HandleActiveClueChanged(null, _subscribedDirector.CurrentClue);
    }

    private void SubscribeToDirector()
    {
        ActiveClueDirector director = ActiveClueDirector.Instance;
        if (director == null || _subscribedDirector == director)
            return;

        if (_subscribedDirector != null)
        {
            _subscribedDirector.OnActiveClueChanged -= HandleActiveClueChanged;
            _subscribedDirector.OnActiveClueResolved -= HandleActiveClueResolved;
        }

        _subscribedDirector = director;
        _subscribedDirector.OnActiveClueChanged += HandleActiveClueChanged;
        _subscribedDirector.OnActiveClueResolved += HandleActiveClueResolved;
    }

    /// <summary>
    /// Gives runtime-bootstrapped levels a usable clue panel when no Inspector wiring exists.
    /// Authored HUD references still win; this fallback is created only for an armed level.
    /// </summary>
    private void EnsureRuntimePanel()
    {
        if (_cluePanelRoot != null)
            return;

        Canvas canvas = ResolveHudCanvas();
        if (canvas == null)
            return;

        TextMeshProUGUI textTemplate = FindFirstObjectByType<TextMeshProUGUI>();

        // Editor-only: builtin UI resources are not included in player builds, so this is
        // null at runtime and the panel renders as flat untextured quads. Acceptable because
        // this whole panel is the no-Inspector-wiring fallback -- an authored HUD supplies
        // its own art and never reaches here. Do not ship a level relying on this path.
        // Batch mode: the builtin lookup logs an assert with no graphics device, which
        // fails any headless test that arms a level; the styling is invisible there anyway.
        Sprite defaultUiSprite = Application.isBatchMode
            ? null
            : Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

        GameObject panel = new GameObject("[Runtime] ActiveCluePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -120f);
        panelRect.sizeDelta = new Vector2(460f, 140f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = defaultUiSprite;
        panelImage.color = new Color(0.04f, 0.06f, 0.12f, 0.94f);
        panelImage.raycastTarget = false;
        panel.SetActive(false);

        GameObject textObject = new GameObject("[Runtime] ActiveClueText", typeof(RectTransform));
        textObject.transform.SetParent(panel.transform, false);
        TextMeshProUGUI clueText = textObject.AddComponent<TextMeshProUGUI>();
        CopyFont(textTemplate, clueText);
        clueText.fontSize = 28f;
        clueText.alignment = TextAlignmentOptions.Center;
        clueText.color = Color.white;
        clueText.raycastTarget = false;
        SetStretch(textObject.GetComponent<RectTransform>(), new Vector2(110f, 10f),
            new Vector2(-100f, -10f));

        GameObject imageObject = new GameObject("[Runtime] ActiveClueImage", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(panel.transform, false);
        Image clueImage = imageObject.GetComponent<Image>();
        clueImage.sprite = defaultUiSprite;
        clueImage.color = Color.white;
        clueImage.preserveAspect = true;
        clueImage.raycastTarget = false;
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0f, 0.5f);
        imageRect.anchorMax = new Vector2(0f, 0.5f);
        imageRect.pivot = new Vector2(0f, 0.5f);
        imageRect.anchoredPosition = new Vector2(12f, 0f);
        imageRect.sizeDelta = new Vector2(88f, 88f);
        imageObject.SetActive(false);

        GameObject buttonObject = new GameObject(
            "[Runtime] ActiveClueReplayButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel.transform, false);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.sprite = defaultUiSprite;
        buttonImage.color = new Color(0.18f, 0.45f, 0.76f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-12f, 0f);
        buttonRect.sizeDelta = new Vector2(82f, 44f);

        GameObject labelObject = new GameObject("[Runtime] ActiveClueReplayLabel", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        CopyFont(textTemplate, label);
        label.text = "Replay";
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        SetStretch(labelObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        _cluePanelRoot = panel;
        _clueText = clueText;
        _clueImage = clueImage;
        _replayAudioButton = buttonObject;
    }

    private static void CopyFont(TextMeshProUGUI source, TextMeshProUGUI target)
    {
        if (source == null || target == null || source.font == null)
            return;

        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
    }

    private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void BindReplayAudioButton()
    {
        Button nextButton = _replayAudioButton != null
            ? _replayAudioButton.GetComponent<Button>()
            : null;
        if (_replayAudioButtonComponent == nextButton)
            return;

        if (_replayAudioButtonComponent != null)
            _replayAudioButtonComponent.onClick.RemoveListener(ReplayAudio);

        _replayAudioButtonComponent = nextButton;
        if (_replayAudioButtonComponent != null)
            _replayAudioButtonComponent.onClick.AddListener(ReplayAudio);
    }

    private void ReplayAudio()
    {
        if (_currentClue != null && _currentClue.Character != null)
            EventBus.RaisePronunciationRequested(_currentClue.Character);
    }

    private void HandleActiveClueChanged(Enemy previous, Enemy current)
    {
        _currentClue = current;

        // Legacy levels keep every badge visible. Without this guard a (null, null) change --
        // raised from ApplyLevel and Start -- would sweep Hide() across every on-screen
        // enemy on a level that never armed clue combat.
        if (!IsClueCombatArmed)
            return;

        // Hide every non-active badge. EnemyGlyphBadge is normally visible for legacy combat,
        // so hiding only the previous clue would leak answers when the subsystem is enabled.
        bool showGlyph = (_resolvedChannels & ClueChannels.Glyph) != ClueChannels.None;
        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker != null)
        {
            tracker.FillActiveEnemiesSnapshot(_badgeSweepBuffer);
            for (int i = 0; i < _badgeSweepBuffer.Count; i++)
                ApplyBadgePolicy(_badgeSweepBuffer[i], current, showGlyph);
        }

        if (previous != null && previous != current && previous.GlyphBadge != null)
            previous.GlyphBadge.Hide();

        if (current != null && current.GlyphBadge != null)
        {
            if (showGlyph)
                current.GlyphBadge.Show();
            else
                current.GlyphBadge.Hide();
        }

        UpdateActiveClueMark(current);
        UpdateCluePanel(current);
    }

    /// <summary>One enemy's badge state under the current clue: the mark shows, everyone hides.</summary>
    private static void ApplyBadgePolicy(Enemy enemy, Enemy clue, bool showGlyph)
    {
        if (enemy == null || enemy.GlyphBadge == null)
            return;

        if (showGlyph && enemy == clue)
            enemy.GlyphBadge.Show();
        else
            enemy.GlyphBadge.Hide();
    }

    /// <summary>
    /// The mark latches, so an enemy that spawns mid-latch raises no clue change and the sweep
    /// in HandleActiveClueChanged never reaches it. Without this it walks on screen still
    /// showing its glyph answer.
    /// </summary>
    private void HandleEnemySpawned(Enemy enemy)
    {
        if (!IsClueCombatArmed)
            return;

        ApplyBadgePolicy(
            enemy, _currentClue, (_resolvedChannels & ClueChannels.Glyph) != ClueChannels.None);
    }

    /// <summary>
    /// Keeps the mark on the marked enemy as it advances. Inert until a mark exists, which only
    /// happens on a level that arms clue combat.
    /// </summary>
    private void LateUpdate()
    {
        if (_activeClueMark == null)
            return;

        if (!IsClueCombatArmed || _currentClue == null)
        {
            if (_activeClueMark.activeSelf)
                _activeClueMark.SetActive(false);
            return;
        }

        _activeClueMark.transform.position =
            _currentClue.transform.position + (Vector3)_activeClueMarkOffset;
    }

    /// <summary>
    /// Spec section 3.5: the mark is a marker treatment on the active enemy driven independently
    /// of channel, so a sound-only or text-only level still shows which enemy is the clue.
    /// </summary>
    private void UpdateActiveClueMark(Enemy clue)
    {
        if (clue == null)
        {
            if (_activeClueMark != null)
                _activeClueMark.SetActive(false);
            return;
        }

        EnsureActiveClueMark();
        if (_activeClueMark == null)
            return;

        _activeClueMark.transform.position =
            clue.transform.position + (Vector3)_activeClueMarkOffset;
        _activeClueMark.SetActive(true);
    }

    /// <summary>
    /// An authored prefab wins. The procedural ring is the no-art fallback, generated rather
    /// than taken from builtin resources so it also renders in a player build.
    /// </summary>
    private void EnsureActiveClueMark()
    {
        if (_activeClueMark != null)
            return;

        if (_activeClueMarkPrefab != null)
        {
            _activeClueMark = Instantiate(_activeClueMarkPrefab);
            _activeClueMark.name = "[Runtime] ActiveClueMark";
            _activeClueMark.SetActive(false);
            return;
        }

        _runtimeMarkSprite = CreateRingSprite();

        _activeClueMark = new GameObject("[Runtime] ActiveClueMark", typeof(SpriteRenderer));
        SpriteRenderer markRenderer = _activeClueMark.GetComponent<SpriteRenderer>();
        markRenderer.sprite = _runtimeMarkSprite;
        markRenderer.color = new Color(1f, 0.84f, 0.29f, 0.85f);
        markRenderer.sortingOrder = RenderOrder.ActiveClueMark;
        _activeClueMark.transform.localScale =
            new Vector3(_activeClueMarkScale, _activeClueMarkScale, 1f);
        _activeClueMark.SetActive(false);
    }

    /// <summary>A one world unit hollow ring, so the mark frames the enemy without hiding it.</summary>
    private static Sprite CreateRingSprite()
    {
        const int size = 64;
        const float outerRadius = 0.5f;
        const float innerRadius = 0.38f;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];
        var opaque = new Color32(255, 255, 255, 255);
        var clear = new Color32(255, 255, 255, 0);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = ((x + 0.5f) / size) - 0.5f;
                float dy = ((y + 0.5f) / size) - 0.5f;
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                pixels[(y * size) + x] =
                    distance <= outerRadius && distance >= innerRadius ? opaque : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void DestroyActiveClueMark()
    {
        Texture2D generatedTexture =
            _runtimeMarkSprite != null ? _runtimeMarkSprite.texture : null;

        DestroyOwnedObject(_activeClueMark);
        DestroyOwnedObject(_runtimeMarkSprite);
        DestroyOwnedObject(generatedTexture);

        _activeClueMark = null;
        _runtimeMarkSprite = null;
    }

    private static void DestroyOwnedObject(UnityEngine.Object owned)
    {
        if (owned == null)
            return;

        if (Application.isPlaying)
            Destroy(owned);
        else
            DestroyImmediate(owned);
    }

    private void UpdateCluePanel(Enemy clue)
    {
        if (_cluePanelRoot != null)
            _cluePanelRoot.SetActive(clue != null);

        if (_clueText != null)
        {
            bool showText = clue != null
                && (_resolvedChannels
                    & (ClueChannels.LatinText | ClueChannels.IncompleteWord)) != ClueChannels.None;
            _clueText.gameObject.SetActive(showText);
            if (showText)
                SetClueText(clue);
            else
                _clueText.text = string.Empty;
        }

        if (_clueImage != null)
        {
            // Resolve the sprite only when the channel is actually on, so a level that never
            // uses context images does no focus-word lookup and leaves the Image untouched.
            bool showImage = clue != null
                && (_resolvedChannels & ClueChannels.ContextImage) != ClueChannels.None;

            if (showImage)
            {
                FocusWordDefinition word = FindFocusWordContaining(clue.Character?.stableId);
                _clueImage.sprite = ResolveContextImage(word);
                showImage = _clueImage.sprite != null;
            }

            _clueImage.gameObject.SetActive(showImage);
        }

        if (_replayAudioButton != null)
        {
            _replayAudioButton.SetActive(
                clue != null
                && (_resolvedChannels & ClueChannels.SpokenAudio) != ClueChannels.None);
        }

        // Announce the new clue only if nothing else just did. When a hit resolves,
        // CombatResolver announces the character the player drew and the mark then moves,
        // which would stack a second overlapping clip -- AudioManager uses PlayOneShot.
        if (clue != null
            && (_resolvedChannels & ClueChannels.SpokenAudio) != ClueChannels.None
            && clue.Character != null
            && Time.unscaledTime - _lastPronunciationTime > PronunciationDebounceSeconds)
        {
            EventBus.RaisePronunciationRequested(clue.Character);
        }
    }

    /// <summary>
    /// Stamps every pronunciation on the bus, whoever raised it, so the presenter can tell
    /// when its own announcement would collide with one already playing.
    /// </summary>
    private void HandlePronunciationRequested(BaybayinCharacterSO character)
    {
        _lastPronunciationTime = Time.unscaledTime;
    }

    /// <summary>
    /// AC1's word-restoration half (SALIN-135). An accepted draw earns two answers: the combat
    /// response the enemy plays, and a language response saying which word just got its symbol
    /// back. Before this the only "Restored:" surface was the end-of-level summary, so a player
    /// mid-defense never saw the point of the symbol they had just drawn.
    ///
    /// Fired from ActiveClueDirector.TryConsumeClue, so it is already exactly-once per clue and
    /// an echoed recognition cannot double it.
    /// </summary>
    private void HandleActiveClueResolved(Enemy clue)
    {
        if (!IsClueCombatArmed || clue == null || clue.Character == null)
            return;

        // Legacy content and any symbol outside this level's focus words have nothing to
        // restore; staying silent beats announcing an empty word.
        FocusWordDefinition word = FindFocusWordContaining(clue.Character.stableId);
        if (word == null)
            return;

        string restored = BuildRestoredWordLabel(word);
        if (string.IsNullOrEmpty(restored))
            return;

        ShowWordRestoredCue(restored);
    }

    /// <summary>
    /// Prefers the authored display label, falling back to the Latin spelling that the masked
    /// IncompleteWord channel was hiding.
    /// </summary>
    private static string BuildRestoredWordLabel(FocusWordDefinition word)
    {
        if (word == null)
            return null;

        string spelling = !string.IsNullOrEmpty(word.displayLabel)
            ? word.displayLabel
            : word.latinSpelling;

        return string.IsNullOrEmpty(spelling) ? null : WordRestoredPrefix + spelling;
    }

    private void ShowWordRestoredCue(string message)
    {
        // Counted at the decision, not at the draw call: a HUD with no canvas to build on must
        // still be provably raising one cue per accepted draw and not two.
        _wordRestoredCueCount++;
        _lastWordRestoredMessage = message;

        EnsureWordRestoredLabel();
        if (_wordRestoredText == null)
            return;

        _wordRestoredText.text = message;
        _wordRestoredText.gameObject.SetActive(true);

        // A disabled presenter cannot run a coroutine. Leaving the label up is the harmless
        // outcome: OnDisable tears the runtime label down anyway.
        if (!isActiveAndEnabled)
            return;

        if (_wordRestoredRoutine != null)
            StopCoroutine(_wordRestoredRoutine);

        _wordRestoredRoutine = StartCoroutine(HideWordRestoredCueAfterDelay());
    }

    /// <summary>
    /// Unscaled so the cue still clears while the game is paused mid-flash, matching
    /// DrawingFeedback's flash timing.
    /// </summary>
    private IEnumerator HideWordRestoredCueAfterDelay()
    {
        float duration = Mathf.Max(0f, _wordRestoredDurationSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_wordRestoredText != null)
            _wordRestoredText.gameObject.SetActive(false);

        _wordRestoredRoutine = null;
    }

    /// <summary>
    /// Built independently of EnsureRuntimePanel so the cue also reaches an authored HUD that
    /// predates this ticket and therefore has no serialized reference to wire.
    /// </summary>
    private void EnsureWordRestoredLabel()
    {
        if (_wordRestoredText != null)
            return;

        Canvas canvas = ResolveHudCanvas();
        if (canvas == null)
            return;

        TextMeshProUGUI textTemplate = _clueText != null
            ? _clueText
            : FindFirstObjectByType<TextMeshProUGUI>();

        _runtimeWordRestoredObject =
            new GameObject("[Runtime] WordRestoredCue", typeof(RectTransform));
        _runtimeWordRestoredObject.transform.SetParent(canvas.transform, false);

        var label = _runtimeWordRestoredObject.AddComponent<TextMeshProUGUI>();
        CopyFont(textTemplate, label);
        label.fontSize = 32f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.84f, 0.29f, 1f);
        label.raycastTarget = false;

        RectTransform rect = _runtimeWordRestoredObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -280f);
        rect.sizeDelta = new Vector2(520f, 60f);

        _runtimeWordRestoredObject.SetActive(false);
        _wordRestoredText = label;
    }

    /// <summary>Same canvas search EnsureRuntimePanel uses, shared so the two agree.</summary>
    private Canvas ResolveHudCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            return canvas;

        GameObject hudCanvas = GameObject.Find("HUDCanvas");
        canvas = hudCanvas != null ? hudCanvas.GetComponent<Canvas>() : null;
        if (canvas != null)
            return canvas;

        return FindFirstObjectByType<Canvas>();
    }

    private void DestroyRuntimeWordRestoredLabel()
    {
        _wordRestoredRoutine = null;

        if (_runtimeWordRestoredObject == null)
        {
            // An authored label is not ours to destroy; just stop showing the last cue.
            if (_wordRestoredText != null)
                _wordRestoredText.gameObject.SetActive(false);
            return;
        }

        // Only the runtime label is presenter-owned, so only it is torn down.
        if (_wordRestoredText != null
            && _wordRestoredText.gameObject == _runtimeWordRestoredObject)
        {
            _wordRestoredText = null;
        }

        DestroyOwnedObject(_runtimeWordRestoredObject);
        _runtimeWordRestoredObject = null;
    }

    /// <summary>
    /// Word-specific image first, level-wide context image second. The level-wide image is a
    /// deliberately weaker cue -- it sets the scene rather than naming the word -- so it is
    /// only ever a fallback. Keeping it means a level authored with ContextImage but no
    /// per-word art still presents something rather than passing validation and showing
    /// nothing; SALIN-184 is expected to supply per-word art and retire the fallback.
    /// </summary>
    private Sprite ResolveContextImage(FocusWordDefinition word)
    {
        if (word?.media?.contextImage != null)
            return word.media.contextImage;

        return _level?.contextMedia?.contextImage;
    }

    private void SetClueText(Enemy clue)
    {
        if (_clueText == null || clue == null || clue.Character == null)
            return;

        FocusWordDefinition word = FindFocusWordContaining(clue.Character.stableId);
        if (word == null)
        {
            _clueText.text = string.Empty;
            return;
        }

        // IncompleteWord masks the target symbol's position; LatinText shows the whole word.
        bool masked = (_resolvedChannels & ClueChannels.IncompleteWord) != ClueChannels.None
                      && (_resolvedChannels & ClueChannels.LatinText) == ClueChannels.None;

        _clueText.text = masked
            ? BuildMaskedSpelling(word, clue.Character.stableId)
            : word.latinSpelling;
    }

    private FocusWordDefinition FindFocusWordContaining(string symbolStableId)
    {
        if (_level == null || _level.focusWords == null || string.IsNullOrEmpty(symbolStableId))
            return null;

        for (int i = 0; i < _level.focusWords.Count; i++)
        {
            FocusWordDefinition word = _level.focusWords[i];
            if (word?.decomposition == null)
                continue;

            for (int j = 0; j < word.decomposition.Count; j++)
            {
                SymbolValueReference reference = word.decomposition[j];
                if (reference?.symbol != null && reference.symbol.stableId == symbolStableId)
                    return word;
            }
        }

        return null;
    }

    /// <summary>
    /// Replaces the target symbol's syllable with an underscore run so the player must retrieve
    /// it rather than read it.
    /// </summary>
    private static string BuildMaskedSpelling(FocusWordDefinition word, string symbolStableId)
    {
        if (word?.decomposition == null)
            return word?.latinSpelling;

        var builder = new System.Text.StringBuilder();
        for (int i = 0; i < word.decomposition.Count; i++)
        {
            SymbolValueReference reference = word.decomposition[i];
            if (reference?.symbol == null)
                continue;

            builder.Append(reference.symbol.stableId == symbolStableId
                ? "__"
                : reference.symbol.syllable);
        }

        return builder.Length > 0 ? builder.ToString() : word.latinSpelling;
    }
}
