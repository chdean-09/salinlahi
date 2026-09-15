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
    [Tooltip("Where the rail sits under the HUD container, anchored to the BOTTOM centre. y is "
             + "measured UP from the bottom edge to the rail's own bottom edge, so a larger y "
             + "lifts the rail further off the foot of the screen.\n\n"
             + "The rail used to hang from the top centre at y=-120, above the clue panel. It was "
             + "moved to the bottom band on request: the top of the screen is where the enemies "
             + "walk in and where the clue panel and wave text already sit, and the target text "
             + "the player is filling reads better as the player's own row along the foot of the "
             + "field, under the base and under Juan.\n\n"
             + "y no longer has to dodge the fence. Every earlier value here was an attempt to "
             + "thread the rail into the gap between the fence's foot and the bottom of the screen, "
             + "and there was never enough room: the rail is about 190 units tall and the gap is "
             + "about 145, so the rail sat ON the planks whatever y it was given. The rail now asks "
             + "the play column to reserve a band for it and the play field is raised clear, so y is "
             + "just the breathing room under the labels and wants to be SMALL — a large value here "
             + "no longer lifts the rail off anything, it only makes the reserved band taller and "
             + "eats the play field.\n\n"
             + "The safe area needs no arithmetic here: the rail parents to HUDLayer, a full-rect "
             + "child of HUDRoot, and HUDRoot carries SafeAreaHandler — so y=0 is the bottom of "
             + "the SAFE area and the home indicator's inset is already taken out underneath it.")]
    [SerializeField] private Vector2 _railAnchoredPosition = new Vector2(0f, 24f);

    [Tooltip("Clearance in canvas units left between the top of the rail and the foot of the play "
             + "field, added to the band the rail asks the play column to reserve. Keeps the fence's "
             + "bottom plank and Juan's feet from ending exactly on the rail's top edge, which reads "
             + "as an overlap even when it is not one.")]
    [SerializeField, Min(0f)] private float _railPlayFieldClearance = 28f;

    [Tooltip("Size of one target-text slot in canvas units. Slots are square by authoring "
             + "convention but the two axes are separate so a wide glyph can be given room.")]
    [SerializeField] private Vector2 _slotSize = new Vector2(124f, 124f);

    [Tooltip("Gap between two slots inside the same focus word, in canvas units. Small: slots of "
             + "one word have to read as one text rather than as separate collectables.")]
    [SerializeField, Min(0f)] private float _slotSpacing = 16f;

    [Tooltip("Gap between two focus words' slot groups, in canvas units. Must be clearly wider "
             + "than the slot spacing — the grouping is what tells the player INA AMA is two "
             + "words and not one run of four symbols.")]
    [SerializeField, Min(0f)] private float _wordGap = 80f;

    [Tooltip("Share of the slot's box the glyph's INK should span, leaving the rest as an even "
             + "margin inside the gold frame.\n\n"
             + "Expressed as a share of the box rather than as an inset in canvas units because the "
             + "rail grew and the glyphs did not follow: an inset is absolute, so the same 10 units "
             + "that left a sensible margin on a small box leaves a huge one on a large box.")]
    [SerializeField, Range(0.3f, 1f)] private float _slotGlyphFill = 0.84f;

    [Tooltip("Share of an almanac PNG's square that actually carries ink.\n\n"
             + "The almanac art is authored on a 320x320 canvas with the glyph drawn small and "
             + "centred: the opaque pixels of A, NA and MA span 42-44% of the width. Fitting that "
             + "sprite to the box therefore fills the box with mostly-transparent art and the glyph "
             + "reads at about 40% of its frame, which is the complaint. The glyph rect is scaled up "
             + "by fill/ink so it is the INK, not the PNG's empty margin, that meets the frame. "
             + "Applies only to almanac art; the outline and badge fallbacks are drawn tight and are "
             + "fitted to the box directly.")]
    [SerializeField, Range(0.1f, 1f)] private float _almanacGlyphInkFraction = 0.44f;

    [Tooltip("Frame colour of a slot that is still waiting for its symbol.")]
    [SerializeField] private Color _emptySlotColor = new Color(1f, 1f, 1f, 0.22f);

    [Tooltip("Frame colour of a slot whose symbol has been restored.")]
    [SerializeField] private Color _filledSlotColor = new Color(1f, 0.84f, 0.29f, 0.85f);

    [Tooltip("Tint applied to the restored slot's glyph. WHITE, i.e. no tint, and that is the "
             + "point.\n\n"
             + "The slot used to show glyphOutlineSprite, which is a flat white silhouette that "
             + "only looked like a glyph because this field tinted it dark gold. The slot now "
             + "shows almanacSprite instead: the same bare glyph the almanac prints, already "
             + "self-coloured as a near-white fill inside a dark brown outline. Multiplying gold "
             + "over that would muddy the fill and flatten the outline that makes it legible, so "
             + "an Image tint here must stay white. The gold stays on the slot FRAME "
             + "(_filledSlotColor), which is where it was doing useful work.")]
    [SerializeField] private Color _filledGlyphColor = Color.white;

    // The navy backing plates that used to be declared here — one inside every restored slot, one
    // continuous strip behind the whole label row — are gone, and deliberately not replaced.
    //
    // They were both answers to the same accident: the rail was drawn ON TOP of the wooden fence,
    // because it is taller than the gap between the fence's foot and the bottom of the screen. A
    // near-white glyph and gold text on brown planks needed a plate to survive. The rail now has a
    // reserved band of its own beneath the play field (AspectLockedCamera.SetBottomBandPixels), so
    // it is read against the flat dark ground below the fence and there is nothing left to plate
    // against. A plate here now would be a dark rectangle in the middle of a dark band.

    [Tooltip("Thickness of a slot frame's border as a fraction of the slot, used to generate the "
             + "hollow frame sprite. The frame is generated rather than authored because there is "
             + "no slot art in the project and an empty slot must not read as a filled block.")]
    [SerializeField, Range(0.02f, 0.4f)] private float _slotFrameBorderFraction = 0.09f;

    [Header("Rail Latin Labels")]
    // These are now PER-SLOT labels printed UNDER each box, and they are always shown. That is a
    // deliberate reversal, requested directly, of the policy that used to live here: one Latin label
    // per WORD, beside the slots, default OFF, refused outright on any level whose roster can mask
    // the clue — because a rail printing "INA" next to an ash-masked clue handed back the exact
    // reading Abo ng Simula's ability had just taken away.
    //
    // The reversal is narrower than it looks but it is not free, and it is worth stating plainly:
    // naming every syllable of the target text under its own box for the whole level does reduce
    // what the ash can still hide to the ORDER of the syllables rather than their identity. It was
    // asked for on the same pass that removed the first-draw trace guide from the field, which
    // leaves these labels as the only standing cue for which box wants which symbol.

    [Tooltip("Font size of a slot's romanised label on the rail.")]
    [SerializeField, Min(1f)] private float _latinWordLabelFontSize = 46f;

    [Tooltip("Colour of a slot's romanised label on the rail.")]
    [SerializeField] private Color _latinWordLabelColor = new Color(1f, 0.84f, 0.29f, 1f);

    [Tooltip("Height reserved BELOW the slots for the romanised labels, in canvas units. The row is "
             + "reserved whether or not a label fills it, so the slots sit at a fixed height for "
             + "the whole level: DrawFeedbackPresenter flies a badge to a slot rect that must not "
             + "travel while the badge is in the air.")]
    [SerializeField, Min(0f)] private float _latinWordLabelRowHeight = 56f;

    [Tooltip("Gap between the slots and the romanised label row beneath them, in canvas units.")]
    [SerializeField, Min(0f)] private float _latinWordLabelGap = 10f;

    [Tooltip("Font size of the divider drawn between two focus words' slot groups. Larger than the "
             + "slot labels: it is a piece of punctuation between groups, not a reading.")]
    [SerializeField, Min(1f)] private float _wordSeparatorFontSize = 64f;

    [Tooltip("Colour of the divider between two focus words' slot groups. Dimmer than the labels "
             + "on purpose — it separates the groups without competing with them for attention.")]
    [SerializeField] private Color _wordSeparatorColor = new Color(1f, 0.84f, 0.29f, 0.55f);

    [Header("Rail Slot Fill Pop")]
    [Tooltip("Peak scale of the brief pop played on the ONE slot that just filled. This is the beat "
             + "that teaches the lesson's whole point — the enemy fell and THAT box became a "
             + "letter — so the box has to visibly do something at the moment it fills rather than "
             + "just quietly change colour. 1 disables the pop.")]
    [SerializeField, Min(1f)] private float _slotFillPopScale = 1.28f;

    [Tooltip("Seconds for the fill pop's full out-and-back. Short on purpose: a flourish here "
             + "competes with the restoration line being read.")]
    [SerializeField, Min(0f)] private float _slotFillPopSeconds = 0.42f;

    [Header("Rail Slot Glyph Flight")]
    // The claim this beat has to make is spatial: you took THAT syllable off THAT enemy and put it
    // in THAT box. A fade-in says "a box changed"; a glyph that leaves the enemy, arcs down and
    // lands in the box says where it came from. Everything below runs on UNSCALED time — beat 1 of
    // the enemy introduction holds Time.timeScale at 0.15 and a kill can land mid-lesson, which
    // would stretch a 0.7s flourish into most of five seconds.

    [Tooltip("Whether a filled slot's glyph flies in from the defeated enemy. Off falls back to "
             + "the immediate fill, which is the same end state.")]
    [SerializeField] private bool _slotGlyphFlightEnabled = true;

    [Tooltip("Seconds the glyph spends travelling from the enemy to its box, on an ease-out arc.")]
    [SerializeField, Min(0.01f)] private float _slotGlyphFlightSeconds = 0.40f;

    [Tooltip("Scale the glyph leaves the enemy at, relative to its resting size in the box. "
             + "Larger at the source so it reads as coming toward the player's readout.")]
    [SerializeField, Min(0.1f)] private float _slotGlyphFlightStartScale = 1.4f;

    [Tooltip("Scale the glyph overshoots to on arrival before settling back to 1.")]
    [SerializeField, Min(1f)] private float _slotGlyphArrivalScale = 1.25f;

    [Tooltip("Seconds for the back-eased settle from the arrival overshoot down to resting size.")]
    [SerializeField, Min(0.01f)] private float _slotGlyphSettleSeconds = 0.18f;

    [Tooltip("Seconds for the small trailing bounce after the settle. The whole flight is meant "
             + "to be over inside about 0.70s so it never outlives the line explaining it.")]
    [SerializeField, Min(0f)] private float _slotGlyphBounceSeconds = 0.12f;

    [Tooltip("Height of the trailing bounce as a fraction of resting size.")]
    [SerializeField, Range(0f, 0.25f)] private float _slotGlyphBounceAmount = 0.03f;

    [Tooltip("How far the travel arc bows upward, as a fraction of the straight-line distance. "
             + "A straight slide reads as a UI tween; a bow reads as something thrown.")]
    [SerializeField, Range(0f, 1f)] private float _slotGlyphArcHeightFraction = 0.28f;

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
    /// <summary>
    /// The mark drawn between two focus words' slot groups. A colon rather than a slash or a bullet:
    /// it is the divider the requested shape asks for, it is present in every font the HUD can fall
    /// back to, and it carries no reading of its own that could be mistaken for a syllable.
    /// </summary>
    private const string WordSeparatorText = ":";

    private sealed class RailSlot
    {
        public FocusWordDefinition Word;
        public int DecompositionIndex;
        public RectTransform Anchor;
        public Image Frame;
        public Image Glyph;

        /// <summary>The romanised syllable printed under the box, and the label printing it.</summary>
        public TextMeshProUGUI Label;
        public string LatinLabel;

        /// <summary>
        /// The in-flight glyph currently heading for this box, if any. Held per slot rather than in
        /// one global list so a second fill on the SAME box can retire the first rather than let
        /// two fliers race each other into the same place. Fills on DIFFERENT boxes are genuinely
        /// independent and run side by side.
        /// </summary>
        public GameObject Flier;
        public Coroutine FlightRoutine;

        /// <summary>
        /// Unscaled time by which this flight must have finished on its own. Past it, the watchdog
        /// finishes it regardless.
        ///
        /// <para>
        /// This is what covers the kills the flier itself cannot see: <c>StopAllCoroutines</c>, or
        /// anything else that drops the routine while the presenter, the rail and the flier all
        /// stay perfectly alive. In that case there is nothing structurally wrong for the watchdog
        /// to notice — only a glyph that stopped moving — so the deadline is the only signal left.
        /// </para>
        /// </summary>
        public float FlightDeadline;
    }

    /// <summary>
    /// Every slot with a flight in progress. Walked by <see cref="ReconcileSlotFlights"/> once a
    /// frame as a watchdog, and drained outright on teardown.
    ///
    /// <para>
    /// This list, and not a <c>finally</c> in the flight coroutine, is what guarantees the rail
    /// cannot be left half-animated. Unity does NOT run a coroutine's <c>finally</c> when it stops
    /// the coroutine — <c>StopAllCoroutines</c>, disabling the component, destroying the object and
    /// unloading the scene all kill the routine dead where it stands. A flight that parked the real
    /// glyph hidden and relied on its own tail to unhide it would strand an invisible glyph in a
    /// slot the state says is restored. So the rest state is instead re-established from outside
    /// the coroutine, by <see cref="FinishSlotFlight"/>, which is idempotent and is called from the
    /// watchdog, from <see cref="OnDisable"/> and from <see cref="DestroyRestorationRail"/>.
    /// </para>
    /// </summary>
    private readonly List<RailSlot> _slotsInFlight = new List<RailSlot>();

    private readonly List<RailSlot> _railSlots = new List<RailSlot>();

    /// <summary>
    /// The rail's slot rects in flattened reading order, handed out through
    /// <see cref="RestorationSlotAnchors"/>. Held as its own list rather than projected on demand
    /// so a caller polling it every frame allocates nothing.
    /// </summary>
    private readonly List<RectTransform> _railSlotAnchors = new List<RectTransform>();

    private GameObject _railRoot;
    private System.Action _bandRefreshHandler;
    private AspectLockedCamera _bandRefreshColumn;
    private CanvasGroup _railCanvasGroup;
    private Sprite _runtimeSlotFrameSprite;
    private Coroutine _railFlashRoutine;

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
    /// The clue panel's own rect, or null on a HUD with no panel wired.
    ///
    /// <para>
    /// Exposed for code that has to know which band of the screen the HUD covers — the enemy
    /// introduction beat halts its subject BELOW this rect, because the panel is drawn in front of
    /// the lane and an enemy parked behind it is on camera and still invisible.
    /// </para>
    /// </summary>
    public RectTransform CluePanelRect =>
        _cluePanelRoot != null ? _cluePanelRoot.transform as RectTransform : null;

    /// <summary>The restoration rail's own rect, or null before the rail is built.</summary>
    public RectTransform RestorationRailRect =>
        _railRoot != null ? _railRoot.transform as RectTransform : null;

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

        // Before anything is torn down. Disabling the component is one of the ways Unity kills a
        // coroutine without running its tail, so the slots are put back at rest here, by hand,
        // while the rail still exists to be put back.
        FinishAllSlotFlights();

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

        // A new level means a new target text, so the rail is rebuilt from the incoming focus
        // words rather than repainted over the previous level's slots.
        DestroyRestorationRail();

        _resolvedChannels = level == null
            ? ClueChannels.Glyph
            : ClueChannelResolver.Resolve(level.clueChannels, level.audioVisualFallback);

        // An Inspector-wired presenter runs OnEnable and Start before LevelFlowController
        // creates the director, so both earlier attempts found Instance null. Without this
        // the authored HUD path would silently never present a clue.
        SubscribeToDirector();

        // No runtime clue panel is built any more. The fallback used to drop a dark plate across
        // the top of the play field carrying a masked "----" readout and a Replay button, on every
        // level that armed clue combat without an authored panel — which is every level. The
        // restoration rail along the foot of the screen already shows the target text as slots,
        // and the authored DrawGlowingSymbolInstruction already carries the standing instruction,
        // so the plate was a second, unplaced copy of both, drawn in front of the lane the enemies
        // walk down. An authored panel wired in the Inspector still works exactly as before.

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

    private static void CopyFont(TextMeshProUGUI source, TextMeshProUGUI target)
    {
        if (source == null || target == null || source.font == null)
            return;

        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
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
        ReconcileSlotFlights();

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

        // Fired between the repaint and the cue, so the box has already become a letter by the time
        // it pops and the pop lands on the same frame as the line that explains it.
        PopSlotsForSymbol(clue.Character.stableId);

        // And AFTER the repaint for the same reason plus one more: the repaint is what leaves every
        // slot in its finished state, so if the flight below never starts, never finishes, or is
        // killed halfway, the rail is already correct. The flight only borrows the glyph.
        LaunchSlotGlyphFlights(clue.Character.stableId, clue.transform.position);

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

        // Beat 9 exists to teach ONE thing — killing the enemy fills the word — and the cue
        // announcing it was printed straight across the slot rail that shows it happening, with
        // "INA" sitting inside an empty slot box. The announcement moves; the rail does not, because
        // the rail is the thing being taught.
        MoveWordRestoredCueClearOfRail();

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
    /// Canvas units of clear air left between the restoration rail's bottom edge and the top of the
    /// word-restoration cue.
    /// </summary>
    private const float WordRestoredCueRailGap = 34f;

    /// <summary>
    /// Slides the word-restoration cue clear of the restoration rail, and leaves it wherever it
    /// already was if it is already clear.
    ///
    /// <para>
    /// <b>Measured from the two live rects, not from authored constants.</b> The rail hangs off the
    /// HUD container, which <c>SafeAreaHandler</c> insets at runtime, while the cue hangs off the
    /// canvas, which is not inset — so the gap the two authored anchored positions imply is not the
    /// gap on screen, and on the device the check ran the two landed in the same band. Comparing
    /// world corners and converting the correction back through the cue's own parent is the only
    /// form of this that is right on both.
    /// </para>
    ///
    /// <para>
    /// <b>The direction is chosen from where the rail actually is, and that is not cosmetic.</b>
    /// This used to move the cue DOWN unconditionally, which was right while the rail hung from the
    /// top of the HUD. The rail now sits in the bottom band, and "below the rail" there is off the
    /// bottom of the screen — so an unconditional push down would silently throw the cue away, and
    /// the cue is the line that names what the player just restored. The rail's own centre against
    /// the canvas centre decides: a rail in the lower half pushes the cue UP above its top edge, a
    /// rail in the upper half pushes it DOWN below its bottom edge as before. Either way the cue
    /// ends up on the screen-centre side of the rail, which is the side with room on it.
    /// </para>
    /// </summary>
    private void MoveWordRestoredCueClearOfRail()
    {
        if (_wordRestoredText == null || _railRoot == null)
            return;

        if (_wordRestoredText.rectTransform.parent is not RectTransform cueParent)
            return;

        if (_railRoot.transform is not RectTransform railRect)
            return;

        RectTransform cueRect = _wordRestoredText.rectTransform;

        var railCorners = new Vector3[4];
        railRect.GetWorldCorners(railCorners);
        float railWorldBottom = Mathf.Min(
            Mathf.Min(railCorners[0].y, railCorners[1].y),
            Mathf.Min(railCorners[2].y, railCorners[3].y));
        float railWorldTop = Mathf.Max(
            Mathf.Max(railCorners[0].y, railCorners[1].y),
            Mathf.Max(railCorners[2].y, railCorners[3].y));

        var cueCorners = new Vector3[4];
        cueRect.GetWorldCorners(cueCorners);
        float cueWorldBottom = Mathf.Min(
            Mathf.Min(cueCorners[0].y, cueCorners[1].y),
            Mathf.Min(cueCorners[2].y, cueCorners[3].y));
        float cueWorldTop = Mathf.Max(
            Mathf.Max(cueCorners[0].y, cueCorners[1].y),
            Mathf.Max(cueCorners[2].y, cueCorners[3].y));

        float parentScale = Mathf.Abs(cueParent.lossyScale.y);
        if (parentScale <= Mathf.Epsilon)
            return;

        float gapWorld = WordRestoredCueRailGap * parentScale;
        float correctionLocal;

        if (RailSitsInLowerHalf(railRect, railWorldBottom, railWorldTop))
        {
            // Rail along the bottom: the cue goes ABOVE it.
            float desiredWorldBottom = railWorldTop + gapWorld;
            if (cueWorldBottom >= desiredWorldBottom)
                return;

            correctionLocal = (desiredWorldBottom - cueWorldBottom) / parentScale;
        }
        else
        {
            // Rail along the top: the cue goes BELOW it, as it always did.
            float desiredWorldTop = railWorldBottom - gapWorld;
            if (cueWorldTop <= desiredWorldTop)
                return;

            correctionLocal = (desiredWorldTop - cueWorldTop) / parentScale;
        }

        cueRect.anchoredPosition += new Vector2(0f, correctionLocal);
    }

    /// <summary>
    /// Whether the rail is sitting in the bottom half of the canvas it is drawn on, which decides
    /// which side of it has room for the word-restoration cue. Falls back to the rail's anchor when
    /// no canvas rect can be read, so a presenter built without a canvas in a test still answers.
    /// </summary>
    private static bool RailSitsInLowerHalf(
        RectTransform railRect, float railWorldBottom, float railWorldTop)
    {
        Canvas canvas = railRect.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.transform is RectTransform canvasRect)
        {
            var canvasCorners = new Vector3[4];
            canvasRect.GetWorldCorners(canvasCorners);
            float canvasBottom = Mathf.Min(
                Mathf.Min(canvasCorners[0].y, canvasCorners[1].y),
                Mathf.Min(canvasCorners[2].y, canvasCorners[3].y));
            float canvasTop = Mathf.Max(
                Mathf.Max(canvasCorners[0].y, canvasCorners[1].y),
                Mathf.Max(canvasCorners[2].y, canvasCorners[3].y));

            if (canvasTop > canvasBottom)
            {
                float railCentre = (railWorldBottom + railWorldTop) * 0.5f;
                return railCentre < (canvasBottom + canvasTop) * 0.5f;
            }
        }

        return railRect.anchorMin.y < 0.5f;
    }

    /// <summary>
    /// Shows or hides the standing instruction line — Gameplay authors it as
    /// <c>DrawGlowingSymbolInstruction</c>. Matched on "Instruction" in the name rather than on a
    /// serialized reference, so this works on a HUD authored before the cue existed and needs
    /// nobody to rewire a scene. A HUD with no such label is simply left alone.
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

        // Authored first: on a HUD that stands its own instruction line — every HUD, now that no
        // panel is built at runtime — that line is the one the player sees.
        _clueInstructionText = ResolveAuthoredClueInstruction();
        if (_clueInstructionText != null)
            return _clueInstructionText;

        // An Inspector-wired panel may still carry its own instruction child.
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

    /// <summary>
    /// Finds the instruction line the SCENE authored — Gameplay's <c>DrawGlowingSymbolInstruction</c>
    /// — looking under this presenter first, where the authored line sits as a sibling of the
    /// restoration readout, and then across the HUD canvas for a HUD that parents it elsewhere.
    /// Matched on "Instruction" in the name, the same loose match the panel scan uses, because no
    /// serialized reference for this line exists on a HUD authored before the cue did.
    /// <para>
    /// Labels this presenter builds itself are skipped by their <c>[Runtime]</c> prefix, so this
    /// only ever answers with something a human placed.
    /// </para>
    /// </summary>
    private TextMeshProUGUI ResolveAuthoredClueInstruction()
    {
        TextMeshProUGUI found =
            FindAuthoredInstruction(GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true));
        if (found != null)
            return found;

        Canvas canvas = ResolveHudCanvas();
        return canvas == null
            ? null
            : FindAuthoredInstruction(
                canvas.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true));
    }

    private TextMeshProUGUI FindAuthoredInstruction(TextMeshProUGUI[] candidates)
    {
        if (candidates == null)
            return null;

        for (int i = 0; i < candidates.Length; i++)
        {
            TextMeshProUGUI candidate = candidates[i];
            if (candidate == null
                || candidate == _clueText
                || candidate == _wordRestoredText
                || candidate == _restorationProgressText)
                continue;

            if (candidate.name.StartsWith("[Runtime]", System.StringComparison.Ordinal))
                continue;

            if (candidate.name.IndexOf("Instruction", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            return candidate;
        }

        return null;
    }

    private TextMeshProUGUI _clueInstructionText;

    /// <summary>
    /// Built on demand so the cue also reaches an authored HUD that
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
        label.fontSize = UITextScale.Body;
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

    /// <summary>The canvas every runtime-built HUD piece here hangs from, shared so they agree.</summary>
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

        // Bottom-centre. The rail used to hang from the top of the HUD; it now sits in the band
        // under the player's base and Juan, so the top of the screen is the enemies' and the bottom
        // band is the player's own readout.
        //
        // The safe area is already handled by where this is parented, not by arithmetic here:
        // ResolveHudContainer returns HUDLayer, which is a full-rect child of HUDRoot, and HUDRoot
        // carries SafeAreaHandler. So y=0 for this rect is the bottom of the SAFE area, not the
        // bottom of the glass, and the home indicator's inset has already been taken out before
        // _railAnchoredPosition is applied on top of it.
        RectTransform railRect = _railRoot.GetComponent<RectTransform>();
        railRect.anchorMin = new Vector2(0.5f, 0f);
        railRect.anchorMax = new Vector2(0.5f, 0f);
        railRect.pivot = new Vector2(0.5f, 0f);
        railRect.anchoredPosition = _railAnchoredPosition;

        // Slots occupy the TOP of the rail rect and their labels the row beneath, so the rail's own
        // bottom edge is the bottom of the label row. Children are laid out from the rect's top-left
        // as before; only the rect itself moved.
        float labelRow = _latinWordLabelRowHeight + _latinWordLabelGap;
        const float slotRowTop = 0f;

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
        bool anyWordPlaced = false;
        for (int wordIndex = 0; wordIndex < words.Count; wordIndex++)
        {
            FocusWordDefinition word = words[wordIndex];
            int slotCount = CountEmittedSlots(word);
            if (slotCount == 0)
                continue;

            // The divider goes in the gap that was already being opened between two word groups, so
            // it costs no width and cannot push the rail wider than the collision check measured.
            // Keyed off a word actually having been placed rather than off x > 0, because a first
            // word placed at x = 0 is indistinguishable from no word at all by position alone.
            if (anyWordPlaced)
            {
                BuildWordSeparator(railRect, fontTemplate, wordIndex, x, slotRowTop);
                x += _wordGap;
            }

            anyWordPlaced = true;
            float wordWidth = WordWidth(slotCount);

            // Mirrors TargetTextSlotMap.Build's flattening exactly — every reference with a symbol,
            // in authored order — because the draw-feedback report's SlotIndex is produced by that
            // type, and this list has to be index-aligned with it or a badge flies to the wrong
            // slot. A reference with no symbol is skipped by both and occupies no slot here.
            //
            // The per-slot label is built from this same walk, so a label belongs to exactly one box
            // and the grouping falls out of the authored decomposition rather than out of a
            // hardcoded 2+2. Level 1 is INA + AMA; a level whose words are 3+1 or 1+1+2 groups
            // itself correctly here with no change.
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
                built.Label = BuildSlotLabel(
                    railRect, fontTemplate, _railSlots.Count, slotX, slotRowTop);
                built.LatinLabel = ResolveSlotLatinLabel(reference.symbol);
                _railSlots.Add(built);
                _railSlotAnchors.Add(built.Anchor);
                emitted++;
            }

            x += wordWidth;
        }

        ReservePlayFieldBandForRail(railRect);

        // The band is measured in screen pixels, so it goes stale the moment the screen changes
        // size — a rotation, or a Game view resized while playing. Re-measured from the rail's own
        // corners each time the play area recomputes. This settles in one extra pass: the second
        // request carries the same number and SetBottomBandPixels returns without recomputing.
        _bandRefreshHandler ??= () =>
        {
            if (_railRoot != null && _railRoot.transform is RectTransform live)
                ReservePlayFieldBandForRail(live);
        };
        if (AspectLockedCamera.Instance != null)
        {
            AspectLockedCamera.Instance.OnPlayAreaChanged -= _bandRefreshHandler;
            AspectLockedCamera.Instance.OnPlayAreaChanged += _bandRefreshHandler;
            _bandRefreshColumn = AspectLockedCamera.Instance;
        }

        _railRoot.SetActive(false);
        RepaintRail(forceRestored: false);
    }

    /// <summary>
    /// Asks the play column to reserve the screen the rail occupies, so the play field — the fence,
    /// the shrine and Juan, who stands below both — is raised clear of it.
    ///
    /// <para>
    /// This is the fix for the rail being drawn ON the fence. The rail is about 190 canvas units
    /// tall and the gap between the fence's foot and the bottom of the screen is about 145, so no
    /// anchoring could have fitted it: the room has to be made, not found. Made by lowering the
    /// camera, which moves nothing in the world — the shrine's hit line and the enemy path are
    /// authored world positions and are untouched.
    /// </para>
    ///
    /// <para>
    /// Measured from the rail's own live corners rather than computed from the layout fields, so a
    /// taller rail reserves a taller band on its own and the two can never drift apart. Screen
    /// pixels, not canvas units, because the band is compared against the screen: the canvas
    /// scaler's own factor and the safe-area inset the rail is parented inside are both already
    /// baked into where those corners land.
    /// </para>
    /// </summary>
    private void ReservePlayFieldBandForRail(RectTransform railRect)
    {
        AspectLockedCamera playColumn = AspectLockedCamera.Instance;
        if (playColumn == null || railRect == null)
            return;

        Canvas canvas = railRect.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        Camera uiCamera = canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector3[] corners = new Vector3[4];
        railRect.GetWorldCorners(corners);

        float topScreenY = float.NegativeInfinity;
        for (int i = 0; i < corners.Length; i++)
        {
            float screenY = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]).y;
            if (screenY > topScreenY)
                topScreenY = screenY;
        }

        if (float.IsInfinity(topScreenY) || float.IsNaN(topScreenY))
            return;

        playColumn.SetBottomBandPixels(
            topScreenY + (_railPlayFieldClearance * Mathf.Max(0.01f, canvas.scaleFactor)));
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

    /// <summary>
    /// The romanised syllable printed directly beneath one slot's box, centred on it.
    ///
    /// <para>
    /// One label per BOX rather than one per word: the player is being asked which symbol goes in
    /// which box, and a word spelled out beside the group leaves them to divide it up themselves.
    /// The label row sits below the slot row so the box and its reading are adjacent, and the rail
    /// reads as [box][box] : [box][box] with the syllables underneath.
    /// </para>
    ///
    /// <para>
    /// The text is the symbol's own <c>syllable</c> — the same romanisation the rest of the HUD
    /// names a glyph by — uppercased, so the row reads as labels rather than as prose. It is
    /// withheld behind the same mask as the box's glyph until that slot is restored; see
    /// <see cref="RepaintRail"/>.
    /// </para>
    /// </summary>
    private TextMeshProUGUI BuildSlotLabel(
        RectTransform railRect,
        TextMeshProUGUI fontTemplate,
        int flattenedIndex,
        float slotX,
        float slotRowTop)
    {
        var labelObject = new GameObject(
            $"[Runtime] RestorationSlotLabel_{flattenedIndex}", typeof(RectTransform));
        labelObject.transform.SetParent(railRect, false);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        CopyFont(fontTemplate, label);
        label.fontSize = _latinWordLabelFontSize;
        label.alignment = TextAlignmentOptions.Top;
        label.color = _latinWordLabelColor;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        // Starts masked and is repainted by RepaintRail. The syllable printed under a box is as
        // much of the answer as the box's own glyph is — leaving "NA" readable under an empty box
        // would hand back exactly the reading the clue panel now withholds.
        label.text = UnreadableSlotMask;

        // Exactly the slot's own width, at the slot's own x, so "centred under its own box" is a
        // property of the rect rather than of a measured string.
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition =
            new Vector2(slotX, slotRowTop - _slotSize.y - _latinWordLabelGap);
        rect.sizeDelta = new Vector2(_slotSize.x, _latinWordLabelRowHeight);
        return label;
    }

    /// <summary>
    /// The divider drawn between two focus words' slot groups, vertically centred on the slot row.
    ///
    /// <para>
    /// The gap alone already separated the groups; the mark makes the separation something the
    /// player can point at rather than something they have to measure. It is drawn in the gap the
    /// layout was opening anyway, so it adds no width.
    /// </para>
    /// </summary>
    private void BuildWordSeparator(
        RectTransform railRect,
        TextMeshProUGUI fontTemplate,
        int wordIndex,
        float gapX,
        float slotRowTop)
    {
        var separatorObject = new GameObject(
            $"[Runtime] RestorationWordSeparator_{wordIndex}", typeof(RectTransform));
        separatorObject.transform.SetParent(railRect, false);

        TextMeshProUGUI separator = separatorObject.AddComponent<TextMeshProUGUI>();
        CopyFont(fontTemplate, separator);
        separator.fontSize = _wordSeparatorFontSize;
        separator.alignment = TextAlignmentOptions.Center;
        separator.color = _wordSeparatorColor;
        separator.raycastTarget = false;
        separator.textWrappingMode = TextWrappingModes.NoWrap;
        separator.overflowMode = TextOverflowModes.Overflow;
        separator.text = WordSeparatorText;

        RectTransform rect = separatorObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(gapX, slotRowTop);
        rect.sizeDelta = new Vector2(_wordGap, _slotSize.y);
    }

    /// <summary>
    /// How one slot's symbol is named under its box: its authored romanised syllable, uppercased,
    /// falling back to the combat id so a symbol with no romanisation still labels its box rather
    /// than leaving a silent blank the player reads as a rendering fault.
    /// </summary>
    private static string ResolveSlotLatinLabel(BaybayinCharacterSO symbol)
    {
        if (symbol == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(symbol.syllable))
            return symbol.syllable.ToUpperInvariant();

        return !string.IsNullOrWhiteSpace(symbol.characterID)
            ? symbol.characterID.ToUpperInvariant()
            : string.Empty;
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

        Sprite glyphSprite = ResolveSlotGlyph(reference.symbol);
        Image glyph = glyphObject.GetComponent<Image>();
        glyph.sprite = glyphSprite;
        glyph.color = _filledGlyphColor;
        glyph.preserveAspect = true;
        glyph.raycastTarget = false;
        ApplyGlyphSize(glyphObject.GetComponent<RectTransform>(), reference.symbol, glyphSprite);
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
    /// Sizes a slot's glyph so its INK, rather than its PNG, fills the box.
    ///
    /// <para>
    /// The rect is centred on the slot and sized to <see cref="_slotGlyphFill"/> of it, then divided
    /// by the art's ink fraction when the art is almanac art, which is drawn small inside a large
    /// transparent square. Without that division the box is filled with the sprite's empty margin
    /// and the glyph reads at about 40% of its frame — the complaint this answers. The overshoot is
    /// transparent, so a rect that runs past the gold frame draws nothing there; what meets the
    /// frame is the glyph itself, with an even margin left by <see cref="_slotGlyphFill"/>.
    /// </para>
    /// </summary>
    private void ApplyGlyphSize(RectTransform glyphRect, BaybayinCharacterSO symbol, Sprite sprite)
    {
        if (glyphRect == null)
            return;

        bool isAlmanacArt = symbol != null && sprite != null && sprite == symbol.almanacSprite;
        float inkFraction = isAlmanacArt ? Mathf.Max(0.01f, _almanacGlyphInkFraction) : 1f;
        float scale = Mathf.Max(0.01f, _slotGlyphFill) / inkFraction;

        // Centred, sized as a share of the slot, rather than stretched with an absolute inset: the
        // inset was the reason the boxes could grow and the glyphs could not follow.
        glyphRect.anchorMin = new Vector2(0.5f, 0.5f);
        glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
        glyphRect.pivot = new Vector2(0.5f, 0.5f);
        glyphRect.anchoredPosition = Vector2.zero;
        glyphRect.sizeDelta = new Vector2(_slotSize.x * scale, _slotSize.y * scale);
    }

    /// <summary>
    /// The art a filled slot shows: the almanac glyph first, the bare outline second, the framed
    /// badge third — never <c>displaySprite</c>, which is a learning card carrying the romanised
    /// syllable printed on it. A rail built out of learning cards would print the Latin reading in
    /// picture form and defeat the ash exactly as the old text readout did.
    ///
    /// <para>
    /// The almanac art leads because it is the only one of the three that is a finished glyph.
    /// <c>glyphOutlineSprite</c> is a flat white silhouette; it read as a symbol at all only
    /// because the slot tinted it dark gold, and the same character then looked like two different
    /// things in the rail and in the almanac. <c>almanacSprite</c> is self-coloured — a near-white
    /// fill inside a dark brown outline, no card or scroll behind it — so the box now shows the
    /// player the same mark the almanac will show them later. The two fallbacks stay so a character
    /// authored without almanac art still renders something rather than nothing.
    /// </para>
    /// </summary>
    private static Sprite ResolveSlotGlyph(BaybayinCharacterSO symbol)
    {
        if (symbol == null)
            return null;

        if (symbol.almanacSprite != null)
            return symbol.almanacSprite;

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

            // A slot with a glyph in the air is the one case where the resting glyph stays hidden
            // while the state says restored: the flier is standing in for it. This is the ONLY
            // place the two can disagree, and FinishSlotFlight closes the gap from outside the
            // coroutine so a killed flight cannot leave it open.
            if (showGlyph && slot.Flier != null)
                showGlyph = false;

            if (slot.Glyph.gameObject.activeSelf != showGlyph)
                slot.Glyph.gameObject.SetActive(showGlyph);

            // The syllable under the box follows the same rule as the glyph inside it. The label
            // row was originally treated as a fact about the target text rather than about the
            // player's progress, so it printed "I NA" under two empty boxes and read the word out
            // before either had been earned — the same leak the clue panel's mask closes.
            if (slot.Label != null)
            {
                slot.Label.text = restored ? slot.LatinLabel : UnreadableSlotMask;

                // Colour is restored here too, so a repaint landing after a killed flight puts the
                // label back to full brightness even though the routine that was dimming it never
                // got to finish. Only a live flight is allowed to leave it dim.
                if (slot.Flier == null)
                    slot.Label.color = _latinWordLabelColor;
            }
        }

        // Word dividers are set once at build and never repainted: the boundary between two words
        // is a fact about the target text, not about how much of it the player has restored.

        // Left alone while the completion flash owns the alpha, so a repaint landing mid-beat
        // cannot snap the rail back to full opacity halfway through a dip.
        if (_railCanvasGroup != null && _railFlashRoutine == null)
            _railCanvasGroup.alpha = 1f;
    }

    /// <summary>
    /// Pops the slot (or slots) that the just-restored symbol fills.
    ///
    /// <para>
    /// Scoped to the symbol rather than to the whole rail on purpose. The lesson's claim is that
    /// beating THAT enemy filled THAT box; flashing every slot would say "something changed
    /// somewhere", which is the reading the player already had and did not learn from. A symbol
    /// that appears in both focus words legitimately pops twice, because it genuinely filled two
    /// boxes.
    /// </para>
    ///
    /// <para>
    /// Runs on the slot's own scale and on unscaled time. Beat 1 pulls <c>Time.timeScale</c> down to
    /// 0.15 for the lesson, and a pop on scaled time would stretch a 0.42s flourish into nearly
    /// three seconds sitting on top of the line the player is trying to read.
    /// </para>
    /// </summary>
    private void PopSlotsForSymbol(string symbolStableId)
    {
        if (string.IsNullOrEmpty(symbolStableId) || _slotFillPopScale <= 1f
            || _slotFillPopSeconds <= 0f || !isActiveAndEnabled)
        {
            return;
        }

        for (int i = 0; i < _railSlots.Count; i++)
        {
            RailSlot slot = _railSlots[i];
            if (slot?.Anchor == null)
                continue;

            if (!SlotCarriesSymbol(slot, symbolStableId))
                continue;

            if (!_restorationState.IsSlotRestored(slot.Word, slot.DecompositionIndex))
                continue;

            StartCoroutine(PopSlot(slot.Anchor));
        }
    }

    /// <summary>Whether this rail slot's authored reference is the given symbol.</summary>
    private static bool SlotCarriesSymbol(RailSlot slot, string symbolStableId)
    {
        if (slot?.Word?.decomposition == null)
            return false;

        if (slot.DecompositionIndex < 0
            || slot.DecompositionIndex >= slot.Word.decomposition.Count)
        {
            return false;
        }

        BaybayinCharacterSO symbol = slot.Word.decomposition[slot.DecompositionIndex]?.symbol;
        return symbol != null && symbol.stableId == symbolStableId;
    }

    private IEnumerator PopSlot(RectTransform slotRect)
    {
        Vector3 baseScale = slotRect.localScale;
        float half = _slotFillPopSeconds * 0.5f;
        float elapsed = 0f;

        while (elapsed < _slotFillPopSeconds)
        {
            if (slotRect == null)
                yield break;

            // Out for the first half, back for the second, so the box ends exactly where it began
            // even if the routine is interrupted near the end by a rail teardown.
            float t = elapsed < half
                ? elapsed / half
                : 1f - ((elapsed - half) / half);
            float scale = Mathf.Lerp(1f, _slotFillPopScale, Mathf.SmoothStep(0f, 1f, t));
            slotRect.localScale = baseScale * scale;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (slotRect != null)
            slotRect.localScale = baseScale;
    }

    // ---------------------------------------------------------------------------------------
    // Slot glyph flight
    //
    // The rule the whole section is built around: the RESTING state is never owned by a coroutine.
    // RepaintRail has already put every slot in its finished state before a single flier exists,
    // and FinishSlotFlight — a plain method, callable at any time, safe to call twice — is the only
    // thing that takes a slot out of the flying state. The coroutine merely calls it on the way out.
    // Kill the coroutine at any instant and the watchdog, OnDisable or the teardown will call the
    // same method with the same result, which is why the end state after an animation is byte for
    // byte the end state of an immediate fill.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Sends a glyph flying from the defeated enemy into every box that symbol just filled.
    /// </summary>
    /// <param name="symbolStableId">The symbol whose boxes just filled.</param>
    /// <param name="sourceWorldPosition">Where the enemy died, in world space.</param>
    private void LaunchSlotGlyphFlights(string symbolStableId, Vector3 sourceWorldPosition)
    {
        if (!_slotGlyphFlightEnabled || string.IsNullOrEmpty(symbolStableId) || !isActiveAndEnabled)
            return;

        if (_railRoot == null || !_railRoot.activeInHierarchy)
            return;

        if (_railRoot.transform is not RectTransform railRect)
            return;

        Canvas canvas = railRect.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
        Camera worldCamera = Camera.main != null ? Camera.main : uiCamera;
        if (worldCamera == null)
            return;

        Vector3 sourceScreen = worldCamera.WorldToScreenPoint(sourceWorldPosition);

        // Behind the camera. WorldToScreenPoint mirrors the point through the origin rather than
        // failing, so an unchecked negative z would launch the glyph from the wrong side of the
        // screen. Fall through to the immediate fill instead.
        if (sourceScreen.z < 0f)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                railRect, sourceScreen, uiCamera, out Vector2 startLocal))
        {
            return;
        }

        for (int i = 0; i < _railSlots.Count; i++)
        {
            RailSlot slot = _railSlots[i];
            if (slot?.Glyph == null || slot.Glyph.sprite == null)
                continue;

            if (!SlotCarriesSymbol(slot, symbolStableId))
                continue;

            if (!_restorationState.IsSlotRestored(slot.Word, slot.DecompositionIndex))
                continue;

            TryBeginSlotFlight(slot, railRect, uiCamera, startLocal);
        }
    }

    private void TryBeginSlotFlight(
        RailSlot slot, RectTransform railRect, Camera uiCamera, Vector2 startLocal)
    {
        // Two fills in quick succession on the SAME box: retire the one already in the air rather
        // than let a second flier race it. FinishSlotFlight snaps the first one home, so the box is
        // momentarily correct and then takes off again — never two gliding glyphs for one slot, and
        // never a stranded one. Fills on different boxes never reach this branch and fly in
        // parallel, which is what a symbol appearing in both focus words should look like.
        if (slot.Flier != null || slot.FlightRoutine != null)
            FinishSlotFlight(slot);

        if (slot.Glyph == null)
            return;

        var glyphRect = slot.Glyph.transform as RectTransform;
        if (glyphRect == null)
            return;

        Vector3 targetScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, glyphRect.position);

        // The box is off-screen — a rail pushed out of the safe area, or a canvas mid-resize. There
        // is nowhere to fly to that the player can see, so take the immediate fill and leave the
        // slot in its finished state.
        if (targetScreen.x < 0f || targetScreen.y < 0f
            || targetScreen.x > Screen.width || targetScreen.y > Screen.height)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                railRect, targetScreen, uiCamera, out Vector2 endLocal))
        {
            return;
        }

        Rect glyphBounds = glyphRect.rect;
        if (glyphBounds.width <= 0f || glyphBounds.height <= 0f)
            return;

        var flierObject = new GameObject(
            "[Runtime] RestorationSlotGlyphFlier", typeof(RectTransform), typeof(Image));
        flierObject.transform.SetParent(railRect, false);
        // Last sibling so the glyph in the air passes OVER the other boxes rather than sliding
        // behind them.
        flierObject.transform.SetAsLastSibling();

        Image flier = flierObject.GetComponent<Image>();
        flier.sprite = slot.Glyph.sprite;
        flier.color = slot.Glyph.color;
        flier.preserveAspect = true;
        flier.raycastTarget = false;

        var flierRect = (RectTransform)flierObject.transform;
        flierRect.anchorMin = new Vector2(0.5f, 0.5f);
        flierRect.anchorMax = new Vector2(0.5f, 0.5f);
        flierRect.pivot = new Vector2(0.5f, 0.5f);
        flierRect.sizeDelta = glyphBounds.size;
        flierRect.anchoredPosition = startLocal;
        flierRect.localScale = Vector3.one * _slotGlyphFlightStartScale;

        slot.Flier = flierObject;
        // Half a second of slack on top of the scripted length, so a frame hitch or a long editor
        // stall never trips the watchdog on a flight that is merely slow.
        slot.FlightDeadline = Time.unscaledTime
            + _slotGlyphFlightSeconds + _slotGlyphSettleSeconds + _slotGlyphBounceSeconds + 0.5f;

        if (!_slotsInFlight.Contains(slot))
            _slotsInFlight.Add(slot);

        // The resting glyph steps aside for the flier. RepaintRail knows about this and will not
        // undo it while Flier is non-null; FinishSlotFlight puts it back.
        slot.Glyph.gameObject.SetActive(false);

        if (slot.Label != null)
            slot.Label.color = DimmedLabelColor(_latinWordLabelColor);

        slot.FlightRoutine = StartCoroutine(RunSlotFlight(slot, startLocal, endLocal));
    }

    /// <summary>
    /// The label under a box waiting for its glyph to land. Not the mask — the text is already the
    /// real syllable by now — just held back a beat so the brightening reads as the glyph arriving.
    /// </summary>
    private static Color DimmedLabelColor(Color rested) =>
        new Color(rested.r * 0.45f, rested.g * 0.45f, rested.b * 0.45f, rested.a * 0.55f);

    private IEnumerator RunSlotFlight(RailSlot slot, Vector2 startLocal, Vector2 endLocal)
    {
        var flierRect = slot.Flier != null ? slot.Flier.transform as RectTransform : null;
        if (flierRect == null)
        {
            FinishSlotFlight(slot);
            yield break;
        }

        // The control point of a quadratic bezier, lifted along +Y. A straight slide reads as a UI
        // tween; a bow reads as something thrown from the enemy into the word.
        Vector2 mid = (startLocal + endLocal) * 0.5f;
        Vector2 control = mid + (Vector2.up * (Vector2.Distance(startLocal, endLocal)
            * _slotGlyphArcHeightFraction));

        Color restedLabel = _latinWordLabelColor;
        Color dimLabel = DimmedLabelColor(restedLabel);

        // --- Travel -------------------------------------------------------------------------
        float elapsed = 0f;
        while (elapsed < _slotGlyphFlightSeconds)
        {
            if (slot.Flier == null || flierRect == null || !isActiveAndEnabled)
            {
                FinishSlotFlight(slot);
                yield break;
            }

            float t = Mathf.Clamp01(elapsed / _slotGlyphFlightSeconds);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic

            float inv = 1f - eased;
            flierRect.anchoredPosition =
                (inv * inv * startLocal) + (2f * inv * eased * control) + (eased * eased * endLocal);

            float scale = Mathf.Lerp(
                _slotGlyphFlightStartScale, _slotGlyphArrivalScale, eased);
            flierRect.localScale = new Vector3(scale, scale, 1f);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (slot.Flier == null || flierRect == null || !isActiveAndEnabled)
        {
            FinishSlotFlight(slot);
            yield break;
        }

        flierRect.anchoredPosition = endLocal;

        // --- Arrival: back-eased settle, squash, and the frame's impact pulse ----------------
        elapsed = 0f;
        while (elapsed < _slotGlyphSettleSeconds)
        {
            if (slot.Flier == null || flierRect == null || !isActiveAndEnabled)
            {
                FinishSlotFlight(slot);
                yield break;
            }

            float u = Mathf.Clamp01(elapsed / _slotGlyphSettleSeconds);
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            float scale = Mathf.Lerp(_slotGlyphArrivalScale, 1f, eased);

            // A short squash across the first half of the settle: wider than tall on contact,
            // recovering as it seats. Small — this is a glyph landing in a box, not a rubber ball.
            float squash = Mathf.Sin(u * Mathf.PI) * 0.10f;
            flierRect.localScale = new Vector3(scale * (1f + squash), scale * (1f - squash), 1f);

            if (slot.Frame != null)
            {
                slot.Frame.color = Color.Lerp(
                    _filledSlotColor, Color.white, Mathf.Sin(u * Mathf.PI) * 0.6f);
            }

            if (slot.Label != null)
                slot.Label.color = Color.Lerp(dimLabel, restedLabel, eased);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // --- Trailing bounce ----------------------------------------------------------------
        elapsed = 0f;
        while (elapsed < _slotGlyphBounceSeconds && _slotGlyphBounceAmount > 0f)
        {
            if (slot.Flier == null || flierRect == null || !isActiveAndEnabled)
            {
                FinishSlotFlight(slot);
                yield break;
            }

            float u = Mathf.Clamp01(elapsed / _slotGlyphBounceSeconds);
            float bounce = 1f + (Mathf.Sin(u * Mathf.PI) * _slotGlyphBounceAmount);
            flierRect.localScale = new Vector3(bounce, bounce, 1f);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        FinishSlotFlight(slot);
    }

    /// <summary>
    /// Puts one slot back into its resting, restored state and forgets the flight.
    ///
    /// <para>
    /// Deliberately a plain method rather than the tail of the coroutine, and deliberately safe to
    /// call on a slot that is not flying, twice in a row, or after the flier has already been
    /// destroyed by something else. Unity will not run a stopped coroutine's <c>finally</c>, so this
    /// is the only thing standing between an interrupted flight and a permanently blank box.
    /// </para>
    /// </summary>
    private void FinishSlotFlight(RailSlot slot)
    {
        if (slot == null)
            return;

        if (slot.FlightRoutine != null)
        {
            // Safe on a routine that has already completed, and it is what makes "retire the flight
            // already in the air" work when a second fill lands on the same box.
            StopCoroutine(slot.FlightRoutine);
            slot.FlightRoutine = null;
        }

        if (slot.Flier != null)
        {
            DestroyOwnedObject(slot.Flier);
            slot.Flier = null;
        }

        _slotsInFlight.Remove(slot);

        bool restored = _restorationState.IsSlotRestored(slot.Word, slot.DecompositionIndex);

        if (slot.Glyph != null)
        {
            var glyphRect = slot.Glyph.transform as RectTransform;
            if (glyphRect != null)
                glyphRect.localScale = Vector3.one;

            bool showGlyph = restored && slot.Glyph.sprite != null;
            if (slot.Glyph.gameObject.activeSelf != showGlyph)
                slot.Glyph.gameObject.SetActive(showGlyph);
        }

        if (slot.Frame != null)
            slot.Frame.color = restored ? _filledSlotColor : _emptySlotColor;

        if (slot.Label != null)
            slot.Label.color = _latinWordLabelColor;
    }

    /// <summary>Drains every flight, leaving each slot at rest. Used on teardown and disable.</summary>
    private void FinishAllSlotFlights()
    {
        // Walked backwards over a copy of the count because FinishSlotFlight removes from the list.
        for (int i = _slotsInFlight.Count - 1; i >= 0; i--)
        {
            if (i < _slotsInFlight.Count)
                FinishSlotFlight(_slotsInFlight[i]);
        }

        _slotsInFlight.Clear();
    }

    /// <summary>
    /// Once-a-frame watchdog. Catches the cases the coroutine cannot catch itself: the flier being
    /// destroyed out from under it, the rail being switched off mid-flight, or the slot list having
    /// been rebuilt under a flight that is still notionally running.
    /// </summary>
    private void ReconcileSlotFlights()
    {
        if (_slotsInFlight.Count == 0)
            return;

        bool railUsable = _railRoot != null && _railRoot.activeInHierarchy;

        for (int i = _slotsInFlight.Count - 1; i >= 0; i--)
        {
            if (i >= _slotsInFlight.Count)
                continue;

            RailSlot slot = _slotsInFlight[i];
            if (slot == null)
            {
                _slotsInFlight.RemoveAt(i);
                continue;
            }

            bool overdue = Time.unscaledTime > slot.FlightDeadline;
            if (!railUsable || overdue || slot.Flier == null || !_railSlots.Contains(slot))
                FinishSlotFlight(slot);
        }
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

        // Drained before the slot list is cleared, so each flight still has a slot to rest. Fliers
        // are children of the rail root and would go with it anyway; this is about not leaving
        // _slotsInFlight holding slots that no longer belong to a rail.
        FinishAllSlotFlights();

        _railSlots.Clear();
        _railSlotAnchors.Clear();

        Texture2D frameTexture =
            _runtimeSlotFrameSprite != null ? _runtimeSlotFrameSprite.texture : null;

        DestroyOwnedObject(_railRoot);
        DestroyOwnedObject(_runtimeSlotFrameSprite);
        DestroyOwnedObject(frameTexture);

        _railRoot = null;
        _railCanvasGroup = null;
        _runtimeSlotFrameSprite = null;

        // Hand the band back with the rail. A level that tears its rail down and never builds
        // another must frame exactly as a level that never had one.
        if (_bandRefreshColumn != null && _bandRefreshHandler != null)
            _bandRefreshColumn.OnPlayAreaChanged -= _bandRefreshHandler;
        _bandRefreshColumn = null;

        if (AspectLockedCamera.Instance != null)
            AspectLockedCamera.Instance.SetBottomBandPixels(0f);
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
    /// Replaces every syllable the player has not yet restored with an underscore run, so the word
    /// is retrieved rather than read. <paramref name="symbolStableId"/> is kept on the signature
    /// because callers and tests identify the clue by it, but the mask no longer turns on it: the
    /// needed slot is unrestored by definition and is masked by the restoration rule.
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

            bool isAshedSlot = ashFirstSlot && emittedSlots == 0;
            bool isRestoredSlot = restorationState != null
                && restorationState.IsSlotRestored(word, i);
            emittedSlots++;

            // A slot is readable ONLY once the player has RESTORED it. Previously only the slot
            // currently needed was masked, so INA opened as "__na": the player could read NA off
            // the panel before ever drawing it, and the Iligaw lesson's "one character, one piece
            // of the memory — restored" was being said over a word that was already most of the
            // way on screen. INA now reads "____" at level start, "i__" once I is restored, and
            // "ina" once NA's carrier falls.
            //
            // The ash is unchanged and still composes on top: Abo ng Simula masks the word's first
            // slot whatever this rule says about it, including a slot already restored — which is
            // now the only state in which the ash has anything to take away.
            //
            // isTargetSlot is no longer read here: "the slot you need" and "the slot you have not
            // earned" only ever differed for slots the player had not earned either, so the needed
            // slot is covered by the restoration rule itself.
            builder.Append(!isRestoredSlot || isAshedSlot
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
