using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private float _activeClueMarkScale = 1.9f;

    // Off by default: the gold ring read as noise around the enemy art rather than as a marker.
    // The scroll badge above the enemy carries the "this is your target" job on levels that reveal
    // the glyph. Levels that do NOT reveal it have no other active-enemy marker, so the ring stays
    // switchable rather than deleted.
    [SerializeField] private bool _showActiveClueMark;

    /// <summary>Whether the ring marking the active enemy is drawn. Off by default.</summary>
    public bool ShowActiveClueMark
    {
        get => _showActiveClueMark;
        set => _showActiveClueMark = value;
    }

    [Header("Word Restoration Cue")]
    [Tooltip("Optional authored label for the at-accept word-restoration cue. "
             + "A runtime label is built when empty.")]
    [SerializeField] private TextMeshProUGUI _wordRestoredText;
    [Tooltip("How long the restored word stays on screen, in unscaled seconds.")]
    [SerializeField, Min(0f)] private float _wordRestoredDurationSeconds = 1.4f;

    [Header("Combat Restoration Progress")]
    [Tooltip("Optional authored label for the focus-word slots restored during combat. "
             + "A runtime label is built when empty on levels using the shared restoration path.")]
    [SerializeField] private TextMeshProUGUI _restorationProgressText;

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
    private GameObject _runtimeRestorationProgressObject;
    private Coroutine _wordRestoredRoutine;
    private int _wordRestoredCueCount;
    private string _lastWordRestoredMessage;
    private readonly ActiveClueRestorationState _restorationState =
        new ActiveClueRestorationState();

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

    /// <summary>The shared combat restoration state for this level attempt.</summary>
    public ActiveClueRestorationState RestorationState => _restorationState;

    /// <summary>True when the level has authored at least one focus word to restore.</summary>
    public bool HasRestorationWords => _restorationState.FocusWordCount > 0;

    /// <summary>Checks whether the requested focus words have all filled their slots.</summary>
    public bool AreRestorationWordsComplete(IReadOnlyList<string> stableIds)
        => _restorationState.AreWordsComplete(stableIds);

    /// <summary>Checks the exact word or syllable targets required by the current flow segment.</summary>
    public bool AreRestorationTargetsComplete(IReadOnlyList<ActiveClueRestorationTarget> targets)
        => _restorationState.AreTargetsComplete(targets);

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

        DestroyRuntimeRestorationProgressLabel();
    }

    /// <summary>Resolves this level's channels, including the visual audio fallback.</summary>
    public void ApplyLevel(LevelConfigSO level)
    {
        _level = level;
        _restorationState.Configure(level?.focusWords);
        _resolvedChannels = level == null
            ? ClueChannels.Glyph
            : ClueChannelResolver.Resolve(level.clueChannels, level.audioVisualFallback);

        // An Inspector-wired presenter runs OnEnable and Start before LevelFlowController
        // creates the director, so both earlier attempts found Instance null. Without this
        // the authored HUD path would silently never present a clue.
        SubscribeToDirector();

        if (Application.isPlaying && level != null && level.activeClueCombatEnabled)
            EnsureRuntimePanel();
        if (Application.isPlaying && level != null && level.activeClueRestorationEnabled)
            EnsureRestorationProgressLabel();
        BindReplayAudioButton();

        if (_subscribedDirector != null)
            HandleActiveClueChanged(null, _subscribedDirector.CurrentClue);
        UpdateRestorationProgress();
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
        Transform hudContainer = ResolveHudContainer(canvas);
        if (hudContainer == null)
            return;

        TextMeshProUGUI textTemplate = FindFirstObjectByType<TextMeshProUGUI>();

        // No builtin-sprite lookup: UISprite.psd lives in unity_builtin_extra, which
        // Resources.GetBuiltinResource cannot serve. A null sprite renders a flat tinted
        // quad, which is intentional here: this no-Inspector-wiring fallback remains readable
        // on mobile layouts without requiring an authored UI skin.
        Sprite defaultUiSprite = null;

        GameObject panel = new GameObject("[Runtime] ActiveCluePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(hudContainer, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -155f);
        panelRect.sizeDelta = new Vector2(760f, 180f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = defaultUiSprite;
        panelImage.color = new Color(0.04f, 0.06f, 0.12f, 0.94f);
        panelImage.raycastTarget = false;
        panel.SetActive(false);

        GameObject instructionObject =
            new GameObject("[Runtime] ActiveClueInstruction", typeof(RectTransform));
        instructionObject.transform.SetParent(panel.transform, false);
        TextMeshProUGUI instruction = instructionObject.AddComponent<TextMeshProUGUI>();
        CopyFont(textTemplate, instruction);
        instruction.text = "DRAW THE GLOWING SYMBOL TO DEFEND";
        instruction.fontSize = 24f;
        instruction.alignment = TextAlignmentOptions.Center;
        instruction.color = new Color(1f, 0.84f, 0.29f, 1f);
        instruction.raycastTarget = false;
        SetStretch(instructionObject.GetComponent<RectTransform>(), new Vector2(118f, 18f),
            new Vector2(-118f, -18f));

        GameObject textObject = new GameObject("[Runtime] ActiveClueText", typeof(RectTransform));
        textObject.transform.SetParent(panel.transform, false);
        TextMeshProUGUI clueText = textObject.AddComponent<TextMeshProUGUI>();
        CopyFont(textTemplate, clueText);
        clueText.fontSize = 38f;
        clueText.alignment = TextAlignmentOptions.Center;
        clueText.color = Color.white;
        clueText.raycastTarget = false;
        SetStretch(textObject.GetComponent<RectTransform>(), new Vector2(118f, 18f),
            new Vector2(-118f, -62f));

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

    /// <summary>
    /// Keeps the runtime clue inside the safe gameplay HUD instead of placing it directly on
    /// the canvas. Authored scenes use HUDLayer; the canvas fallback preserves bootstrapped
    /// levels and tests that do not include the full HUD hierarchy.
    /// </summary>
    private static Transform ResolveHudContainer(Canvas canvas)
    {
        GameObject hudLayer = GameObject.Find("HUDLayer");
        if (hudLayer != null)
            return hudLayer.transform;

        GameObject hudRoot = GameObject.Find("HUDRoot");
        if (hudRoot != null)
            return hudRoot.transform;

        return canvas != null ? canvas.transform : null;
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
        UpdateRestorationProgress();
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
        if (!_showActiveClueMark)
        {
            if (_activeClueMark != null)
                _activeClueMark.SetActive(false);
            return;
        }

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
        markRenderer.color = new Color(1f, 0.84f, 0.29f, 1f);
        markRenderer.sortingOrder = RenderOrder.ActiveClueMark;
        _activeClueMark.transform.localScale =
            new Vector3(_activeClueMarkScale, _activeClueMarkScale, 1f);
        _activeClueMark.SetActive(false);
    }

    /// <summary>A one world unit hollow ring, so the mark frames the enemy without hiding it.</summary>
    private static Sprite CreateRingSprite()
    {
        const int size = 128;
        const float outerRadius = 0.5f;
        const float innerRadius = 0.41f;

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

        // The at-accept cue predates the shared restoration gate and remains useful on
        // Level 1, which still owns its authored post-wave challenge. Do not let that legacy
        // presentation path mutate the new slot state or unmask its combat clue text.
        if (_level == null || !_level.activeClueRestorationEnabled)
        {
            FocusWordDefinition legacyWord = FindFocusWordContaining(clue.Character.stableId);
            string legacyMessage = BuildRestoredWordLabel(
                legacyWord == null
                    ? null
                    : new[] { legacyWord });
            if (!string.IsNullOrEmpty(legacyMessage))
                ShowWordRestoredCue(legacyMessage);
            return;
        }

        // Legacy content and any symbol outside this level's focus words have nothing to
        // restore; staying silent beats announcing an empty word. The state marks every
        // matching slot, so a symbol shared by both focus words fills both target texts.
        IReadOnlyList<FocusWordDefinition> changedWords =
            _restorationState.Apply(clue.Character.stableId);
        if (changedWords.Count == 0)
            return;

        string restored = BuildRestoredWordLabel(changedWords);
        if (string.IsNullOrEmpty(restored))
            return;

        // The clue stays latched through the pronunciation lead. Refreshing the panel here
        // makes the accepted syllable appear in the target text before the enemy leaves.
        UpdateCluePanel(_currentClue);
        UpdateRestorationProgress();
        ShowWordRestoredCue(restored);
    }

    /// <summary>
    /// Prefers the authored display label, falling back to the Latin spelling that the masked
    /// IncompleteWord channel was hiding.
    /// </summary>
    private static string BuildRestoredWordLabel(IReadOnlyList<FocusWordDefinition> words)
    {
        if (words == null || words.Count == 0)
            return null;

        var labels = new List<string>(words.Count);
        for (int i = 0; i < words.Count; i++)
        {
            FocusWordDefinition word = words[i];
            if (word == null)
                continue;

            string spelling = !string.IsNullOrEmpty(word.displayLabel)
                ? word.displayLabel
                : word.latinSpelling;
            if (!string.IsNullOrEmpty(spelling))
                labels.Add(spelling);
        }

        return labels.Count == 0
            ? null
            : WordRestoredPrefix + string.Join(", ", labels);
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
    /// Builds the persistent target-text readout used by the shared combat-restoration path.
    /// Unlike the timed "Restored:" cue, this stays visible for the whole defense so a player
    /// can see each syllable fill instead of waiting for a post-wave board.
    /// </summary>
    private void EnsureRestorationProgressLabel()
    {
        if (_restorationProgressText != null)
            return;

        Canvas canvas = ResolveHudCanvas();
        Transform hudContainer = ResolveHudContainer(canvas);
        if (hudContainer == null)
            return;

        TextMeshProUGUI textTemplate = _clueText != null
            ? _clueText
            : FindFirstObjectByType<TextMeshProUGUI>();

        _runtimeRestorationProgressObject =
            new GameObject("[Runtime] ActiveClueRestorationProgress", typeof(RectTransform));
        _runtimeRestorationProgressObject.transform.SetParent(hudContainer, false);

        TextMeshProUGUI label = _runtimeRestorationProgressObject.AddComponent<TextMeshProUGUI>();
        CopyFont(textTemplate, label);
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        RectTransform rect = _runtimeRestorationProgressObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -345f);
        rect.sizeDelta = new Vector2(760f, 105f);

        _runtimeRestorationProgressObject.SetActive(false);
        _restorationProgressText = label;
    }

    private void UpdateRestorationProgress()
    {
        bool shouldShow = IsClueCombatArmed
            && _level != null
            && _level.activeClueRestorationEnabled
            && HasRestorationWords;

        if (!shouldShow)
        {
            if (_restorationProgressText != null)
                _restorationProgressText.gameObject.SetActive(false);
            return;
        }

        EnsureRestorationProgressLabel();
        if (_restorationProgressText == null)
            return;

        _restorationProgressText.text = BuildRestorationProgressText();
        _restorationProgressText.gameObject.SetActive(true);
    }

    private string BuildRestorationProgressText()
    {
        var builder = new System.Text.StringBuilder();
        IReadOnlyList<FocusWordDefinition> words = _restorationState.FocusWords;
        for (int wordIndex = 0; wordIndex < words.Count; wordIndex++)
        {
            FocusWordDefinition word = words[wordIndex];
            if (word == null)
                continue;

            if (builder.Length > 0)
                builder.Append('\n');

            string label = !string.IsNullOrEmpty(word.displayLabel)
                ? word.displayLabel
                : word.latinSpelling;
            builder.Append(label).Append(": ");

            bool wroteSlot = false;
            for (int slotIndex = 0;
                 word.decomposition != null && slotIndex < word.decomposition.Count;
                 slotIndex++)
            {
                SymbolValueReference reference = word.decomposition[slotIndex];
                if (reference?.symbol == null)
                    continue;

                if (wroteSlot)
                    builder.Append(" · ");

                builder.Append(_restorationState.IsSlotRestored(word, slotIndex)
                    ? SpokenValueResolver.ResolveLabel(reference.symbol, reference.spokenValueId)
                    : UnreadableSlotMask);
                wroteSlot = true;
            }

            if (_restorationState.IsWordComplete(word.stableId))
                builder.Append("  ✓");
        }

        return builder.ToString();
    }

    private void DestroyRuntimeRestorationProgressLabel()
    {
        if (_runtimeRestorationProgressObject == null)
        {
            if (_restorationProgressText != null)
                _restorationProgressText.gameObject.SetActive(false);
            return;
        }

        if (_restorationProgressText != null
            && _restorationProgressText.gameObject == _runtimeRestorationProgressObject)
        {
            _restorationProgressText = null;
        }

        DestroyOwnedObject(_runtimeRestorationProgressObject);
        _runtimeRestorationProgressObject = null;
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

        // SALIN-284: Abo ng Simula ashes the word's first slot on top of the target mask. Read
        // here and passed down rather than consulted inside BuildMaskedSpelling, so that method
        // stays a pure function of its arguments and can be tested without a live enemy.
        _clueText.text = masked
            ? BuildMaskedSpellingWithRestoration(
                word,
                clue.Character.stableId,
                AshFirstSlotController.IsAnyActive(),
                _restorationState)
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
    /// The run that stands in for a slot the player may not read. Slot 0 under Abo ng Simula
    /// deliberately reuses the target slot's own mask rather than introducing a second, invented
    /// ash glyph: AUDIT.md:464 specifies the ability as "the clue's incomplete-word text masks the
    /// <b>first</b> syllable as well as the target one", which is exactly this.
    /// </summary>
    private const string UnreadableSlotMask = "__";

    /// <summary>
    /// Replaces the target symbol's syllable with an underscore run so the player must retrieve
    /// it rather than read it.
    /// <para>
    /// SALIN-284: when <paramref name="ashFirstSlot"/> is set, the word's first slot is masked too
    /// — Abo ng Simula covers the opening symbol with ash. Presentation only; nothing downstream of
    /// this string decides whether a draw is accepted.
    /// </para>
    /// </summary>
    private static string BuildMaskedSpelling(
        FocusWordDefinition word,
        string symbolStableId,
        bool ashFirstSlot)
    {
        return BuildMaskedSpellingWithRestoration(word, symbolStableId, ashFirstSlot, null);
    }

    private static string BuildMaskedSpellingWithRestoration(
        FocusWordDefinition word,
        string symbolStableId,
        bool ashFirstSlot,
        ActiveClueRestorationState restorationState)
    {
        if (word?.decomposition == null)
            return word?.latinSpelling;

        var builder = new System.Text.StringBuilder();
        // Counts slots actually emitted, not raw list indices: a decomposition may carry a null
        // symbol, and the ash belongs on the first slot the player can see.
        int emittedSlots = 0;
        for (int i = 0; i < word.decomposition.Count; i++)
        {
            SymbolValueReference reference = word.decomposition[i];
            if (reference?.symbol == null)
                continue;

            bool isTargetSlot = reference.symbol.stableId == symbolStableId;
            bool isAshedSlot = ashFirstSlot && emittedSlots == 0;
            bool isRestoredSlot = restorationState != null
                && restorationState.IsSlotRestored(word, i);
            emittedSlots++;

            // SALIN-221: unmasked slots read the word-context spoken value, so INA spells "i__"
            // rather than "e/i__".
            builder.Append((isTargetSlot && !isRestoredSlot) || isAshedSlot
                ? UnreadableSlotMask
                : SpokenValueResolver.ResolveLabel(reference.symbol, reference.spokenValueId));
        }

        return builder.Length > 0 ? builder.ToString() : word.latinSpelling;
    }
}

/// <summary>One authored focus-word target for the shared combat-restoration gate.</summary>
public sealed class ActiveClueRestorationTarget
{
    public ActiveClueRestorationTarget(string wordStableId, string symbolStableId = null)
    {
        WordStableId = wordStableId;
        SymbolStableId = symbolStableId;
    }

    public string WordStableId { get; }
    public string SymbolStableId { get; }
}

/// <summary>
/// Tracks the focus-word syllables restored by accepted active clues.
///
/// The combat system only reports a canonical symbol stable id, so a restored slot is
/// deliberately keyed by that id rather than by a challenge-board occurrence. This keeps
/// the state shared by every active-clue level and makes it independent of the retired
/// post-wave restoration board.
/// </summary>
public sealed class ActiveClueRestorationState
{
    private sealed class WordState
    {
        public readonly FocusWordDefinition Word;
        public readonly bool[] RestoredSlots;

        public WordState(FocusWordDefinition word)
        {
            Word = word;
            RestoredSlots = word?.decomposition == null
                ? new bool[0]
                : new bool[word.decomposition.Count];
        }

        public bool IsComplete
        {
            get
            {
                if (Word == null || Word.decomposition == null || Word.decomposition.Count == 0)
                    return false;

                bool hasSlot = false;
                for (int i = 0; i < Word.decomposition.Count; i++)
                {
                    SymbolValueReference reference = Word.decomposition[i];
                    if (reference?.symbol == null)
                        continue;

                    hasSlot = true;
                    if (!RestoredSlots[i])
                        return false;
                }

                return hasSlot;
            }
        }
    }

    private readonly List<WordState> _words = new List<WordState>();
    private readonly List<FocusWordDefinition> _changedWords =
        new List<FocusWordDefinition>();

    /// <summary>All focus words in authored order.</summary>
    public IReadOnlyList<FocusWordDefinition> FocusWords
    {
        get
        {
            var words = new List<FocusWordDefinition>(_words.Count);
            for (int i = 0; i < _words.Count; i++)
                words.Add(_words[i].Word);
            return words;
        }
    }

    public int FocusWordCount => _words.Count;

    /// <summary>True only when at least one word exists and every authored slot is restored.</summary>
    public bool IsComplete
    {
        get
        {
            if (_words.Count == 0)
                return false;

            for (int i = 0; i < _words.Count; i++)
            {
                if (!_words[i].IsComplete)
                    return false;
            }

            return true;
        }
    }

    /// <summary>Starts a fresh restoration run for the supplied level's focus words.</summary>
    public void Configure(IReadOnlyList<FocusWordDefinition> focusWords)
    {
        _words.Clear();
        _changedWords.Clear();

        if (focusWords == null)
            return;

        for (int i = 0; i < focusWords.Count; i++)
        {
            FocusWordDefinition word = focusWords[i];
            if (word != null)
                _words.Add(new WordState(word));
        }
    }

    /// <summary>
    /// Restores every matching slot in the focus words and returns only words changed by this
    /// call. The returned list is reused on the next call and is intended for immediate use.
    /// </summary>
    public IReadOnlyList<FocusWordDefinition> Apply(string symbolStableId)
    {
        _changedWords.Clear();
        if (string.IsNullOrEmpty(symbolStableId))
            return _changedWords;

        for (int wordIndex = 0; wordIndex < _words.Count; wordIndex++)
        {
            WordState state = _words[wordIndex];
            bool changed = false;
            if (state.Word.decomposition == null)
                continue;

            for (int slotIndex = 0; slotIndex < state.Word.decomposition.Count; slotIndex++)
            {
                SymbolValueReference reference = state.Word.decomposition[slotIndex];
                if (reference?.symbol == null
                    || reference.symbol.stableId != symbolStableId
                    || state.RestoredSlots[slotIndex])
                {
                    continue;
                }

                state.RestoredSlots[slotIndex] = true;
                changed = true;
            }

            if (changed)
                _changedWords.Add(state.Word);
        }

        return _changedWords;
    }

    public bool IsWordComplete(string stableId)
    {
        WordState state = FindWord(stableId);
        return state != null && state.IsComplete;
    }

    /// <summary>
    /// Checks a segment's required word ids. An unknown id is incomplete rather than silently
    /// satisfied, so a bad segment mapping cannot unlock the next phase.
    /// </summary>
    public bool AreWordsComplete(IReadOnlyList<string> stableIds)
    {
        if (stableIds == null || stableIds.Count == 0)
            return false;

        for (int i = 0; i < stableIds.Count; i++)
        {
            if (!IsWordComplete(stableIds[i]))
                return false;
        }

        return true;
    }

    public bool AreTargetsComplete(IReadOnlyList<ActiveClueRestorationTarget> targets)
    {
        if (targets == null || targets.Count == 0)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            ActiveClueRestorationTarget target = targets[i];
            if (target == null || !IsTargetComplete(target.WordStableId, target.SymbolStableId))
                return false;
        }

        return true;
    }

    public bool IsTargetComplete(string wordStableId, string symbolStableId)
    {
        if (string.IsNullOrEmpty(symbolStableId))
            return IsWordComplete(wordStableId);

        WordState state = FindWord(wordStableId);
        if (state == null || state.Word.decomposition == null)
            return false;

        for (int i = 0; i < state.Word.decomposition.Count; i++)
        {
            SymbolValueReference reference = state.Word.decomposition[i];
            if (reference?.symbol != null
                && reference.symbol.stableId == symbolStableId)
            {
                return state.RestoredSlots[i];
            }
        }

        return false;
    }

    public bool IsSlotRestored(FocusWordDefinition word, int slotIndex)
    {
        WordState state = FindWord(word);
        return state != null
            && slotIndex >= 0
            && slotIndex < state.RestoredSlots.Length
            && state.RestoredSlots[slotIndex];
    }

    private WordState FindWord(string stableId)
    {
        if (string.IsNullOrEmpty(stableId))
            return null;

        for (int i = 0; i < _words.Count; i++)
        {
            if (_words[i].Word != null && _words[i].Word.stableId == stableId)
                return _words[i];
        }

        return null;
    }

    private WordState FindWord(FocusWordDefinition word)
    {
        if (word == null)
            return null;

        for (int i = 0; i < _words.Count; i++)
        {
            if (ReferenceEquals(_words[i].Word, word))
                return _words[i];
        }

        return null;
    }
}
