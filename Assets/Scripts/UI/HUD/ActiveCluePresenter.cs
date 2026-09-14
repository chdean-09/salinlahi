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

    [Tooltip("Enemies within this many world units of the active clue hide their glyph, so two "
             + "scrolls never overlap. Everything further away keeps its glyph.")]
    [SerializeField, Min(0f)] private float _badgeCrowdRadius = 2.5f;

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
    [Tooltip("Optional authored label that used to print the restoration readout as text. It is "
             + "now only a font template for the runtime rail, and its own GameObject is switched "
             + "off: the printed readout named each focus word in Latin, which is exactly the "
             + "reading crutch Abo ng Simula's ash exists to take away.")]
    [SerializeField] private TextMeshProUGUI _restorationProgressText;

    [Header("Restoration Slot Rail")]
    [Tooltip("Where the rail sits under the HUD container, anchored to the top centre.\n\n"
             + "Sits ABOVE the clue panel rather than at the old readout's spot. The rail is "
             + "roughly 98px tall, and at the readout's -345 it occupied the 345-443 band, which "
             + "overlapped both ActiveCluePanel (230-410) and FeedbackMessage (320-410) by about "
             + "65px. It is also the element that should read first: the target text is what the "
             + "player is filling, so it belongs across the top, with the clue panel beneath it.\n\n"
             + "MEASURED, NOT VERIFIED BY EYE. Confirm on a notched device — the rail parents to "
             + "HUDLayer, which SafeAreaHandler insets at runtime, so its real top edge moves down "
             + "by the device inset while FullScreenOverlay siblings do not.")]
    [SerializeField] private Vector2 _railAnchoredPosition = new Vector2(0f, -120f);

    [Tooltip("Size of one target-text slot in canvas units. Slots are square by authoring "
             + "convention but the two axes are separate so a wide glyph can be given room.")]
    [SerializeField] private Vector2 _slotSize = new Vector2(62f, 62f);

    [Tooltip("Gap between two slots inside the same focus word, in canvas units. Small: slots of "
             + "one word have to read as one text rather than as separate collectables.")]
    [SerializeField, Min(0f)] private float _slotSpacing = 9f;

    [Tooltip("Gap between two focus words' slot groups, in canvas units. Must be clearly wider "
             + "than the slot spacing — the grouping is what tells the player INA AMA is two "
             + "words and not one run of four symbols.")]
    [SerializeField, Min(0f)] private float _wordGap = 46f;

    [Tooltip("How far the Baybayin glyph is inset inside its slot, in canvas units, so the frame "
             + "stays visible around a filled slot.")]
    [SerializeField, Min(0f)] private float _slotGlyphInset = 6f;

    [Tooltip("Frame colour of a slot that is still waiting for its symbol.")]
    [SerializeField] private Color _emptySlotColor = new Color(1f, 1f, 1f, 0.22f);

    [Tooltip("Frame colour of a slot whose symbol has been restored.")]
    [SerializeField] private Color _filledSlotColor = new Color(1f, 0.84f, 0.29f, 0.85f);

    [Tooltip("Tint applied to the restored slot's glyph. The glyph art is white, so this is the "
             + "colour the player actually reads the symbol in.")]
    [SerializeField] private Color _filledGlyphColor = Color.white;

    [Tooltip("Thickness of a slot frame's border as a fraction of the slot, used to generate the "
             + "hollow frame sprite. The frame is generated rather than authored because there is "
             + "no slot art in the project and an empty slot must not read as a filled block.")]
    [SerializeField, Range(0.02f, 0.4f)] private float _slotFrameBorderFraction = 0.09f;

    [Header("Rail Latin Labels")]
    // Default OFF, and §2 B1 asks for bare slots: the player's first model of the level has to be
    // "fill these", which a Latin word sitting beside the slots answers for them. It is also the
    // whole of Abo ng Simula's ability — the ash masks the clue panel's Latin spelling, and a rail
    // printing "INA" next to the masked clue hands that reading back for free, which made the
    // level's signature ability cosmetic. A word that is already COMPLETE is exempt below: there is
    // nothing left to leak once every slot of it is filled, and §2 B10 wants the finished text
    // readable as one word.
    [Tooltip("Prints each focus word's Latin spelling beside its slots while the word is still "
             + "incomplete. OFF by default — bare slots. Turning it on is refused on any level "
             + "whose roster can mask the clue, because there it would give back the exact "
             + "reading the mask removed.")]
    [SerializeField] private bool _showLatinWordLabels;

    [Tooltip("Font size of a focus word's Latin label on the rail.")]
    [SerializeField, Min(1f)] private float _latinWordLabelFontSize = 24f;

    [Tooltip("Colour of a focus word's Latin label on the rail.")]
    [SerializeField] private Color _latinWordLabelColor = new Color(1f, 0.84f, 0.29f, 1f);

    [Tooltip("Height reserved above the slots for the Latin labels, in canvas units. The row is "
             + "reserved even while every label is hidden: a word completing mid-level would "
             + "otherwise grow the rail and move every slot, and DrawFeedbackPresenter flies a "
             + "badge to a slot rect that must not travel while the badge is in the air.")]
    [SerializeField, Min(0f)] private float _latinWordLabelRowHeight = 30f;

    [Tooltip("Gap between the Latin label row and the slots below it, in canvas units.")]
    [SerializeField, Min(0f)] private float _latinWordLabelGap = 6f;

    [Header("Rail Completion Flash")]
    [Tooltip("How many times the whole rail flashes when the target text completes. Zero shows "
             + "the finished rail without a flash.")]
    [SerializeField, Min(0)] private int _railFlashCount = 3;

    [Tooltip("Seconds of one half cycle of the completion flash, in unscaled time. The instant-win "
             + "beat dips the time scale, so a scaled flash would crawl.")]
    [SerializeField, Min(0f)] private float _railFlashHalfCycleSeconds = 0.12f;

    [Tooltip("Alpha the rail dips to at the bottom of a completion flash. Above zero so the "
             + "finished text never fully disappears at the moment it is being celebrated.")]
    [SerializeField, Range(0f, 1f)] private float _railFlashDipAlpha = 0.3f;

    [Header("Ash Crumble")]
    [Tooltip("Seconds the clue stays fully readable after an Abo's ash arms, covering the gust's "
             + "travel. The clue must be readable right up to the frame the ash lands, or the "
             + "gust stops being the reason the letters went away. Lead plus duration below "
             + "should equal AshGustController's gust duration (0.6 s).")]
    [SerializeField, Min(0f)] private float _clueCrumbleLeadSeconds = 0.35f;

    [Tooltip("Seconds the readable characters take to crumble into their mask. Zero renders the "
             + "masked string immediately, which is also what a non-playing context does.")]
    [SerializeField, Min(0f)] private float _clueCrumbleDurationSeconds = 0.25f;

    [Tooltip("Fraction of the crumble spent fading the doomed characters out before the mask "
             + "fades in. Below one the two overlap, so the slot is never blank.")]
    [SerializeField, Range(0.1f, 1f)] private float _clueCrumbleHandoff = 0.6f;

    [Tooltip("Fraction of the crumble that each character lags behind the one to its left, so "
             + "the run comes apart left to right instead of dissolving as one block.")]
    [SerializeField, Range(0f, 0.9f)] private float _clueCrumbleCharacterStagger = 0.2f;

    [Tooltip("How far a crumbling character sinks as it fades, in em of the clue's font size. "
             + "Ash falls; the offset is what makes the fade read as crumbling rather than as a "
             + "dimmed label.")]
    [SerializeField] private float _clueCrumbleDropEm = 0.5f;

    [Tooltip("How far each mask character rises into place as it fades in, in em. Small: the "
             + "mask is settling ash, not an arriving object.")]
    [SerializeField] private float _clueCrumbleMaskRiseEm = 0.22f;

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
    private Coroutine _clueCrumbleRoutine;
    private int _wordRestoredCueCount;
    private string _lastWordRestoredMessage;

    /// <summary>
    /// One built slot on the target-text rail. Holds the authored slot it stands for, so the rail
    /// can be repainted from restoration state without rebuilding, and both of its graphics, so a
    /// repaint touches no component lookups.
    /// </summary>
    private sealed class RailSlot
    {
        public FocusWordDefinition Word;
        public int DecompositionIndex;
        public RectTransform Anchor;
        public Image Frame;
        public Image Glyph;
    }

    /// <summary>One focus word's group on the rail, kept so its Latin label can be repainted.</summary>
    private sealed class RailWord
    {
        public FocusWordDefinition Word;
        public TextMeshProUGUI LatinLabel;
    }

    private readonly List<RailSlot> _railSlots = new List<RailSlot>();
    private readonly List<RailWord> _railWords = new List<RailWord>();

    /// <summary>
    /// The rail's slot rects in flattened reading order, handed out through
    /// <see cref="RestorationSlotAnchors"/>. Held as its own list rather than projected on demand
    /// so a caller polling it every frame allocates nothing.
    /// </summary>
    private readonly List<RectTransform> _railSlotAnchors = new List<RectTransform>();

    private GameObject _railRoot;
    private CanvasGroup _railCanvasGroup;
    private Sprite _runtimeSlotFrameSprite;
    private Coroutine _railFlashRoutine;

    /// <summary>
    /// Cached answer to "can this level's roster mask the clue", which decides whether the Latin
    /// labels may be switched on at all. Computed once per level: the roster cannot change during
    /// a run, and the check walks every wave.
    /// </summary>
    private bool? _levelMasksTheClue;

    /// <summary>
    /// Last observed ash state, so the onset can be spotted. Without this the ash would only
    /// appear on the next clue change: the ability arms while an Abo walks, which raises no clue
    /// event, and the panel would keep showing the readable spelling until something else moved.
    /// </summary>
    private bool _ashWasActive;

    /// <summary>
    /// Set only for the refresh raised by the ash onset, so the crumble animates exactly there
    /// and every other path through SetClueText stays an immediate assignment.
    /// </summary>
    private bool _animateClueCrumble;
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

    /// <summary>
    /// The enabled presenter, for ability code that must read target-text progress without owning
    /// a reference to the HUD. A plain static handle rather than a singleton base class: an
    /// ability lives on a pooled enemy shell and has to cope with there being no presenter at all
    /// on a level that never arms clue combat.
    /// </summary>
    public static ActiveCluePresenter Active { get; private set; }

    /// <summary>Test seam: stand in for the OnEnable that EditMode never runs.</summary>
    public static void SetActiveForTests(ActiveCluePresenter presenter) => Active = presenter;

    /// <summary>How many target-text slots the player has already restored.</summary>
    public int RestoredSlotCount => _restorationState.RestoredSlotCount;

    /// <summary>
    /// The 1-based position, inside its own focus word, of the slot the target text needs next —
    /// or zero when every slot is filled or the level has no focus words.
    ///
    /// <para>
    /// "Needed next" is the leftmost unrestored slot over the focus words in authored order, which
    /// is the same flattening <see cref="SpawnAssignmentCoordinator"/> builds its slot list from
    /// and, at Level 1's <c>activeSlotWindow</c> of one, the same slot its director calls the
    /// cursor. Exposed as a position within the word rather than as a global index because that is
    /// the fact abilities care about: Abo ng Simula's ash masks a word's first slot, so it only
    /// changes anything while the needed slot is <b>not</b> its word's first.
    /// </para>
    /// </summary>
    public int NeededSlotPositionInWord
    {
        get
        {
            if (_level?.focusWords == null)
                return 0;

            for (int wordIndex = 0; wordIndex < _level.focusWords.Count; wordIndex++)
            {
                FocusWordDefinition word = _level.focusWords[wordIndex];
                if (word?.decomposition == null)
                    continue;

                // Counts emitted slots, not raw list indices: a decomposition may carry a null
                // symbol, and the spawn schedule skips those too.
                int position = 0;
                for (int slotIndex = 0; slotIndex < word.decomposition.Count; slotIndex++)
                {
                    SymbolValueReference reference = word.decomposition[slotIndex];
                    if (reference?.symbol == null || string.IsNullOrEmpty(reference.symbol.stableId))
                        continue;

                    position++;
                    if (!_restorationState.IsSlotRestored(word, slotIndex))
                        return position;
                }
            }

            return 0;
        }
    }

    /// <summary>True when the level has authored at least one focus word to restore.</summary>
    public bool HasRestorationWords => _restorationState.FocusWordCount > 0;

    /// <summary>Checks whether the requested focus words have all filled their slots.</summary>
    public bool AreRestorationWordsComplete(IReadOnlyList<string> stableIds)
        => _restorationState.AreWordsComplete(stableIds);

    /// <summary>Checks the exact word or syllable targets required by the current flow segment.</summary>
    public bool AreRestorationTargetsComplete(IReadOnlyList<ActiveClueRestorationTarget> targets)
        => _restorationState.AreTargetsComplete(targets);

    /// <summary>
    /// The rail's slot rects in flattened reading order — focus word 0's emitted syllables left to
    /// right, then focus word 1's — which is the same flattening
    /// <see cref="SpawnAssignmentCoordinator"/> and the draw-feedback report number their slots by.
    /// Empty until the rail is built, which only happens in play mode on a level that arms the
    /// shared restoration path.
    ///
    /// <para>
    /// The list is live: the rail is rebuilt on a level change, so a caller holding the returned
    /// reference keeps seeing the current slots, but an index captured across a rebuild is not
    /// guaranteed to name the same rect. Read it, fly to it, drop it.
    /// </para>
    /// </summary>
    public IReadOnlyList<RectTransform> RestorationSlotAnchors => _railSlotAnchors;

    /// <summary>
    /// One rail slot's rect by flattened slot index, or null when the index is outside the target
    /// text or the rail does not exist. Null rather than an exception because the callers are HUD
    /// presenters reacting to a combat report: a slot index they cannot resolve means "do not fly
    /// the badge", never "fail the draw".
    /// </summary>
    public RectTransform GetRestorationSlotAnchor(int flattenedSlotIndex)
    {
        if (flattenedSlotIndex < 0 || flattenedSlotIndex >= _railSlotAnchors.Count)
            return null;

        return _railSlotAnchors[flattenedSlotIndex];
    }

    /// <summary>
    /// Shows the whole rail as one finished text and flashes it — §2 B10 step 1, the beat that
    /// turns four separately filled slots into the word the player just restored. Every slot is
    /// painted restored and every focus word's Latin label is revealed, so the join reads whole
    /// even on the frame the last fill arrives, and the completed word is readable because a
    /// complete word has no remaining answer to leak.
    ///
    /// <para>
    /// Returns false when there is no rail to celebrate — a level that never armed the restoration
    /// path, or a non-playing context. Callers use that to fall back to their own presentation
    /// rather than to hold on an empty screen.
    /// </para>
    /// </summary>
    public bool CelebrateRestorationComplete()
    {
        if (_railRoot == null)
            return false;

        RepaintRail(forceRestored: true);
        _railRoot.SetActive(true);

        if (_railFlashRoutine != null)
        {
            StopCoroutine(_railFlashRoutine);
            _railFlashRoutine = null;
        }

        // A disabled presenter cannot run the flash. Showing the finished rail is the useful half
        // of this call, so it still counts as celebrated rather than reporting failure.
        if (!isActiveAndEnabled || _railCanvasGroup == null)
            return true;

        _railFlashRoutine = StartCoroutine(FlashRail());
        return true;
    }

    /// <summary>
    /// Unscaled seconds <see cref="CelebrateRestorationComplete"/> spends flashing, so a caller
    /// sequencing a win beat can hold for exactly as long as the rail is still moving instead of
    /// guessing at a duration that would drift the moment the flash is retuned.
    /// </summary>
    public float RestorationCelebrationDurationSeconds =>
        Mathf.Max(0, _railFlashCount) * Mathf.Max(0f, _railFlashHalfCycleSeconds) * 2f;

    private void OnEnable()
    {
        Active = this;
        _ashWasActive = AshFirstSlotController.IsAnyActive();
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
        // Only clear the handle if it still points at us, so a scene bringing up a replacement
        // presenter is not left with a null one when the old presenter tears down after it.
        if (Active == this)
            Active = null;

        _clueCrumbleRoutine = null;
        _railFlashRoutine = null;
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

        DestroyRestorationRail();
    }

    /// <summary>Resolves this level's channels, including the visual audio fallback.</summary>
    public void ApplyLevel(LevelConfigSO level)
    {
        _level = level;
        _restorationState.Configure(level?.focusWords);

        // A new level means a new roster and a new target text, so both cached answers about the
        // old one are dropped: the mask verdict is recomputed on demand and the rail is rebuilt
        // from the incoming focus words rather than repainted over the previous level's slots.
        _levelMasksTheClue = null;
        DestroyRestorationRail();

        _resolvedChannels = level == null
            ? ClueChannels.Glyph
            : ClueChannelResolver.Resolve(level.clueChannels, level.audioVisualFallback);

        // An Inspector-wired presenter runs OnEnable and Start before LevelFlowController
        // creates the director, so both earlier attempts found Instance null. Without this
        // the authored HUD path would silently never present a clue.
        SubscribeToDirector();

        if (Application.isPlaying && level != null && level.activeClueCombatEnabled)
            EnsureRuntimePanel();
        // Built here rather than on the first fill: §2 B1 requires the target text to be standing
        // on screen as empty slots before the first enemy walks on, because the player's opening
        // mental model has to be "fill this", not "kill those".
        if (Application.isPlaying && level != null && level.activeClueRestorationEnabled)
            EnsureRestorationRail();
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
        instruction.fontSize = 40f;
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
    private void ApplyBadgePolicy(Enemy enemy, Enemy clue, bool showGlyph)
    {
        if (enemy == null || enemy.GlyphBadge == null)
            return;

        if (!showGlyph)
        {
            enemy.GlyphBadge.Hide();
            return;
        }

        if (enemy == clue)
        {
            enemy.GlyphBadge.Show();
            return;
        }

        // Previously every enemy but the clue was hidden, so the field showed exactly one scroll and
        // the player could not read what was coming. The reason to hide any of them is overlap: a
        // scroll sitting right on top of the clue's makes both unreadable. So hide only the crowd
        // within _badgeCrowdRadius of the clue and let everything further up the field keep its glyph.
        bool crowdsTheClue = clue != null
            && Vector2.Distance(enemy.transform.position, clue.transform.position) <= _badgeCrowdRadius;

        if (crowdsTheClue)
            enemy.GlyphBadge.Hide();
        else
            enemy.GlyphBadge.Show();
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
        WatchAshOnset();

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

        // One panel, one voice. The cue label and the standing "DRAW THE GLOWING SYMBOL TO DEFEND"
        // instruction occupy overlapping bands of the same clue panel, so on a successful draw
        // "Restored: INA" printed straight through "DEFEND" and neither could be read. The
        // instruction is the one that has nothing to say at that moment — the player has just done
        // the thing it asks for — so it stands down for the length of the cue and comes back with
        // it. See HideWordRestoredCueAfterDelay.
        SetClueInstructionVisible(false);

        // A disabled presenter cannot run a coroutine, so nothing would ever bring the instruction
        // back. Restore it now and leave the cue up: OnDisable tears the runtime label down anyway.
        if (!isActiveAndEnabled)
        {
            SetClueInstructionVisible(true);
            return;
        }

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

        SetClueInstructionVisible(true);
        _wordRestoredRoutine = null;
    }

    /// <summary>
    /// Shows or hides the clue panel's standing instruction line, wherever it came from: the
    /// authored HUD calls it <c>DrawGlowingSymbolInstruction</c> and
    /// <see cref="EnsureRuntimePanel"/>'s no-wiring fallback calls it
    /// <c>[Runtime] ActiveClueInstruction</c>. Matched on the shared "Instruction" in the name
    /// rather than on a serialized reference, so this works on a HUD authored before the cue
    /// existed and needs nobody to rewire a scene. A panel with no such child is simply left alone.
    /// </summary>
    private void SetClueInstructionVisible(bool visible)
    {
        TextMeshProUGUI instruction = ResolveClueInstruction();
        if (instruction != null && instruction.gameObject.activeSelf != visible)
            instruction.gameObject.SetActive(visible);
    }

    private TextMeshProUGUI ResolveClueInstruction()
    {
        if (_clueInstructionText != null)
            return _clueInstructionText;

        if (_cluePanelRoot == null)
            return null;

        TextMeshProUGUI[] candidates =
            _cluePanelRoot.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == null || candidates[i] == _clueText)
                continue;

            if (candidates[i].name.IndexOf("Instruction", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            _clueInstructionText = candidates[i];
            return _clueInstructionText;
        }

        return null;
    }

    private TextMeshProUGUI _clueInstructionText;

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
    /// Builds the persistent target-text rail used by the shared combat-restoration path: one
    /// visual slot per flattened target slot, grouped per focus word, so INA AMA stands on screen
    /// as [ ][ ] [ ][ ] before anything has been drawn.
    ///
    /// <para>
    /// Built from the level's focus words rather than from authored children. Every level plays in
    /// the one Gameplay scene, so hand-authored slots would have to be a fixed count that happened
    /// to match whichever level was loaded; generating them means the rail is correct for a
    /// two-slot level and a nine-slot one with no scene work at all.
    /// </para>
    /// </summary>
    private void EnsureRestorationRail()
    {
        if (_railRoot != null)
            return;

        IReadOnlyList<FocusWordDefinition> words = _restorationState.FocusWords;
        if (words.Count == 0)
            return;

        Canvas canvas = ResolveHudCanvas();
        Transform hudContainer = ResolveHudContainer(canvas);
        if (hudContainer == null)
            return;

        // The old printed readout is the font template and nothing else. Switching its own object
        // off is deliberate: an authored HUD may still carry it, and left alive it would keep
        // showing whatever string it last held beside a rail that has replaced it.
        TextMeshProUGUI fontTemplate = _restorationProgressText != null
            ? _restorationProgressText
            : _clueText;
        if (fontTemplate == null)
            fontTemplate = FindFirstObjectByType<TextMeshProUGUI>();
        if (_restorationProgressText != null)
            _restorationProgressText.gameObject.SetActive(false);

        _runtimeSlotFrameSprite = CreateSlotFrameSprite(_slotFrameBorderFraction);

        _railRoot = new GameObject(
            "[Runtime] ActiveClueRestorationRail", typeof(RectTransform), typeof(CanvasGroup));
        _railRoot.transform.SetParent(hudContainer, false);
        _railCanvasGroup = _railRoot.GetComponent<CanvasGroup>();
        _railCanvasGroup.blocksRaycasts = false;
        _railCanvasGroup.interactable = false;

        RectTransform railRect = _railRoot.GetComponent<RectTransform>();
        railRect.anchorMin = new Vector2(0.5f, 1f);
        railRect.anchorMax = new Vector2(0.5f, 1f);
        railRect.pivot = new Vector2(0.5f, 1f);
        railRect.anchoredPosition = _railAnchoredPosition;

        // The label row is reserved whether or not any label is ever shown, so the slots sit at a
        // fixed height for the whole level.
        float labelRow = _latinWordLabelRowHeight + _latinWordLabelGap;
        float slotRowTop = -labelRow;

        float totalWidth = 0f;
        for (int wordIndex = 0; wordIndex < words.Count; wordIndex++)
        {
            int slotCount = CountEmittedSlots(words[wordIndex]);
            if (slotCount == 0)
                continue;

            if (totalWidth > 0f)
                totalWidth += _wordGap;
            totalWidth += WordWidth(slotCount);
        }

        railRect.sizeDelta = new Vector2(totalWidth, labelRow + _slotSize.y);

        float x = 0f;
        for (int wordIndex = 0; wordIndex < words.Count; wordIndex++)
        {
            FocusWordDefinition word = words[wordIndex];
            int slotCount = CountEmittedSlots(word);
            if (slotCount == 0)
                continue;

            if (x > 0f)
                x += _wordGap;

            float wordWidth = WordWidth(slotCount);
            _railWords.Add(new RailWord
            {
                Word = word,
                LatinLabel = BuildWordLatinLabel(
                    railRect, fontTemplate, wordIndex, x, wordWidth),
            });

            // Mirrors TargetTextSlotMap.Build's flattening exactly — every reference with a symbol,
            // in authored order — because the draw-feedback report's SlotIndex is produced by that
            // type, and this list has to be index-aligned with it or a badge flies to the wrong
            // slot. A reference with no symbol is skipped by both and occupies no slot here.
            int emitted = 0;
            for (int slotIndex = 0;
                 word.decomposition != null && slotIndex < word.decomposition.Count;
                 slotIndex++)
            {
                SymbolValueReference reference = word.decomposition[slotIndex];
                if (reference?.symbol == null)
                    continue;

                float slotX = x + (emitted * (_slotSize.x + _slotSpacing));
                RailSlot built = BuildSlot(
                    railRect, word, reference, slotIndex, _railSlots.Count, slotX, slotRowTop);
                _railSlots.Add(built);
                _railSlotAnchors.Add(built.Anchor);
                emitted++;
            }

            x += wordWidth;
        }

        _railRoot.SetActive(false);
        RepaintRail(forceRestored: false);
    }

    private float WordWidth(int slotCount) =>
        (slotCount * _slotSize.x) + ((slotCount - 1) * _slotSpacing);

    /// <summary>Slots this word contributes to the rail, under TargetTextSlotMap's skip rule.</summary>
    private static int CountEmittedSlots(FocusWordDefinition word)
    {
        if (word?.decomposition == null)
            return 0;

        int count = 0;
        for (int i = 0; i < word.decomposition.Count; i++)
        {
            if (word.decomposition[i]?.symbol != null)
                count++;
        }

        return count;
    }

    private TextMeshProUGUI BuildWordLatinLabel(
        RectTransform railRect,
        TextMeshProUGUI fontTemplate,
        int wordIndex,
        float x,
        float wordWidth)
    {
        var labelObject = new GameObject(
            $"[Runtime] RestorationWordLabel_{wordIndex}", typeof(RectTransform));
        labelObject.transform.SetParent(railRect, false);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        CopyFont(fontTemplate, label);
        label.fontSize = _latinWordLabelFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = _latinWordLabelColor;
        label.raycastTarget = false;
        label.text = string.Empty;

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(wordWidth, _latinWordLabelRowHeight);

        return label;
    }

    private RailSlot BuildSlot(
        RectTransform railRect,
        FocusWordDefinition word,
        SymbolValueReference reference,
        int decompositionIndex,
        int flattenedIndex,
        float x,
        float y)
    {
        var slotObject = new GameObject(
            $"[Runtime] RestorationSlot_{flattenedIndex}", typeof(RectTransform), typeof(Image));
        slotObject.transform.SetParent(railRect, false);

        Image frame = slotObject.GetComponent<Image>();
        frame.sprite = _runtimeSlotFrameSprite;
        // Sliced so the generated frame's border stays one thickness at any authored slot size; a
        // Simple fill would scale the border with the slot and thicken it on a larger rail.
        frame.type = Image.Type.Sliced;
        frame.raycastTarget = false;

        RectTransform rect = slotObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = _slotSize;

        var glyphObject = new GameObject(
            $"[Runtime] RestorationSlotGlyph_{flattenedIndex}", typeof(RectTransform), typeof(Image));
        glyphObject.transform.SetParent(slotObject.transform, false);

        Image glyph = glyphObject.GetComponent<Image>();
        glyph.sprite = ResolveSlotGlyph(reference.symbol);
        glyph.color = _filledGlyphColor;
        glyph.preserveAspect = true;
        glyph.raycastTarget = false;
        SetStretch(
            glyphObject.GetComponent<RectTransform>(),
            new Vector2(_slotGlyphInset, _slotGlyphInset),
            new Vector2(-_slotGlyphInset, -_slotGlyphInset));
        glyphObject.SetActive(false);

        return new RailSlot
        {
            Word = word,
            DecompositionIndex = decompositionIndex,
            Anchor = rect,
            Frame = frame,
            Glyph = glyph,
        };
    }

    /// <summary>
    /// The art a filled slot shows. The bare outline first and the framed badge second — never
    /// <c>displaySprite</c>, which is a learning card carrying the romanised syllable printed on
    /// it. A rail built out of learning cards would print the Latin reading in picture form and
    /// defeat the ash exactly as the old text readout did.
    /// </summary>
    private static Sprite ResolveSlotGlyph(BaybayinCharacterSO symbol)
    {
        if (symbol == null)
            return null;

        return symbol.glyphOutlineSprite != null ? symbol.glyphOutlineSprite : symbol.badgeSprite;
    }

    private void UpdateRestorationProgress()
    {
        bool shouldShow = IsClueCombatArmed
            && _level != null
            && _level.activeClueRestorationEnabled
            && HasRestorationWords;

        if (!shouldShow)
        {
            if (_railRoot != null)
                _railRoot.SetActive(false);

            // Also covers an authored readout on a level that never builds a rail: whatever string
            // it was left holding is not this level's progress, and it named its words in Latin.
            if (_restorationProgressText != null)
                _restorationProgressText.gameObject.SetActive(false);
            return;
        }

        EnsureRestorationRail();
        if (_railRoot == null)
            return;

        RepaintRail(forceRestored: false);
        _railRoot.SetActive(true);
    }

    /// <summary>
    /// Paints every slot from restoration state: a restored slot shows its Baybayin glyph in the
    /// filled frame colour, an unrestored one stays a bare empty frame. The frame carries the state
    /// as well as the glyph, so a symbol with no glyph art still reads as filled rather than as
    /// silently unfinished.
    /// </summary>
    /// <param name="forceRestored">
    /// Paints every slot restored regardless of state, for the completion beat. It changes nothing
    /// about restoration state itself — the win was already decided on that state — so a caller
    /// cannot use this to fake progress the player has not made.
    /// </param>
    private void RepaintRail(bool forceRestored)
    {
        for (int i = 0; i < _railSlots.Count; i++)
        {
            RailSlot slot = _railSlots[i];
            bool restored = forceRestored
                || _restorationState.IsSlotRestored(slot.Word, slot.DecompositionIndex);

            slot.Frame.color = restored ? _filledSlotColor : _emptySlotColor;

            // A symbol with no glyph art leaves the child off rather than showing it: an Image with
            // no sprite draws a solid quad, which would fill the slot with a block instead of a
            // glyph. The frame colour still reports the slot as restored.
            bool showGlyph = restored && slot.Glyph.sprite != null;
            if (slot.Glyph.gameObject.activeSelf != showGlyph)
                slot.Glyph.gameObject.SetActive(showGlyph);
        }

        for (int i = 0; i < _railWords.Count; i++)
        {
            RailWord word = _railWords[i];
            bool show = ShouldShowLatinLabel(word.Word, forceRestored);
            word.LatinLabel.text = show ? ResolveWordLatinLabel(word.Word) : string.Empty;
        }

        // Left alone while the completion flash owns the alpha, so a repaint landing mid-beat
        // cannot snap the rail back to full opacity halfway through a dip.
        if (_railCanvasGroup != null && _railFlashRoutine == null)
            _railCanvasGroup.alpha = 1f;
    }

    /// <summary>
    /// Whether this word's Latin spelling may appear beside its slots.
    ///
    /// <para>
    /// A complete word is always readable: every slot of it is filled, so the spelling answers
    /// nothing the player has not already drawn, and §2 B10 wants the finished text whole. An
    /// incomplete word needs the serialized opt-in, and even then is refused on a level whose
    /// roster can mask the clue — see the field's comment. The alternative, letting the flag
    /// silently win everywhere, is how the leak happened in the first place: the presenter has no
    /// way to know an author flipped it for a later level and forgot Level 1 shares this HUD.
    /// </para>
    /// </summary>
    private bool ShouldShowLatinLabel(FocusWordDefinition word, bool forceRestored)
    {
        if (word == null)
            return false;

        if (forceRestored || _restorationState.IsWordComplete(word.stableId))
            return true;

        return _showLatinWordLabels && !LevelMasksTheClue();
    }

    private static string ResolveWordLatinLabel(FocusWordDefinition word)
    {
        if (word == null)
            return string.Empty;

        return !string.IsNullOrEmpty(word.displayLabel) ? word.displayLabel : word.latinSpelling;
    }

    /// <summary>
    /// True when any enemy this level can spawn masks the clue's readable spelling — today that is
    /// Abo ng Simula's <c>ashesFirstSlot</c>. Both the level roster and every wave's own list are
    /// walked: a wave may name an enemy type the master roster has since been trimmed of, and a
    /// single missed carrier is enough to hand the masked reading back.
    /// </summary>
    private bool LevelMasksTheClue()
    {
        if (_levelMasksTheClue.HasValue)
            return _levelMasksTheClue.Value;

        bool masks = false;
        if (_level != null)
        {
            masks = RosterMasksTheClue(_level.allowedEnemyTypes);

            for (int i = 0; !masks && _level.waves != null && i < _level.waves.Count; i++)
                masks = RosterMasksTheClue(_level.waves[i]?.enemyTypes);
        }

        _levelMasksTheClue = masks;
        return masks;
    }

    private static bool RosterMasksTheClue(IReadOnlyList<EnemyDataSO> roster)
    {
        if (roster == null)
            return false;

        for (int i = 0; i < roster.Count; i++)
        {
            if (roster[i] != null && roster[i].ashesFirstSlot)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Flashes the whole rail as one object. Deliberately the rail's group alpha rather than a
    /// per-slot animation: the beat's content is that four slots have become one restored text, so
    /// they have to move as one thing.
    /// </summary>
    private IEnumerator FlashRail()
    {
        for (int i = 0; i < _railFlashCount; i++)
        {
            _railCanvasGroup.alpha = _railFlashDipAlpha;
            yield return WaitUnscaled(_railFlashHalfCycleSeconds);
            _railCanvasGroup.alpha = 1f;
            yield return WaitUnscaled(_railFlashHalfCycleSeconds);
        }

        _railCanvasGroup.alpha = 1f;
        _railFlashRoutine = null;
    }

    /// <summary>
    /// Unscaled wait, like the crumble and the word cue: the instant-win beat holds the game at a
    /// dipped time scale, and a scaled flash would stretch past the beat that asked for it.
    /// </summary>
    private static IEnumerator WaitUnscaled(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// A hollow square: an empty slot has to read as a waiting outline, and a null
    /// sprite renders a filled quad, which reads as an already-occupied block. Generated rather
    /// than authored because the project has no slot art, and generated in code rather than taken
    /// from builtin resources so it also renders in a player build — the same reasoning as
    /// <see cref="CreateRingSprite"/>.
    /// </summary>
    private static Sprite CreateSlotFrameSprite(float borderFraction)
    {
        const int size = 64;
        int border = Mathf.Max(1, Mathf.RoundToInt(size * borderFraction));

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
                bool onBorder = x < border || y < border
                    || x >= size - border || y >= size - border;
                pixels[(y * size) + x] = onBorder ? opaque : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        // Sliced with a border matching the drawn frame, so a non-square slot size stretches the
        // frame's edges instead of its corners.
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
    }

    private void DestroyRestorationRail()
    {
        _railFlashRoutine = null;
        _railSlots.Clear();
        _railWords.Clear();
        _railSlotAnchors.Clear();

        Texture2D frameTexture =
            _runtimeSlotFrameSprite != null ? _runtimeSlotFrameSprite.texture : null;

        DestroyOwnedObject(_railRoot);
        DestroyOwnedObject(_runtimeSlotFrameSprite);
        DestroyOwnedObject(frameTexture);

        _railRoot = null;
        _railCanvasGroup = null;
        _runtimeSlotFrameSprite = null;
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
        bool ashActive = AshFirstSlotController.IsAnyActive();
        string finalText = masked
            ? BuildMaskedSpellingWithRestoration(
                word, clue.Character.stableId, ashActive, _restorationState)
            : word.latinSpelling;

        // A crumble in flight is always abandoned rather than blended: whatever raised this call
        // is newer information than the animation is carrying.
        StopClueCrumble();

        if (masked && ashActive && _animateClueCrumble && CanAnimateClueCrumble)
        {
            string readableText = BuildMaskedSpellingWithRestoration(
                word, clue.Character.stableId, false, _restorationState);

            // Equal strings mean the ash bit nothing — the needed slot already was the word's
            // first. Nothing to crumble, and animating would show motion with no consequence.
            if (readableText != finalText)
            {
                _clueCrumbleRoutine = StartCoroutine(CrumbleClueText(readableText, finalText));
                return;
            }
        }

        _clueText.text = finalText;
    }

    /// <summary>
    /// True only where a coroutine can actually run and has time to run in. EditMode and a
    /// disabled presenter fall through to the immediate assignment, which is also what keeps the
    /// masked string byte-identical for the tests that read it back synchronously.
    /// </summary>
    private bool CanAnimateClueCrumble =>
        Application.isPlaying && isActiveAndEnabled && _clueCrumbleDurationSeconds > 0f;

    private void StopClueCrumble()
    {
        if (_clueCrumbleRoutine == null)
            return;

        StopCoroutine(_clueCrumbleRoutine);
        _clueCrumbleRoutine = null;
    }

    /// <summary>
    /// Spots the frame an Abo's ash arms or lifts and refreshes the panel, because neither event
    /// raises a clue change of its own.
    ///
    /// <para>
    /// Only the readable-to-masked direction animates. The lift is the player's reward for
    /// working out the counter and wants to read as instant relief, not as another six-tenths of
    /// a second of motion before they can read their clue again.
    /// </para>
    /// </summary>
    private void WatchAshOnset()
    {
        if (!IsClueCombatArmed || _clueText == null)
            return;

        bool ashActive = AshFirstSlotController.IsAnyActive();
        if (ashActive == _ashWasActive)
            return;

        _ashWasActive = ashActive;
        _animateClueCrumble = ashActive;
        UpdateCluePanel(_currentClue);
        _animateClueCrumble = false;
    }

    /// <summary>
    /// Animates the readable spelling into its masked form: the doomed characters sink and fade,
    /// the mask fades in behind them, and the final frame assigns the masked string exactly as
    /// <see cref="BuildMaskedSpellingWithRestoration"/> produced it — a transition only, so
    /// nothing downstream of the rendered string ever sees an intermediate value.
    ///
    /// <para>
    /// Unscaled time, like the word-restoration cue: the introduction cards run the level at a
    /// fraction of normal time scale, and a crumble stretched across four seconds would read as a
    /// rendering fault rather than as ash.
    /// </para>
    /// </summary>
    private IEnumerator CrumbleClueText(string readableText, string maskedText)
    {
        // The clue is readable right up to the frame the gust lands.
        _clueText.text = readableText;

        float lead = Mathf.Max(0f, _clueCrumbleLeadSeconds);
        float elapsed = 0f;
        while (elapsed < lead)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // The two strings share a head and a tail; only the slot the ash covers differs. Deriving
        // the changed run rather than assuming it starts at index zero keeps this correct for a
        // word whose first slot is itself already restored or masked.
        int prefixLength = CommonPrefixLength(readableText, maskedText);
        int suffixLength = CommonSuffixLength(readableText, maskedText, prefixLength);

        float duration = Mathf.Max(0.01f, _clueCrumbleDurationSeconds);
        elapsed = 0f;
        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);

            if (progress < _clueCrumbleHandoff)
            {
                float local = Mathf.Clamp01(progress / Mathf.Max(0.01f, _clueCrumbleHandoff));
                _clueText.text = BuildCrumbleFrame(
                    readableText, prefixLength, suffixLength, local, fadingOut: true);
            }
            else
            {
                float local = Mathf.Clamp01(
                    (progress - _clueCrumbleHandoff)
                    / Mathf.Max(0.01f, 1f - _clueCrumbleHandoff));
                _clueText.text = BuildCrumbleFrame(
                    maskedText, prefixLength, suffixLength, local, fadingOut: false);
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _clueText.text = maskedText;
        _clueCrumbleRoutine = null;
    }

    /// <summary>
    /// One frame of the crumble: the unchanged head and tail are written plain, and every
    /// character of the changed run carries its own alpha and vertical offset so the run comes
    /// apart character by character rather than as a block.
    /// </summary>
    private string BuildCrumbleFrame(
        string text,
        int prefixLength,
        int suffixLength,
        float progress,
        bool fadingOut)
    {
        int changedStart = prefixLength;
        int changedEnd = text.Length - suffixLength;
        var builder = new System.Text.StringBuilder(text.Length * 6);

        builder.Append(text, 0, changedStart);

        int changedCount = Mathf.Max(1, changedEnd - changedStart);
        for (int i = changedStart; i < changedEnd; i++)
        {
            // Each character starts its own motion a little after the one to its left, so the
            // stagger is a fraction of the whole rather than a per-character duration to retune.
            float delay = _clueCrumbleCharacterStagger * ((i - changedStart) / (float)changedCount);
            float local = Mathf.Clamp01((progress - delay) / Mathf.Max(0.01f, 1f - delay));

            float alpha = fadingOut ? 1f - local : local;
            float offsetEm = fadingOut
                ? -_clueCrumbleDropEm * local
                : _clueCrumbleMaskRiseEm * (1f - local);

            builder.Append("<alpha=#")
                .Append(Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f).ToString("X2"))
                .Append("><voffset=")
                .Append(offsetEm.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
                .Append("em>")
                .Append(text[i])
                .Append("</voffset>");
        }

        // Restore full opacity before the tail: alpha tags persist to the end of the string.
        builder.Append("<alpha=#FF>");
        builder.Append(text, changedEnd, suffixLength);
        return builder.ToString();
    }

    private static int CommonPrefixLength(string first, string second)
    {
        int limit = Mathf.Min(first.Length, second.Length);
        int index = 0;
        while (index < limit && first[index] == second[index])
            index++;

        return index;
    }

    /// <summary>
    /// Matching tail length, never allowed to overlap the shared head — otherwise two strings that
    /// differ only in a repeated character could claim the same characters twice and the frame
    /// would be built from a negative-length run.
    /// </summary>
    private static int CommonSuffixLength(string first, string second, int prefixLength)
    {
        int limit = Mathf.Min(first.Length, second.Length) - prefixLength;
        int index = 0;
        while (index < limit
               && first[first.Length - 1 - index] == second[second.Length - 1 - index])
        {
            index++;
        }

        return index;
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

    /// <summary>
    /// How many authored slots are restored, across every focus word. Counted rather than stored
    /// so it cannot drift from the slot flags themselves, and exposed because abilities gate on
    /// "the player has used the clue at least once" — Abo ng Simula's ash must not arm before the
    /// player has ever read the clue and acted on it.
    /// </summary>
    public int RestoredSlotCount
    {
        get
        {
            int restored = 0;
            for (int wordIndex = 0; wordIndex < _words.Count; wordIndex++)
            {
                WordState state = _words[wordIndex];
                if (state.Word?.decomposition == null)
                    continue;

                for (int slotIndex = 0; slotIndex < state.Word.decomposition.Count; slotIndex++)
                {
                    SymbolValueReference reference = state.Word.decomposition[slotIndex];
                    if (reference?.symbol != null && state.RestoredSlots[slotIndex])
                        restored++;
                }
            }

            return restored;
        }
    }

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
