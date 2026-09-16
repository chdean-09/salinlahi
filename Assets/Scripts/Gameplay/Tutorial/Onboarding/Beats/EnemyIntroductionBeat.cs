using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The four-step enemy introduction beat: <b>Halt, Name, Ability, Release</b>. It fires once per
/// enemy type, on that type's first ever spawn campaign-wide, and it is the only place a player is
/// told what a new enemy does.
///
/// <para>
/// <b>Why it is not an <see cref="OnboardingBeat"/>.</b> Every beat in this folder is selected by
/// <c>OnboardingBeatType</c> from the sequence asset's authored <c>beatOrder</c> and played in that
/// order by <c>Level1OnboardingController</c> before combat. This beat cannot live there: its
/// trigger is a spawn, mid-combat, at a time nobody can author — and it must be reusable by every
/// level for any type the player has not met, which a fixed per-level order cannot express. It keeps
/// the beat <i>shape</i> instead (one coroutine per run, serialized tuning, every borrowed piece of
/// world state restored on the way out) and is driven from <c>Enemy.Initialize</c>.
/// </para>
///
/// <para>
/// <b>Player input stays enabled for the whole beat.</b> That is deliberate and it is the rule most
/// likely to be broken by a well-meaning edit. The beat interrupts a live field — other enemies keep
/// walking, slowed — so a player who has already read the board and started a stroke must be able to
/// finish it. Nothing here calls <c>EnterDialoguePause</c> or
/// <c>TutorialRuntimeState.SetDrawingInputLocked</c>, and the card surface disables its own
/// raycasts.
/// </para>
///
/// <para>
/// <b>The step durations are wall-clock, not game time.</b> The beat drops <c>Time.timeScale</c> to
/// a per-level value, so a scaled wait would stretch a 2-second card to over thirteen seconds at the
/// Level 1 value of 0.15. Every wait here is realtime, which is also what lets the cards consume
/// wall-clock without consuming the spawn schedule they interrupt.
/// </para>
///
/// <para>
/// <b>Its other half is the suppression rule.</b> A type's ability is inert on the spawn that
/// introduces it and arms on a later spawn. That is what keeps a first ability sighting legible
/// rather than bug-like — the card sets the expectation, and the ability pays it off once the player
/// has a baseline to notice it against. The suppression itself is applied in
/// <c>Enemy.Initialize</c>, keyed off the claim this beat hands back, so the two can never disagree
/// about which spawn was the introduction.
/// </para>
/// </summary>
public sealed class EnemyIntroductionBeat : MonoBehaviour
{
    [Header("Surfaces")]
    [Tooltip("The card and lifetime banner. Required: with no card the beat declines every introduction rather than halting the game behind nothing.")]
    [SerializeField] private EnemyIntroductionCardView _card;

    [Tooltip("Full-screen dim that leaves only the new enemy lit. Optional: left null, one is created at runtime the first time a card plays.")]
    [SerializeField] private TutorialSpotlightOverlay _vignette;

    [Tooltip("World camera the vignette converts the enemy's position through. Falls back to Camera.main.")]
    [SerializeField] private Camera _worldCamera;

    [Header("Step 1 — Halt")]
    [Tooltip("Seconds (wall-clock) the halt takes: the vignette closes and Time.timeScale ramps from its current value down to _introductionTimeScale.")]
    [SerializeField] private float _haltRampSeconds = 0.6f;

    [Tooltip("Time.timeScale held for the whole card. Authored per level — the guidance fade plan lowers the slow from 0.15 on Level 1 to 1.0 by Level 5, where the card plays at full speed.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float _introductionTimeScale = 0.15f;

    [Tooltip("World units of padding around the enemy left undimmed by the vignette.")]
    [SerializeField] private float _vignettePaddingWorld = 0.6f;

    [Header("Step 2 — Name")]
    [Tooltip("Seconds (wall-clock) the card holds showing only the walk sprite, display name and subtitle, before the ability line appears.")]
    [SerializeField] private float _nameStepSeconds = 2f;

    [Header("Step 3 — Ability")]
    [Tooltip("Seconds (wall-clock) the ability line is held on the card before the release begins.")]
    [SerializeField] private float _abilityStepSeconds = 3f;

    [Header("Step 4 — Release")]
    [Tooltip("Seconds (wall-clock) the release takes: the card slides out, the vignette lifts and Time.timeScale ramps back to the value the beat found on entry.")]
    [SerializeField] private float _releaseRampSeconds = 0.4f;

    [Tooltip("Seconds (wall-clock) the restored glyph badge is held before the card asks for a tap, "
             + "so the symbol registers as part of the card rather than as the thing that ended it.")]
    [SerializeField, Min(0f)] private float _glyphRevealStepSeconds = 1f;

    [Tooltip("Seconds (wall-clock) the card will hold for a tap before releasing itself. A safety "
             + "valve for an input device that never reports, NOT the intended path: left at 0 the "
             + "card waits as long as it takes.")]
    [SerializeField, Min(0f)] private float _continueHoldTimeoutSeconds = 0f;

    [Tooltip("Seconds (wall-clock) waited after the enemy is positioned before the halt begins, so the card never lands on a shell still parked at its off-screen pool position.")]
    [SerializeField] private float _spawnSettleSeconds = 0.1f;

    [Header("Step 0 — Walk into frame")]
    [Tooltip("Seconds (wall-clock) the beat will let the introduced enemy keep walking until it is "
             + "fully inside the camera's view before halting it. The spawner releases enemies ABOVE "
             + "the visible play area, so halting on the settle frame alone freezes the subject "
             + "off-screen and the whole lesson plays against an empty field. Safety valve only: on "
             + "timeout the beat halts where the enemy stands and warns, rather than hanging.")]
    [SerializeField, Min(0f)] private float _onScreenWaitTimeoutSeconds = 14f;

    [Tooltip("World units of clearance required between the introduced enemy's own bounds (body AND "
             + "glyph badge, counted even while the badge is hidden for a late reveal) and the edge "
             + "of the camera's view before the halt begins. Buys the vignette and the beat-7 badge "
             + "reveal room to render without clipping the screen edge.")]
    [SerializeField, Min(0f)] private float _onScreenMarginWorld = 0.35f;

    [Tooltip("HUD rect whose bottom edge the introduced enemy must halt BELOW. Left null, the "
             + "scene's ActiveCluePresenter clue panel is used, which is the rect that actually "
             + "occludes the top of the playfield. Inside the camera is not the same as visible: "
             + "the clue panel is drawn in FRONT of the lane, so an enemy halted against the "
             + "camera's top edge is parked behind it.")]
    [SerializeField] private RectTransform _hudOcclusionRect;

    [Tooltip("World units of clearance left between the HUD's occluded band and the top of the "
             + "introduced enemy's bounds. Small on purpose: the halt should read as 'just under "
             + "the HUD', not as 'halfway down the field'.")]
    [SerializeField, Min(0f)] private float _hudClearanceWorld = 0.25f;

    [Tooltip("The halt line, as a fraction of the camera's visible height measured DOWN from its "
             + "top edge (0 = top of the view, 0.5 = the middle). The introduced enemy keeps walking "
             + "until the top of its bounds (plus the on-screen margin) has crossed below this "
             + "line, so the card lands on a sprite the player has watched arrive rather than one "
             + "still hugging the HUD. Combined with the HUD rule: whichever is LOWER wins. 0 "
             + "disables it. Select this object in the Scene view to see the line drawn.")]
    [Range(0f, 0.9f)]
    [SerializeField] private float _haltLineViewportFromTop = 0.3f;

    [Header("Lesson — Beat 2 (Ability)")]
    [Tooltip("Safety valve only. Seconds (wall-clock) beat 2 will wait for the introduced enemy's "
             + "ability to actually fire before giving up and continuing. Generous on purpose: "
             + "AshFirstSlotController's 1.5 s arm delay accrues on SCALED time under this beat's "
             + "0.15 time scale, so the ash legitimately needs around ten wall-clock seconds. "
             + "Shortening this re-introduces the bug where the enemy is named before it has done "
             + "anything. See PlayAbilityBeat.")]
    [SerializeField, Min(0f)] private float _abilityBeatArmTimeoutSeconds = 15f;

    [Header("Lesson — Beat 8 (Draw)")]
    [Tooltip("Seconds (wall-clock) the step's successText is held after a correct draw, before the "
             + "guide closes. Time is already back to normal by beat 8, so this is a live-combat "
             + "pause: long enough to read one short line, no longer.")]
    [SerializeField, Min(0f)] private float _drawSuccessHoldSeconds = 1.5f;

    /// <summary>
    /// The single live runner. One scene holds at most one, because the beat takes over global state
    /// — <c>Time.timeScale</c> and a full-screen dim — that two runners could not share.
    /// </summary>
    private static EnemyIntroductionBeat s_instance;

    /// <summary>
    /// Normalized enemy IDs whose authored LESSON has already run during this level attempt.
    ///
    /// <para>
    /// Instance state, not static, and that is the whole point: one runner is created per scene
    /// load, so this set is empty every time the level is entered and the lesson gets exactly one
    /// run per play. The campaign-wide record in <see cref="EnemyIntroductionProgress"/> is what
    /// stops a lesson repeating within a run; it cannot also express "once per play", because it
    /// deliberately survives a level reload.
    /// </para>
    /// </summary>
    private readonly HashSet<string> _introducedThisAttempt = new();

    /// <summary>
    /// True when the claim now in flight is a forced replay — the type was already introduced
    /// campaign-wide and only the level's <c>alwaysShowTutorial</c> flag let it through. Read by
    /// <see cref="PlayLesson"/> to decide whether beats 3 and 4 replay with the rest of the lesson.
    /// Single field rather than a set because at most one run is ever in flight (<c>_routineActive</c>).
    /// </summary>
    private bool _lessonIsForcedReplay;

    private Enemy _claimedEnemy;
    private Coroutine _routine;
    private bool _isPlaying;
    private bool _routineActive;
    private bool _timeScaleTaken;
    private bool _drawStepAbandoned;
    private float _restoreTimeScale = 1f;
    private TutorialSpotlightOverlay _runtimeVignette;

    /// <summary>
    /// The banner's own lifetime wait, which deliberately outlives the run that raised it.
    ///
    /// <para>
    /// <b>Why it is not part of the run's coroutine any more, and the bug that forced the split.</b>
    /// The banner stays for as long as the introduced enemy is on the field — ordinary combat, for
    /// up to twenty seconds. While that wait lived inside <see cref="PlayIntroduction"/>,
    /// <see cref="_routineActive"/> stayed true for all of it and <see cref="TryClaim"/> refused
    /// every claim, but <see cref="IsHoldingSpawnSchedule"/> reads <see cref="_isPlaying"/>, which
    /// falls the moment the last beat ends. The spawner therefore resumed while claims were still
    /// being refused, and the first type to arrive in that gap got
    /// <see cref="IntroductionOutcome.None"/>: no card, no explanation, and — because a refused
    /// claim does not spend the one-shot — a card that turned up some arbitrary later spawn
    /// instead. That is the "Abo is not introduced the first time he appears" report. The two
    /// windows must be the same window; keeping the banner here is what makes them so.
    /// </para>
    /// </summary>
    private Coroutine _bannerRoutine;

    /// <summary>
    /// True while a card or lesson is on screen. Diagnostic and test seam.
    ///
    /// <para>
    /// <b>This is NOT the coroutine's lifetime, and the difference is a bug that shipped.</b> The
    /// run ends with a banner that deliberately outlives the lesson — it stays for as long as the
    /// introduced enemy is on the field, which can be another twenty seconds of ordinary combat.
    /// While that wait was inside the flag, the game still believed it was mid-lesson long after
    /// the last beat had visibly finished, and everything gated on this flag (the harness's own
    /// input restriction among them) stayed gated. The flag now falls the moment the last beat
    /// ends — and so does <see cref="_routineActive"/>, because the banner's wait runs in
    /// <see cref="_bannerRoutine"/> rather than in the run. A second introduction raised under a
    /// standing banner simply replaces it; see <see cref="ShowBannerForLifetime"/>.
    /// </para>
    /// </summary>
    public static bool IsPlaying => s_instance != null && s_instance._isPlaying;

    /// <summary>
    /// True while a lesson or card owns the screen and the wave schedule must not advance.
    ///
    /// <para>
    /// <b>Why the schedule is held rather than the player made safe.</b> Beats 8 and 9 hand time
    /// and movement back on purpose — the draw the lesson asks for is real combat against a real
    /// enemy, and the beat's standing promise is that player INPUT is live throughout. Making the
    /// player invulnerable would break that promise from the other side: the one draw the lesson
    /// teaches would be the one draw that could not matter. What actually killed five runs out of
    /// five was not the enemy on screen but the ones still ARRIVING behind it, on a spawn clock
    /// that kept running through a seventeen-second reading-and-drawing exercise. Holding the
    /// clock stops the field growing while the player is being taught, and changes nothing about
    /// what the enemies already on it can do.
    /// </para>
    /// </summary>
    public static bool IsHoldingSpawnSchedule => s_instance != null && s_instance._isPlaying;

    /// <summary>
    /// Resolves what this spawn does about its type's introduction.
    ///
    /// <para>
    /// Called from <c>Enemy.Initialize</c> BEFORE the ability components are configured, because
    /// the outcome also decides suppression. Returning <see cref="IntroductionOutcome.None"/> or
    /// <see cref="IntroductionOutcome.DeferAndSuppress"/> does not spend the type's one-shot.
    /// </para>
    ///
    /// <para>
    /// <see cref="IntroductionOutcome.None"/> leaves the ability armed, which is the safe failure:
    /// the player meets an ability with no card, rather than meeting an enemy whose ability is
    /// silently switched off forever. <see cref="IntroductionOutcome.DeferAndSuppress"/> is the
    /// deliberate exception — the decline exists so a pending lesson lands first, so it suppresses
    /// instead of arming. See <c>IntroductionDecision</c> for both rules together.
    /// </para>
    /// </summary>
    public static IntroductionOutcome ResolveIntroduction(Enemy enemy, EnemyDataSO data)
    {
        if (s_instance == null || enemy == null || data == null)
            return IntroductionOutcome.None;

        return s_instance.ResolveFor(enemy, data);
    }

    private IntroductionOutcome ResolveFor(Enemy enemy, EnemyDataSO data)
    {
        EnemyLessonSO lesson = ResolveLesson(data);
        bool claimed = TryClaim(enemy, data, lesson);
        return IntroductionDecision.Resolve(
            claimAccepted: claimed,
            lessonArmsAbility: claimed && lesson != null && lesson.armAbilityOnIntroduction,
            aLessonIsPending: !claimed && ResolvePendingLesson() != null);
    }

    /// <summary>The level's authored lesson for this type, or null.</summary>
    private EnemyLessonSO ResolveLesson(EnemyDataSO data) =>
        EnemyLessonLookup.Find(GameManager.CurrentLevelConfig, data);

    /// <summary>
    /// The level's authored lesson if it has not played yet, else null. A lesson whose enemy has
    /// already been introduced is not pending, which is what lets deferral end.
    /// </summary>
    private EnemyLessonSO ResolvePendingLesson()
    {
        LevelConfigSO config = GameManager.CurrentLevelConfig;
        if (config?.enemyLessons == null)
            return null;

        List<EnemyDataSO> roster = LevelRoster.BuildIntroducibleRoster(config);

        for (int i = 0; i < config.enemyLessons.Length; i++)
        {
            EnemyLessonSO lesson = config.enemyLessons[i];
            if (lesson?.enemy == null)
                continue;

            // A lesson for an enemy the level's wave table never spawns must not defer forever:
            // ResolvePendingLesson only stops returning a lesson once its enemy has been
            // introduced, and an enemy that never spawns is never introduced — which would wedge
            // every other type on the level into permanent suppression.
            if (!RosterContains(roster, lesson.enemy))
                continue;

            if (!HasLessonHadItsRun(lesson))
                return lesson;
        }

        return null;
    }

    /// <summary>
    /// Whether the roster contains this enemy type. Matches the same identity rule as
    /// <see cref="EnemyLessonLookup.Find"/> — reference or case-insensitive <c>enemyID</c> — so a
    /// pooled or domain-reloaded instance still matches its roster entry.
    /// </summary>
    private static bool RosterContains(List<EnemyDataSO> roster, EnemyDataSO enemy)
    {
        if (roster == null || enemy == null)
            return false;

        for (int i = 0; i < roster.Count; i++)
        {
            EnemyDataSO candidate = roster[i];
            if (candidate == null)
                continue;

            if (candidate == enemy)
                return true;

            if (!string.IsNullOrEmpty(candidate.enemyID)
                && string.Equals(candidate.enemyID, enemy.enemyID,
                    System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Starts the beat for an enemy whose claim was accepted. Separate from the claim because
    /// <c>Enemy.Initialize</c> claims early, to decide suppression, but the wave spawner sets the
    /// enemy's field position and carried character only <i>after</i> Initialize returns — so a beat
    /// started at claim time would spotlight the pool's off-screen parking spot.
    /// </summary>
    public static void BeginIntroduction(Enemy enemy)
    {
        if (s_instance == null || enemy == null)
            return;

        s_instance.Begin(enemy);
    }

    private void OnEnable()
    {
        // Last one enabled wins rather than first: a level reload brings up a new runner while the
        // outgoing scene's copy may not have been destroyed yet, and the stale one would otherwise
        // keep answering claims with a card that is about to be torn down.
        s_instance = this;
    }

    private void OnDisable()
    {
        // The beat holds two pieces of global state. A scene unload or a level abort mid-card would
        // otherwise leave the game running at 0.15 with the screen dimmed and no way back.
        //
        // Captured before StopPlayback() clears it: PlayIntroduction's own finally block is what
        // normally restores the enemy's glyph badge and hands its movement back, but Unity does not
        // run a stopped coroutine's pending finally -- StopPlayback's StopCoroutine below removes it
        // from the scheduler without unwinding it. An abort here has to redo both explicitly, or the
        // enemy is left frozen mid-field with its badge permanently hidden.
        Enemy claimedEnemy = _claimedEnemy;

        StopPlayback();
        ReleaseTimeScale();
        LiftVignette();
        IsHoldingForContinue = false;
        if (_card != null)
        {
            _card.HideCardImmediate();
            _card.HideBanner();
        }

        if (claimedEnemy != null)
        {
            claimedEnemy.GlyphBadge?.Show();
            ReleaseEnemy(claimedEnemy);
            HoldIntroducibleAbility(claimedEnemy, held: false);
        }

        if (s_instance == this)
            s_instance = null;
    }

    private bool TryClaim(Enemy enemy, EnemyDataSO data, EnemyLessonSO lesson)
    {
        // _routineActive, not _isPlaying: the two now fall together (the banner outlives the run on
        // its own coroutine), and this is the flag that says a run still owns Time.timeScale and the
        // dim. It must stay in step with IsHoldingSpawnSchedule — a window where claims are refused
        // but spawns are not held is a spawn the player meets with no card at all.
        if (_routineActive)
            return false;

        // A claim that was accepted but never begun — the enemy was returned to the pool between
        // Initialize and the spawner's positioning pass — would otherwise latch this runner shut for
        // the rest of the level. Playback sets _isPlaying before its first yield, so a claim held
        // with nothing playing is always stale.
        if (_claimedEnemy != null)
            _claimedEnemy = null;

        if (_card == null || !_card.CanPresent)
            return false;

        if (!IsIntroducibleSpawn(enemy, data, lesson))
            return false;

        // The lesson's precondition. Declining here deliberately does NOT spend the type's
        // one-shot, so an Abo who arrives before the clue can lose anything simply introduces
        // himself on a later spawn instead of burning his introduction on a no-op ash.
        if (lesson != null && RestoredSlotCount() < lesson.requiredRestoredSlots)
            return false;

        if (!ClaimIntroduction(data, lesson))
            return false;

        _claimedEnemy = enemy;
        return true;
    }

    /// <summary>
    /// Spends this type's introduction, or — on a level that replays its tutorial — grants the
    /// authored lesson a replay the campaign-wide one-shot would otherwise refuse.
    ///
    /// <para>
    /// <b>The defect this closes.</b> <c>Level1_Config.alwaysShowTutorial</c> makes Level 1's
    /// pre-combat onboarding (Juan's intro, the base intro, the heart-loss demo) play on every
    /// single visit, and <see cref="LevelTutorialProgress"/> honours that. The eight-beat lesson
    /// embedded in the same level's combat consulted only the campaign-wide introduction record, so
    /// a player who had met Iligaw once — even in a previous save session — replayed all the
    /// framing and then got silence where the actual lesson should be. The level's own replay flag
    /// is the authority for both halves; it is read here through the same file that answers it for
    /// the onboarding, not re-derived.
    /// </para>
    ///
    /// <para>
    /// <b>Scoped to lessons, deliberately.</b> A plain four-step card is campaign-wide discovery —
    /// "you have never met this type" — and a type the player has known for hours must not be
    /// re-introduced every time they revisit an early level. A LESSON is different: it is authored
    /// on one specific level, it teaches that level's material, and it is that level's tutorial in
    /// every sense the flag means. So <paramref name="lesson"/> being non-null is a precondition of
    /// the replay, and a level without <c>alwaysShowTutorial</c> keeps spending its one-shot exactly
    /// as before — the replay can never leak into "the lesson always plays everywhere".
    /// </para>
    /// </summary>
    private bool ClaimIntroduction(EnemyDataSO data, EnemyLessonSO lesson)
    {
        string enemyID = EnemyDiscoveryProgress.NormalizeEnemyID(data);

        // The ordinary path: a genuine first meeting, campaign-wide. Recorded here whether or not
        // the level replays, so the record stays truthful — the flag defeats the gate, it does not
        // rewrite history. Compare LevelTutorialProgress's Mark* methods, which keep writing for
        // the same reason.
        if (EnemyIntroductionProgress.TryClaimIntroduction(data))
        {
            _lessonIsForcedReplay = false;

            // A first run counts against this attempt too, or the per-attempt rules below — the
            // replay level's deferral, the roster gate — would never see the type as met and would
            // wedge every other type on the level into permanent suppression.
            if (enemyID != null)
                _introducedThisAttempt.Add(enemyID);

            return true;
        }

        if (enemyID == null || !ReplaysOnThisLevel(data, lesson))
            return false;

        if (!_introducedThisAttempt.Add(enemyID))
            return false;

        _lessonIsForcedReplay = lesson != null;
        return true;
    }

    /// <summary>
    /// Whether this type's introduction plays again on every attempt of the current level, even
    /// though the campaign-wide one-shot is spent. Two authorities, either suffices:
    /// <list type="bullet">
    /// <item>The level replays its tutorial and this is its authored lesson (the original rule).</item>
    /// <item>The type DEBUTS on this level (<see cref="EnemyDebutLookup"/>). The level that
    /// teaches a type introduces it every time; a type met on an earlier level is never
    /// re-introduced, which keeps later levels from re-explaining what the player knows.</item>
    /// </list>
    /// Per attempt, not per spawn: the second spawn of the same type in one attempt is an
    /// ordinary enemy, held by <see cref="_introducedThisAttempt"/>.
    /// </summary>
    private static bool ReplaysOnThisLevel(EnemyDataSO data, EnemyLessonSO lesson)
    {
        if (lesson != null && LevelReplaysItsTutorial())
            return true;

        return EnemyDebutLookup.DebutsOnCurrentCampaignLevel(GameManager.CurrentLevelConfig, data);
    }

    /// <summary>
    /// Starts a new level attempt: forgets which types were introduced during the previous one, so
    /// the debut-level replay and the roster gate both start from zero. Called by the level flow
    /// next to <c>SpawnAssignmentCoordinator.ApplyLevel</c>, which resets the gate registry on the
    /// same clock — and BEFORE it, because ApplyLevel evaluates the gate against this record.
    /// </summary>
    public static void BeginAttempt()
    {
        if (s_instance != null)
            s_instance._introducedThisAttempt.Clear();
    }

    /// <summary>
    /// The roster gate's notion of "met", on the attempt's clock rather than the campaign's: a
    /// type introduced during this attempt counts; a type that debuts on this level counts ONLY
    /// if introduced this attempt, because its card replays every attempt and the gate must hold
    /// until it has; any other type falls back to the campaign-wide record, exactly as before.
    /// Shared by the level-start and post-introduction evaluations so they cannot disagree.
    /// </summary>
    public static bool CountsAsIntroducedThisAttempt(LevelConfigSO level, EnemyDataSO data)
    {
        if (data == null)
            return false;

        string enemyID = EnemyDiscoveryProgress.NormalizeEnemyID(data);
        if (enemyID != null && s_instance != null && s_instance._introducedThisAttempt.Contains(enemyID))
            return true;

        if (EnemyDebutLookup.DebutsOnCurrentCampaignLevel(level, data))
            return false;

        return EnemyIntroductionProgress.HasBeenIntroduced(data);
    }

    /// <summary>
    /// Whether the level being played replays its tutorial on every visit. Delegated to
    /// <see cref="LevelTutorialProgress"/> so the lesson and the pre-combat onboarding can never
    /// disagree about the same flag.
    /// </summary>
    private static bool LevelReplaysItsTutorial() =>
        LevelTutorialProgress.AlwaysShowsTutorialForLevel(GameManager.CurrentLevelConfig);

    /// <summary>
    /// Whether this lesson has already had its run for the purposes of deferral. On a replay level
    /// that is a per-attempt question; everywhere else it is the campaign-wide record, unchanged.
    /// </summary>
    private bool HasLessonHadItsRun(EnemyLessonSO lesson)
    {
        if (!ReplaysOnThisLevel(lesson.enemy, lesson))
            return EnemyIntroductionProgress.HasBeenIntroduced(lesson.enemy);

        string enemyID = EnemyDiscoveryProgress.NormalizeEnemyID(lesson.enemy);
        return enemyID != null && _introducedThisAttempt.Contains(enemyID);
    }

    /// <summary>
    /// Focus-word slots restored so far. Read from the presenter rather than tracked here, so the
    /// number the precondition reads is the same number the clue renders. See spec section 4.2.
    /// </summary>
    private static int RestoredSlotCount()
    {
        ActiveCluePresenter presenter = FindFirstObjectByType<ActiveCluePresenter>(
            FindObjectsInactive.Include);
        return presenter != null ? presenter.RestoredSlotCount : 0;
    }

    /// <summary>
    /// Whether a spawn is a genuine first meeting the player can be shown.
    ///
    /// <para>
    /// The decoy and suppressed-discovery cases are not hypothetical: Iligaw's mirror copy is a
    /// runtime clone of Iligaw's own data and carries the same <c>enemyID</c>, so without this guard
    /// the false copy could claim and burn Iligaw's introduction — and the card would then be
    /// pointing at the shadow rather than the enemy.
    /// </para>
    ///
    /// <para>
    /// A tutorial sequence driving combat is excluded because its beats spawn frozen enemies at
    /// authored positions under their own timing; halting one of those and dropping the time scale
    /// underneath a beat that is already mid-lesson would fight it for the same global state.
    /// </para>
    /// </summary>
    private static bool IsIntroducibleSpawn(Enemy enemy, EnemyDataSO data, EnemyLessonSO lesson)
    {
        if (enemy.IsBoss || data.isDecoy || data.suppressDiscovery)
            return false;

        // Without a name there is nothing for step 2 to show, and a nameless card would halt the
        // field to display an ability line under a blank heading.
        if (string.IsNullOrWhiteSpace(data.displayName))
            return false;

        if (TutorialRuntimeState.IsCombatOverrideActive || TutorialRuntimeState.IsDrawingInputLocked)
            return false;

        // The heart-loss demo's stand-in is a scripted prop, not a first meeting. It is a REAL
        // pooled enemy parked on the shrine to stage a base hit, and in one observed run it claimed
        // Hati's introduction mid-demo: a full card, time slow and vignette landed inside the
        // tutorial preamble, on an enemy the player is being shown losing, and Hati's one-shot was
        // spent on it. Declining here rather than inside TryClaim's progress call is what keeps the
        // one-shot unspent, so the type still introduces itself properly on its first real spawn.
        if (TutorialRuntimeState.IsHeartLossDemoActive)
            return false;

        // Deferral. While this level's lesson is still pending, every other type waits: the rule
        // that enemies have abilities is taught once, by the lesson, and a card that lands first
        // would spend that first-meeting moment on an enemy the lesson did not choose.
        if (lesson == null && s_instance != null && s_instance.ResolvePendingLesson() != null)
            return false;

        // The beat's promise is that input stays live through it. Before the run has started
        // accepting drawings there is no such promise to keep, and the halt would read as a freeze.
        return GameManager.Instance != null && GameManager.Instance.AcceptsDrawingInput;
    }

    private void Begin(Enemy enemy)
    {
        if (_claimedEnemy != enemy || _routineActive)
            return;

        // Applied HERE, synchronously, and not from inside the coroutine. Begin is called from
        // Enemy.Initialize, which runs inside EnemyPool.Get — before the spawner has positioned
        // this enemy and therefore before its components have had a single Update. A hold set after
        // the coroutine's first yield is already too late: MirrorDecoyController places its copy on
        // that very first Update, which is why the "one becomes two" moment used to happen one frame
        // after the spawn, off-camera, ten seconds before the beat that exists to show it.
        HoldIntroducibleAbility(enemy, held: true);

        _routine = StartCoroutine(PlayIntroduction(enemy));
    }

    /// <summary>
    /// Asks this spawn's signature ability to wait, or to stop waiting. Opt-in through
    /// <see cref="IIntroductionHoldable"/> rather than through <see cref="IIntroducibleAbility"/>
    /// itself: an ability with nothing to hold back — one whose effect is a change to things already
    /// on screen rather than a new body arriving — simply does not implement it and is unaffected.
    /// </summary>
    private static void HoldIntroducibleAbility(Enemy enemy, bool held)
    {
        if (ResolveIntroducibleAbility(enemy) is IIntroductionHoldable holdable)
            holdable.SetIntroductionHold(held);
    }

    private IEnumerator PlayIntroduction(Enemy enemy)
    {
        _routineActive = true;
        _isPlaying = true;
        EnemyDataSO data = enemy.Data;
        EnemyLessonSO lesson = ResolveLesson(data);

        try
        {
            // Playtest 2026-09-17. The badge goes blank the moment the claim is made, NOT when the
            // halt begins. The introduced type is released above the play area and walks down
            // through the HUD band before it can be framed, and a readable badge across that
            // stretch lets the player draw its syllable and kill it before its card has ever run --
            // the "enemy introduction takes too long, player has already killed it" report.
            //
            // What this does and does NOT do: it withholds the INFORMATION needed to target this
            // enemy on purpose. CombatResolver matches on the character an enemy carries and has no
            // exclusion for introduction spawns, so a draw aimed at some other enemy that happens
            // to carry the same syllable still clears this one. Making the subject untargetable
            // outright would be a bigger change -- a wave can be waiting on it -- and is not this.
            //
            // Safe because the finally below calls Show() on EVERY exit path, including an abort
            // and a coroutine stopped by OnDisable, which redoes it explicitly. Without that an
            // introduction that never reached its reveal would leave an unkillable enemy walking.
            //
            // Only introduction spawns reach here (Enemy gates on _isIntroductionSpawn), so a
            // DeferAndSuppress spawn -- no card this time -- keeps its badge and stays killable.
            enemy.GlyphBadge?.Hide();

            // The spawner positions the enemy and assigns its glyph after Initialize returns, so a
            // card built on this frame's transform would frame the pool's parking position.
            if (_spawnSettleSeconds > 0f)
                yield return new WaitForSecondsRealtime(_spawnSettleSeconds);

            // A spawn can be gone before its own card starts — cleared by an already-queued draw, or
            // returned to the pool by a level that ended. Its introduction is already recorded, so it
            // simply does not play; the type's ability arms from its next spawn onward.
            if (!IsStillPresentable(enemy, data))
                yield break;

            // Step 0 — let it walk in. Settling only proves the spawner has finished POSITIONING the
            // enemy; on a first spawn that position is above the top of the frame. See the field
            // remarks on _onScreenWaitTimeoutSeconds.
            //
            // The condition is asked BEFORE the enumerator is built, not inside it: an enemy that is
            // already framed must cost this beat nothing at all, not even the frame a nested
            // coroutine spends completing. Beat 1's halt, vignette and time-scale drop are expected
            // to be in place the instant Initialize returns.
            if (NeedsToWalkIntoView(enemy))
            {
                yield return WaitUntilEnemyIsOnScreen(enemy, data);

                if (!IsStillPresentable(enemy, data))
                    yield break;
            }

            yield return lesson != null
                ? PlayLesson(enemy, data, lesson)
                : PlayCard(enemy, data);
        }
        finally
        {
            // Released on every exit path, including an abort before beat 2 ever ran: an ability
            // left holding would be inert for the rest of this spawn's life with nothing coming to
            // free it.
            HoldIntroducibleAbility(enemy, held: false);
            // Every exit path — normal, aborted mid-card, or the coroutine stopped by a disable —
            // must hand back the enemy's movement and the two globals. Half the field frozen at 0.15
            // is not a recoverable state for a player. The glyph badge is restored here too, on every
            // exit path, so an abort mid-lesson never leaves an enemy permanently unmarked.
            ReleaseTimeScale();
            LiftVignette();
            ReleaseEnemy(enemy);
            if (enemy != null) enemy.GlyphBadge?.Show();
            _isPlaying = false;
            _routineActive = false;
            RaiseRosterGateIfComplete();
            _lessonIsForcedReplay = false;
            _claimedEnemy = null;
            _routine = null;
        }
    }

    /// <summary>
    /// Holds until the introduced enemy is fully inside the camera's view, or until the timeout.
    ///
    /// <para>
    /// <b>The defect this closes.</b> The wave spawner releases enemies ABOVE the visible play area
    /// and lets them walk in. <c>_spawnSettleSeconds</c> waits for the spawner to finish positioning
    /// the enemy, which is a different fact: on Level 1 that position measured y = 11.40 against a
    /// camera that can see to y = 10.03. The beat then halted the enemy exactly there and played all
    /// eight of its beats against an empty field — the vignette dimmed nothing, the mirror copy
    /// appeared and was never seen, the glyph badge reveal was a twenty-pixel sliver clipped by the
    /// screen edge, and the card named an enemy the player had never laid eyes on.
    /// </para>
    ///
    /// <para>
    /// Walking in and then stopping is the read the lesson wants, so the beat waits rather than
    /// teleporting the enemy somewhere visible. The wait is on the camera's actual world rect, never
    /// a hardcoded y, so a level that moves or resizes its camera needs no change here. It is
    /// vertical only: enemies walk down a lane whose x is already inside the frame, and a horizontal
    /// condition could never be met by walking.
    /// </para>
    /// </summary>
    private bool NeedsToWalkIntoView(Enemy enemy)
    {
        if (_onScreenWaitTimeoutSeconds <= 0f)
            return false;

        Camera camera = _worldCamera != null ? _worldCamera : Camera.main;
        if (camera == null)
            return false;

        return !IsFramedForLesson(camera, ResolveEnemyWorldBounds(enemy), _onScreenMarginWorld,
            ResolveHudOcclusionRect(), _hudClearanceWorld, _haltLineViewportFromTop);
    }

    /// <summary>
    /// The rect the beat treats as the HUD's occluded band. Wired rect first, then the live clue
    /// presenter's own panel, then any presenter in the scene — resolved on demand rather than
    /// cached, because the beat is built by a wiring tool that runs before the HUD exists and a
    /// level reload replaces the presenter underneath it.
    /// </summary>
    private RectTransform ResolveHudOcclusionRect()
    {
        if (_hudOcclusionRect != null)
            return _hudOcclusionRect;

        ActiveCluePresenter presenter = ActiveCluePresenter.Active;
        if (presenter == null)
            presenter = FindFirstObjectByType<ActiveCluePresenter>(FindObjectsInactive.Include);

        return presenter != null ? presenter.CluePanelRect : null;
    }

    private IEnumerator WaitUntilEnemyIsOnScreen(Enemy enemy, EnemyDataSO data)
    {
        Camera camera = _worldCamera != null ? _worldCamera : Camera.main;
        if (camera == null || _onScreenWaitTimeoutSeconds <= 0f)
            yield break;

        float waited = 0f;
        while (waited < _onScreenWaitTimeoutSeconds)
        {
            if (!IsStillPresentable(enemy, data))
                yield break;

            if (IsFramedForLesson(camera, ResolveEnemyWorldBounds(enemy), _onScreenMarginWorld,
                    ResolveHudOcclusionRect(), _hudClearanceWorld, _haltLineViewportFromTop))
                yield break;

            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        DebugLogger.LogWarning(
            $"EnemyIntroductionBeat: '{DescribeEnemy(enemy)}' was still behind the HUD or outside "
            + $"the camera's view after {_onScreenWaitTimeoutSeconds:0.#}s, so the beat halts it "
            + "where it stands and the lesson may play against an occluded or empty field. Check "
            + "the wave spawn height against the camera's orthographic size, that the enemy is "
            + "actually walking, and that the clue panel's rect is where you think it is.");
    }

    /// <summary>
    /// Whether <paramref name="worldBounds"/> sits entirely between the camera's top and bottom
    /// edges with <paramref name="marginWorld"/> to spare. Static and public so the framing rule the
    /// lesson depends on is testable without a play session — which is precisely what 1221 green
    /// tests failed to catch.
    /// </summary>
    public static bool IsVerticallyInsideView(Camera camera, Bounds worldBounds, float marginWorld)
    {
        // No camera is not a reason to stall a lesson forever; the caller has nothing to frame
        // against and the beat proceeds exactly as it did before this step existed.
        if (camera == null || !TryGetCameraWorldRect(camera, out Rect view))
            return true;

        return worldBounds.max.y + marginWorld <= view.yMax
            && worldBounds.min.y - marginWorld >= view.yMin;
    }

    /// <summary>
    /// Whether the introduced enemy is framed for the lesson: inside the camera AND clear of the
    /// band the HUD draws over the top of the playfield.
    ///
    /// <para>
    /// <b>Why the camera rect alone was not enough, measured.</b> The previous fix halted the enemy
    /// the instant its bounds cleared <c>cameraTop - margin</c>, which parks it in the top 11-14 %
    /// of the screen — exactly the band <c>ActiveCluePanel</c> owns, and the clue panel is drawn in
    /// FRONT of the lane. At 1284x2778 that left roughly the top half of Iligaw showing above the
    /// panel; at the shipped 900x1604 portrait aspect it left a wedge of his head, and beat 2's
    /// before/after split frames were the same picture. The enemy was on camera and still invisible.
    /// </para>
    ///
    /// <para>
    /// The ceiling is derived from the panel's live <see cref="RectTransform"/> rather than from a
    /// hardcoded fraction of the screen, so it survives the safe-area inset (which pushes the HUD
    /// down on a notched device), a canvas-scaler match-mode change, and any aspect: at both aspects
    /// the check exercised the derived ceiling lands within a hundredth of a world unit of the same
    /// y, because the scaler ties the HUD to the world's constant width.
    /// </para>
    ///
    /// <para>
    /// A null or unresolvable panel falls back to the camera-top rule, which is the behaviour this
    /// extended — a lesson must never stall because the HUD could not be found.
    /// </para>
    /// </summary>
    public static bool IsFramedForLesson(
        Camera camera,
        Bounds worldBounds,
        float marginWorld,
        RectTransform hudOcclusionRect,
        float hudClearanceWorld)
    {
        return IsFramedForLesson(camera, worldBounds, marginWorld, hudOcclusionRect,
            hudClearanceWorld, haltLineViewportFromTop: 0f);
    }

    /// <summary>
    /// <see cref="IsFramedForLesson(Camera, Bounds, float, RectTransform, float)"/> with the halt
    /// line applied as well: the enemy must also have dropped below
    /// <paramref name="haltLineViewportFromTop"/> of the view. See
    /// <see cref="ResolveHaltCeilingWorldY(Camera, Rect, RectTransform, float, float)"/>.
    /// </summary>
    public static bool IsFramedForLesson(
        Camera camera,
        Bounds worldBounds,
        float marginWorld,
        RectTransform hudOcclusionRect,
        float hudClearanceWorld,
        float haltLineViewportFromTop)
    {
        if (camera == null || !TryGetCameraWorldRect(camera, out Rect view))
            return true;

        return worldBounds.max.y + marginWorld
                <= ResolveHaltCeilingWorldY(camera, view, hudOcclusionRect, hudClearanceWorld,
                    haltLineViewportFromTop)
            && worldBounds.min.y - marginWorld >= view.yMin;
    }

    /// <summary>
    /// The highest world y the introduced enemy's own bounds may reach and still be framed: the
    /// camera's top edge, or the bottom of the HUD's occluded band less
    /// <paramref name="hudClearanceWorld"/> when that is lower.
    ///
    /// <para>
    /// Clamped to stay above the camera's bottom edge. A HUD rect that somehow covers the whole
    /// screen would otherwise produce a ceiling below the floor, and the wait would spend its whole
    /// timeout on a condition that can never be met.
    /// </para>
    /// </summary>
    public static float ResolveHaltCeilingWorldY(
        Camera camera, Rect view, RectTransform hudOcclusionRect, float hudClearanceWorld)
    {
        return ResolveHaltCeilingWorldY(camera, view, hudOcclusionRect, hudClearanceWorld,
            haltLineViewportFromTop: 0f);
    }

    /// <summary>
    /// The halt ceiling with the authored halt line folded in: the HUD-derived ceiling above, or
    /// the line at <paramref name="haltLineViewportFromTop"/> of the view's height below the
    /// camera's top edge — whichever is lower. The HUD rule only guarantees the enemy is not
    /// behind the clue panel; the line is what puts it where the player is actually looking.
    /// A fraction of the view rather than a world y, so a camera that moves or resizes needs no
    /// retuning. Clamped so a line at the very bottom can never produce an unmeetable ceiling.
    /// </summary>
    public static float ResolveHaltCeilingWorldY(
        Camera camera, Rect view, RectTransform hudOcclusionRect, float hudClearanceWorld,
        float haltLineViewportFromTop)
    {
        float ceiling = view.yMax;
        if (TryGetHudBottomWorldY(camera, hudOcclusionRect, out float hudBottomWorldY))
            ceiling = hudBottomWorldY - Mathf.Max(0f, hudClearanceWorld);

        if (haltLineViewportFromTop > 0f)
        {
            float line = view.yMax - Mathf.Clamp01(haltLineViewportFromTop) * view.height;
            ceiling = Mathf.Min(ceiling, line);
        }

        return Mathf.Clamp(ceiling, view.yMin, view.yMax);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws the halt line in the Scene view while this object is selected: the y the introduced
    /// enemy's top must drop below (yellow), and the HUD-only ceiling it is lowering (grey), so
    /// the value can be tuned against the actual lane rather than guessed.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Camera camera = _worldCamera != null ? _worldCamera : Camera.main;
        if (camera == null || !TryGetCameraWorldRect(camera, out Rect view))
            return;

        RectTransform hud = _hudOcclusionRect;
        float hudCeiling = ResolveHaltCeilingWorldY(camera, view, hud, _hudClearanceWorld);
        float ceiling = ResolveHaltCeilingWorldY(camera, view, hud, _hudClearanceWorld,
            _haltLineViewportFromTop);

        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawLine(new Vector3(view.xMin, hudCeiling, 0f), new Vector3(view.xMax, hudCeiling, 0f));

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(view.xMin, ceiling, 0f), new Vector3(view.xMax, ceiling, 0f));
        // The margin band above the line: the enemy's top must clear this too.
        float withMargin = ceiling - _onScreenMarginWorld;
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.4f);
        Gizmos.DrawLine(new Vector3(view.xMin, withMargin, 0f), new Vector3(view.xMax, withMargin, 0f));
    }
#endif

    /// <summary>
    /// The world y of the lowest edge of a HUD rect, projected through the gameplay camera.
    ///
    /// <para>
    /// All four corners are measured rather than just the bottom two, so a rotated or flipped rect
    /// still reports the band it actually covers. The rect's own canvas decides the screen-space
    /// conversion: an overlay canvas converts with no camera, a screen-space-camera or world-space
    /// one with its own. Returns false — meaning "no HUD constraint" — for a null rect, a rect with
    /// no canvas-space extent, or a perspective gameplay camera.
    /// </para>
    /// </summary>
    public static bool TryGetHudBottomWorldY(
        Camera camera, RectTransform hudOcclusionRect, out float worldY)
    {
        worldY = 0f;
        if (camera == null || !camera.orthographic || hudOcclusionRect == null)
            return false;

        Canvas canvas = hudOcclusionRect.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector3[] corners = new Vector3[4];
        hudOcclusionRect.GetWorldCorners(corners);

        float minScreenY = float.PositiveInfinity;
        for (int i = 0; i < corners.Length; i++)
        {
            float screenY = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]).y;
            if (screenY < minScreenY)
                minScreenY = screenY;
        }

        if (float.IsInfinity(minScreenY) || float.IsNaN(minScreenY))
            return false;

        // Orthographic: only the screen y and the camera's pixel height feed the world y, so the
        // x passed here is irrelevant and a forced Camera.aspect cannot skew the answer.
        worldY = camera.ScreenToWorldPoint(new Vector3(0f, minScreenY, 0f)).y;
        return true;
    }

    /// <summary>
    /// The world rectangle an orthographic camera can see. Answers false for a perspective camera
    /// rather than guessing at a projection plane — every caller here treats that as "do not block".
    /// </summary>
    public static bool TryGetCameraWorldRect(Camera camera, out Rect worldRect)
    {
        worldRect = default;
        if (camera == null || !camera.orthographic)
            return false;

        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        Vector3 center = camera.transform.position;
        worldRect = Rect.MinMaxRect(
            center.x - halfWidth, center.y - halfHeight,
            center.x + halfWidth, center.y + halfHeight);
        return true;
    }

    /// <summary>
    /// The enemy's own on-screen extent: the union of every sprite it carries, <b>including
    /// inactive ones</b>. The glyph badge is deliberately hidden through beats 1-6 of a late-reveal
    /// lesson, and measuring only what is currently drawn would let the beat halt at a height where
    /// the badge has no room — which is exactly how beat 7's reveal became a sliver clipped by the
    /// top of the screen. Bounds are valid on a disabled renderer, so counting it costs nothing.
    /// </summary>
    private static Bounds ResolveEnemyWorldBounds(Enemy enemy)
    {
        if (enemy == null)
            return new Bounds(Vector3.zero, Vector3.one);

        SpriteRenderer[] sprites = enemy.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        Bounds? union = null;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
                continue;

            if (union.HasValue)
            {
                Bounds b = union.Value;
                b.Encapsulate(sprites[i].bounds);
                union = b;
            }
            else
            {
                union = sprites[i].bounds;
            }
        }

        return union ?? new Bounds(enemy.transform.position, Vector3.one);
    }

    /// <summary>
    /// Opens Level 1's slot-3 gate once every introducible type in the wave roster has been
    /// introduced. Called after each introduction rather than counted, so a level whose roster
    /// changes mid-development cannot leave a stale count holding the final slot shut.
    ///
    /// <para>
    /// This is only half of the gate. Introductions are campaign-wide and the gate registry resets
    /// per level attempt, so a replay introduces nothing and never reaches here — the other half is
    /// <c>SpawnAssignmentCoordinator.ApplyLevel</c>, which evaluates the same condition at level
    /// start. Both are needed; see <see cref="LevelRoster.TryOpenRosterGate"/>.
    /// </para>
    /// </summary>
    private static void RaiseRosterGateIfComplete()
    {
        SpawnAssignmentCoordinator coordinator = FindFirstObjectByType<SpawnAssignmentCoordinator>(
            FindObjectsInactive.Include);
        if (coordinator == null)
            return;

        LevelConfigSO level = GameManager.CurrentLevelConfig;
        LevelRoster.TryOpenRosterGate(
            level,
            data => CountsAsIntroducedThisAttempt(level, data),
            coordinator.OpenGate);
    }

    /// <summary>
    /// The four-step card: Halt, Name, Ability, Release. Unchanged from before the eight-beat
    /// lesson existed; a level with no authored <see cref="EnemyLessonSO"/> for this type still
    /// gets exactly this.
    /// </summary>
    private IEnumerator PlayCard(Enemy enemy, EnemyDataSO data)
    {
        // The previous type's residue goes before this one's card arrives: two banners describing
        // two different enemies is the one thing the banner's "attached to the thing it describes"
        // rule cannot survive.
        StopBanner();
        _card.PrepareCard(ResolveWalkSprite(data), data.displayName, data.discoverySubtitle);

        // Step 1 — Halt. The enemy stops where it stands, the vignette closes around it, and
        // time slows. The vignette is raised before the ramp rather than during it because its
        // own fade runs on scaled time: started under the slow, a quarter-second dim would take
        // most of the card to arrive.
        HaltEnemy(enemy);
        RaiseVignette(enemy);
        yield return RampTimeScale(Time.timeScale, _introductionTimeScale, _haltRampSeconds);
        yield return RampCard(0f, 1f, _haltRampSeconds);

        // Step 2 — Name. Walk sprite, display name, subtitle. Held long enough to be read, and
        // no longer: the field is still moving underneath.
        yield return WaitRealtime(_nameStepSeconds);

        // Step 3 — Ability. One line, stating what the enemy does. The player derives the
        // counter; see EnemyDataSO.abilityLine for why the copy may never state it.
        _card.ShowAbilityLine(data.abilityLine);
        yield return WaitRealtime(_abilityStepSeconds);

        // Step 3b — Glyph. The badge comes back while the enemy is still spotlit, so the symbol the
        // player will have to draw is read HERE, against a named and explained enemy, rather than
        // discovered on a shape already walking away. Mirrors the lesson's beat 7.
        enemy.GlyphBadge?.Show();
        yield return WaitRealtime(_glyphRevealStepSeconds);

        // Step 3c — Hold. The card has said everything it is going to say; it now waits for the
        // player rather than for a clock. The field is frozen outright for the hold, which is what
        // makes taking input here legitimate at all: the card view's standing rule is that the
        // player can draw straight through it, and that rule exists because the beat used to leave
        // the field live. Nothing to draw through while it is stopped.
        //
        // The poll lives here and not in the view so the card stays raycast-transparent, and so an
        // injected TouchState reaches it -- uGUI buttons do not receive those.
        yield return HoldForContinue();

        // Step 4 — Release. Card out, vignette lifts, time ramps back, the enemy walks again.
        yield return RampCard(1f, 0f, _releaseRampSeconds);
        _card.HideCardImmediate();
        LiftVignette();
        yield return RampTimeScale(Time.timeScale, _restoreTimeScale, _releaseRampSeconds);
        ReleaseTimeScale();
        ReleaseEnemy(enemy);

        // The banner is the card's residue: one line that stays while the introduced enemy is on
        // the field and goes with it, so the reminder is attached to the thing it describes
        // rather than to a stretch of time. The card is finished, so the beat stops claiming the
        // screen here — see IsPlaying — and the banner's wait is handed to its own coroutine so the
        // run can end and the next type's card is not refused behind it.
        _isPlaying = false;
        ShowBannerForLifetime(enemy, data);
    }

    /// <summary>
    /// The nine-beat lesson: 1 Appear, 2 the glyph rule, 3 Ability, 4 React, 5 the ability rule,
    /// 6 Name and ability line, 7 Glyph reveal, 8 Draw, 9 Restoration. Beats 1 and 6 are the
    /// card's own steps; the rest are the lesson's.
    ///
    /// <para>
    /// <b>The teaching order is universal-then-exception, and beat 2's position is the point.</b>
    /// The player is told every enemy carries a mark BEFORE this one does anything, so the split
    /// in beat 3 reads as "and this one also has a trick" rather than as the first thing they
    /// learn. Moving beat 2 after the ability puts the exception first and the rule second, which
    /// is the order the lesson was reordered away from.
    /// </para>
    ///
    /// <para>
    /// Every wait is realtime, like the card's, because the beat holds Time.timeScale down and a
    /// scaled wait would stretch a two-second beat past thirteen.
    /// </para>
    /// </summary>
    private IEnumerator PlayLesson(Enemy enemy, EnemyDataSO data, EnemyLessonSO lesson)
    {
        StopBanner();
        _card.PrepareCard(ResolveWalkSprite(data), data.displayName, data.discoverySubtitle);

        // Beat 7 is a reveal, so the badge stays dark until then. PlayIntroduction now hides it
        // for every introduction spawn, so a lesson that does NOT defer its reveal has to put the
        // badge back here -- otherwise the walk-down blanking would silently become a lesson-long
        // one and beats 1-6 would play against an unreadable enemy.
        if (lesson.revealGlyphLate)
            enemy.GlyphBadge?.Hide();
        else
            enemy.GlyphBadge?.Show();

        // Beat 1 — Appear. Halt and vignette, with NO card yet: the player must watch the
        // ability land on an enemy they cannot yet read anything about.
        HaltEnemy(enemy);
        RaiseVignette(enemy);
        yield return RampTimeScale(Time.timeScale, _introductionTimeScale, _haltRampSeconds);

        // The two universal rules — beat 2's "every enemy carries a mark" and beats 4-5's "each
        // also has its own trick" — share one gate, and it is evaluated ONCE, here, before either
        // can run. They are halves of the same once-per-campaign teaching moment and they are
        // authored on the same asset; splitting the question in two would let a lesson aborted
        // between beat 2 and beat 5 replay only half of it. The gate is read before
        // MarkAbilityRuleSeen below writes it, so a single read is also the only honest one.
        bool playsTheUniversalRules =
            _lessonIsForcedReplay || !EnemyIntroductionProgress.HasSeenAbilityRule();

        // Beat 2 — the glyph rule. The UNIVERSAL mechanic, taught before the exception: every
        // enemy carries a mark, stated while the field is halted and BEFORE the ability fires.
        //
        // This enemy's own badge is deliberately still hidden (revealGlyphLate, above) and must
        // stay hidden until beat 7 — rule first, instance later, so the reveal pays off a promise
        // the player has already been given rather than arriving unannounced. The line is played
        // to completion here, so it has landed and cleared before PlayAbilityBeat releases the
        // ability hold; the player is never reading about marks while the split is happening.
        if (playsTheUniversalRules)
        {
            yield return OnboardingDialogueRunner.Play(
                ResolveDialogueController(), lesson.glyphRuleLine);
        }

        // Beat 3 — Ability. The ability is armed by IntroduceAndArm; this waits for it to actually
        // FIRE and then holds so the player can watch what it did — on Level 1, one Iligaw becoming
        // two. See PlayAbilityBeat for why a fixed hold is not enough.
        yield return PlayAbilityBeat(enemy, lesson);

        // Beats 4 and 5 — React, then the ability rule. Once per campaign, EXCEPT on a forced
        // replay of this lesson: they are beats of it, and a lesson that replays with beats
        // silently missing is worse than one that repeats them. The player who triggered this
        // replay asked for the level's tutorial again, not for a lesson with holes in it.
        // On every other level the rule stays once-per-campaign, so a later level introducing a
        // new type still does not re-teach "enemies have abilities".
        if (playsTheUniversalRules)
        {
            DialogueController dialogue = ResolveDialogueController();
            yield return OnboardingDialogueRunner.Play(dialogue, lesson.reactLine);
            yield return OnboardingDialogueRunner.Play(dialogue, lesson.ruleLine);
            EnemyIntroductionProgress.MarkAbilityRuleSeen();
        }

        // Beats 5 and 6 — Name, then ability line. The card's own steps, in its own order.
        yield return RampCard(0f, 1f, _haltRampSeconds);
        yield return WaitRealtime(_nameStepSeconds);
        _card.ShowAbilityLine(data.abilityLine);
        yield return WaitRealtime(_abilityStepSeconds);
        yield return RampCard(1f, 0f, _releaseRampSeconds);
        _card.HideCardImmediate();

        // Beat 7 — Glyph. The badge comes up while the enemy is still spotlit and time is still
        // slow, so the reveal is the only thing moving on screen.
        enemy.GlyphBadge?.Show();
        yield return WaitRealtime(_nameStepSeconds);

        // Beat 8 — Draw. Time comes back first: the draw is real combat against a real enemy, not
        // a frozen exercise.
        //
        // The enemy's MOVEMENT deliberately does NOT come back with it. Released here, the lesson's
        // own subject walks the length of the field while the player reads the prompt and finds the
        // stroke — measured at y = -4.5, just above the fence, on a nineteen-second first read — so
        // the one enemy the lesson promised to teach against was the one most likely to reach the
        // shrine. Iligaw's mirror copy is slaved to this transform every Update, so holding the
        // original holds the pair. Movement is handed back after beat 9, below.
        LiftVignette();
        yield return RampTimeScale(Time.timeScale, _restoreTimeScale, _releaseRampSeconds);
        ReleaseTimeScale();

        if (lesson.drawStep != null)
        {
            yield return PlayDrawStep(enemy, lesson.drawStep);

            // Beat 8's subject left without being drawn — it reached the shrine, was cleared by
            // something else, or its pooled shell was recycled. Beat 9 would announce a restoration
            // that never happened, so the lesson ends here instead. The finally block below hands
            // back the time scale, the vignette and the spawn hold; leaving that to a wait that can
            // no longer be satisfied is what left one run sitting dead for 135 seconds.
            if (_drawStepAbandoned)
                yield break;
        }

        ReleaseEnemy(enemy);

        // Beats 9 and 10 — Restoration. The draw killed the enemy and the enemy's syllable went
        // into the blank; that link is the one thing the eight beats never say out loud. Played
        // after beat 8 rather than inside it so the player watches the clue change first and is
        // then told what they just watched. OnboardingDialogueRunner no-ops on blank copy, so a
        // lesson that leaves this unauthored ends exactly as it did before.
        yield return OnboardingDialogueRunner.Play(
            ResolveDialogueController(), lesson.restorationLine);

        // Beat 9 was the last one. Everything below is the banner's own lifetime, which is tied to
        // the enemy and not to the lesson, so the lesson stops claiming the screen here. Leaving the
        // flag up through the banner is what pinned an ability line on screen for seventeen seconds
        // after the player had visibly finished the lesson.
        _isPlaying = false;
        ShowBannerForLifetime(enemy, data);
    }

    /// <summary>
    /// Raises the residue banner and lets it live for exactly as long as the introduced enemy does,
    /// on a coroutine of its own so the run that raised it can finish.
    ///
    /// <para>
    /// One banner at a time: a second introduction replaces the first's, because the banner names
    /// the enemy it is attached to and two of them would contradict each other. Nothing here touches
    /// <c>Time.timeScale</c>, the vignette or the card group — the run released all three before the
    /// last beat ended — so this can safely outlive the run without fighting it for global state.
    /// </para>
    /// </summary>
    private void ShowBannerForLifetime(Enemy enemy, EnemyDataSO data)
    {
        StopBanner();
        _bannerRoutine = StartCoroutine(BannerLifetime(enemy, data));
    }

    private IEnumerator BannerLifetime(Enemy enemy, EnemyDataSO data)
    {
        _card.ShowBanner(data.abilityLine);
        yield return WaitWhileEnemyLives(enemy, data);
        _card.HideBanner();
        _bannerRoutine = null;
    }

    /// <summary>Takes a standing banner down. Safe to call with none up.</summary>
    private void StopBanner()
    {
        if (_bannerRoutine != null)
        {
            StopCoroutine(_bannerRoutine);
            _bannerRoutine = null;
        }

        if (_card != null)
            _card.HideBanner();
    }

    /// <summary>
    /// Beat 2. Waits for the introduced enemy's ability to actually fire, then holds
    /// <see cref="EnemyLessonSO.abilityBeatSeconds"/> so the change it made is watchable.
    ///
    /// <para>
    /// <b>Why a fixed hold cannot do this job, and why the two shipped abilities need it for
    /// opposite reasons.</b> Every wait in this beat is REALTIME, and beat 1 has just pulled
    /// <c>Time.timeScale</c> down to <see cref="_introductionTimeScale"/> (0.15 on Level 1).
    /// <c>AshFirstSlotController._armDelaySeconds</c> (1.5s) accrues on SCALED
    /// <c>Time.deltaTime</c> in <c>AshFirstSlotController.Tick</c>, deliberately, so a fixed
    /// realtime hold buys it almost no scaled time and the gust lands four to six realtime seconds
    /// LATE — during beats 5-6, with the card already up. <see cref="MirrorDecoyController"/>, which
    /// is what Level 1's shipped lesson actually waits on, has the opposite shape: it has no delay
    /// at all and places the copy on its first <c>Update</c>, so a fixed hold long enough for the
    /// ash would leave the split sitting on a frozen screen for seconds after the player has already
    /// read it. Waiting on the FACT and then holding
    /// <see cref="EnemyLessonSO.abilityBeatSeconds"/> from that moment is the one rule that paces
    /// both: the hold always measures time the player has actually had to look at the change.
    /// </para>
    ///
    /// <para>
    /// Waiting on the fact rather than on a duration is also robust to either number being retuned.
    /// The realtime timeout is the safety valve: an ability whose own trigger conditions go unmet on
    /// this spawn must not hang the lesson, so the beat gives up and proceeds exactly as the fixed
    /// hold used to. A dependency that is missing outright is caught earlier and never reaches the
    /// wait — see <see cref="IIntroducibleAbility.CanFireThisSpawn"/>.
    /// </para>
    ///
    /// <para>
    /// <b>The question is asked through <see cref="IIntroducibleAbility"/>, not of a named type.</b>
    /// This used to reach for <see cref="AshFirstSlotController"/> by concrete type, which meant any
    /// lesson authored on a different enemy — Iligaw's copy is a <see cref="MirrorDecoyController"/>
    /// — silently took the early return and became the fixed hold this wait exists to replace.
    /// </para>
    /// </summary>
    private IEnumerator PlayAbilityBeat(Enemy enemy, EnemyLessonSO lesson)
    {
        // Beat 2 starts by letting the ability go. Held since the claim, so that whatever it does
        // happens NOW — on a halted, dimmed, on-screen field with the player watching — rather than
        // on the spawn frame, off-camera, before beat 1 had even faded in. An ability that does not
        // implement the hold is unaffected and simply fires whenever it always did.
        HoldIntroducibleAbility(enemy, held: false);

        // A lesson that does not arm the ability has nothing to wait for. The standing suppression
        // rule keeps the ability inert for this whole spawn, so HasFiredThisSpawn can never become
        // true and the wait would spend the entire timeout — fifteen seconds of a dimmed, halted
        // field with no card up — before continuing anyway.
        if (!lesson.armAbilityOnIntroduction)
        {
            yield return WaitRealtime(lesson.abilityBeatSeconds);
            yield break;
        }

        IIntroducibleAbility ability = ResolveIntroducibleAbility(enemy);

        // No ability that COULD fire on this spawn: fall back to the authored hold immediately,
        // which is what the beat did before the wait existed.
        //
        // CanFireThisSpawn is checked here, up front, rather than being left to the wait below.
        // A missing structural dependency — Iligaw's copy comes from EnemyPool, and a level with no
        // pool has nowhere to get one — does not arrive part-way through a beat, so waiting on it
        // would spend the entire _abilityBeatArmTimeoutSeconds behind a dimmed, halted field with no
        // card up and then continue anyway. Asking before the loop turns that into a fast failure.
        // Worth saying out loud in every case, because the lesson asked for the ability to be armed
        // and there is nothing here that can fire.
        if (ability == null || ability.IsSuppressedForIntroductionSpawn || !ability.CanFireThisSpawn)
        {
            DebugLogger.LogWarning(
                $"EnemyIntroductionBeat: beat 2 found no armed IIntroducibleAbility on "
                + $"'{DescribeEnemy(enemy)}', whose lesson sets armAbilityOnIntroduction. The beat "
                + "falls back to a fixed hold, so the lesson may name the enemy before the player "
                + "has seen it do anything. Check that its signature ability component implements "
                + "IIntroducibleAbility, is enabled on this spawn, and has everything it needs to "
                + "fire (a mirror decoy needs an EnemyPool to take its copy from).");
            yield return WaitRealtime(lesson.abilityBeatSeconds);
            yield break;
        }

        float waited = 0f;
        while (!ability.HasFiredThisSpawn && waited < _abilityBeatArmTimeoutSeconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!ability.HasFiredThisSpawn)
        {
            DebugLogger.LogWarning(
                "EnemyIntroductionBeat: beat 2 timed out after "
                + $"{_abilityBeatArmTimeoutSeconds:0.#}s waiting for "
                + $"'{DescribeEnemy(enemy)}' to use its ability, so the lesson names the enemy "
                + "before the player has seen it do anything. Check "
                + $"{ability.GetType().Name}'s firing trigger for this spawn.");
        }

        // The hold is measured from the ability firing, not from the start of the beat: its job is
        // to let the player read the change, and there is nothing to read before it happens.
        yield return WaitRealtime(lesson.abilityBeatSeconds);
    }

    /// <summary>
    /// This spawn's signature ability, if it has one that is live. Enabled-ness is checked through
    /// <see cref="Behaviour"/> rather than being put on the interface, so implementing it costs a
    /// component nothing beyond the two facts beat 2 actually reads.
    /// </summary>
    private static IIntroducibleAbility ResolveIntroducibleAbility(Enemy enemy)
    {
        if (enemy == null)
            return null;

        IIntroducibleAbility[] candidates = enemy.GetComponents<IIntroducibleAbility>();
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                continue;

            return candidates[i];
        }

        return null;
    }

    private static string DescribeEnemy(Enemy enemy) =>
        enemy != null && enemy.Data != null ? enemy.Data.displayName : "?";

    /// <summary>
    /// Beat 8. Reuses the surviving Level1TutorialStepSO guide machinery against a live enemy.
    /// Input is never locked — the beat's standing promise — so a player who has already started a
    /// stroke can finish it.
    /// </summary>
    private IEnumerator PlayDrawStep(Enemy enemy, Level1TutorialStepSO step)
    {
        _drawStepAbandoned = false;

        Level1TutorialGuideUI guide = FindFirstObjectByType<Level1TutorialGuideUI>(
            FindObjectsInactive.Include);

        if (guide != null)
            guide.ShowMessage(step.promptText, canSkip: false);

        string expectedID = step.targetCharacter != null ? step.targetCharacter.characterID : null;
        if (string.IsNullOrEmpty(expectedID))
        {
            if (guide != null) guide.Hide();
            yield break;
        }

        // The identity beat 8 is waiting on, captured before the wait. SpawnSequence as well as
        // liveness, matching WaitWhileEnemyLives: a pooled shell recycled into another type during
        // the wait is a different enemy wearing the same reference.
        long spawnSequence = enemy != null ? enemy.SpawnSequence : 0L;
        EnemyDataSO subjectData = enemy != null ? enemy.Data : null;
        bool SubjectIsGone() =>
            enemy == null
            || enemy.SpawnSequence != spawnSequence
            || enemy.Data != subjectData
            || !enemy.gameObject.activeInHierarchy
            || enemy.IsDying;

        System.Action<RecognitionResult, bool, float> feedback = null;
        if (guide != null)
        {
            feedback = (result, passed, _) =>
            {
                if (passed && string.Equals(result.characterID, expectedID,
                        System.StringComparison.OrdinalIgnoreCase))
                    return;

                guide.ShowFeedback(passed
                    ? step.wrongCharacterFeedback
                    : step.recognitionFailedFeedback);
            };
            EventBus.OnRecognitionResolved += feedback;
        }

        try
        {
            bool drawn = false;
            yield return TutorialDrawWait.WaitForCorrectDrawOrAbandon(
                expectedID, SubjectIsGone, landed => drawn = landed);

            // The subject left without being drawn. Nothing here can be satisfied any more, so the
            // beat says so and unwinds rather than holding the wave schedule open behind a prompt
            // the player can no longer act on.
            if (!drawn)
            {
                _drawStepAbandoned = true;
                DebugLogger.LogWarning(
                    $"EnemyIntroductionBeat: beat 8's subject '{DescribeEnemy(enemy)}' left the "
                    + "field before the player drew its glyph, so the lesson ends without beat 9. "
                    + "The spawn hold is released here; if this happens every run, check that the "
                    + "beat is holding the introduced enemy still for the whole lesson.");
                yield break;
            }

            // The draw landed. The wrong-draw handler comes off FIRST, or a stray recognition
            // resolved during the hold would overwrite the success copy with a correction the
            // player did not earn.
            if (feedback != null)
            {
                EventBus.OnRecognitionResolved -= feedback;
                feedback = null;
            }

            // The instruction the player has just obeyed goes now, not when the guide closes.
            // Left up, "Draw E/I. Follow the guide." sat under its own congratulation for the
            // length of the success hold — about a second and a half of being told to do the thing
            // they had already done.
            if (guide != null)
                guide.ClearPrompt();

            // The authored success copy was previously never shown at all: the prompt simply
            // vanished, with nothing to tell the player the draw was the one being asked for.
            if (guide != null && !string.IsNullOrWhiteSpace(step.successText))
            {
                guide.ShowFeedback(step.successText);
                yield return WaitRealtime(_drawSuccessHoldSeconds);
            }
        }
        finally
        {
            if (feedback != null)
                EventBus.OnRecognitionResolved -= feedback;
            if (guide != null)
                guide.Hide();
        }
    }

    /// <summary>
    /// The scene's dialogue controller. Resolved on demand rather than serialized because this
    /// beat is created by the wiring tool before the HUD it will need exists.
    /// OnboardingDialogueRunner.Play already no-ops on a null controller and on blank copy, which
    /// is what lets beats 3 and 4 ship before their Filipino copy is authored.
    /// </summary>
    private DialogueController ResolveDialogueController()
    {
        if (_dialogue == null)
            _dialogue = FindFirstObjectByType<DialogueController>(FindObjectsInactive.Include);

        return _dialogue;
    }

    private DialogueController _dialogue;

    /// <summary>
    /// Holds the beat open for exactly as long as the introduced enemy is on the field. Checked by
    /// identity as well as liveness: a pooled shell recycled into a different type is not the enemy
    /// the banner is describing, and the spawn sequence is what distinguishes them.
    /// </summary>
    private static IEnumerator WaitWhileEnemyLives(Enemy enemy, EnemyDataSO data)
    {
        long spawnSequence = enemy != null ? enemy.SpawnSequence : 0L;
        while (enemy != null
            && enemy.SpawnSequence == spawnSequence
            && enemy.Data == data
            && enemy.gameObject.activeInHierarchy
            && !enemy.IsDying)
        {
            yield return null;
        }
    }

    private static bool IsStillPresentable(Enemy enemy, EnemyDataSO data)
    {
        return enemy != null
            && data != null
            && enemy.Data == data
            && enemy.gameObject.activeInHierarchy
            && !enemy.IsDying;
    }

    /// <summary>
    /// The card's portrait: the enemy's first walk frame, which is what the player is about to see
    /// walking. The authored almanac portrait is deliberately not used — the card's job is to let a
    /// player match a silhouette on the field, and a stylised portrait is a different image.
    /// </summary>
    private static Sprite ResolveWalkSprite(EnemyDataSO data)
    {
        if (data.walkFrames != null && data.walkFrames.Length > 0 && data.walkFrames[0] != null)
            return data.walkFrames[0];

        return data.portraitSprite;
    }

    /// <summary>
    /// Stops the new enemy where it stands. <c>SetExternallyMoving(false)</c> as well as
    /// <c>Stop()</c>, because the walk animation is driven from the mover's IsMoving: a halted enemy
    /// still cycling its walk frames would read as walking on the spot.
    /// </summary>
    private static void HaltEnemy(Enemy enemy)
    {
        EnemyMover mover = enemy != null ? enemy.GetComponent<EnemyMover>() : null;
        if (mover == null)
            return;

        mover.SetExternallyMoving(false);
        mover.Stop();
    }

    /// <summary>
    /// Hands movement back. Re-pushes <see cref="Enemy.EffectiveSpeed"/> rather than a remembered
    /// value, so any speed buff that arrived while the enemy was halted is honoured on release.
    /// </summary>
    private static void ReleaseEnemy(Enemy enemy)
    {
        if (enemy == null || enemy.IsDying)
            return;

        EnemyMover mover = enemy.GetComponent<EnemyMover>();
        if (mover != null)
            mover.SetSpeed(enemy.EffectiveSpeed);
    }

    private void RaiseVignette(Enemy enemy)
    {
        TutorialSpotlightOverlay overlay = ResolveVignette();
        if (overlay == null || enemy == null)
            return;

        overlay.SetCamera(_worldCamera != null ? _worldCamera : Camera.main);
        overlay.Show(enemy.transform, _vignettePaddingWorld);
    }

    private void LiftVignette()
    {
        if (_vignette != null)
            _vignette.Hide();

        if (_runtimeVignette != null && _runtimeVignette != _vignette)
            _runtimeVignette.Hide();
    }

    /// <summary>
    /// The scene's dim if one was wired, otherwise one ADOPTED from the scene, and only failing
    /// both, one created on first use and kept. Created lazily rather than on wake so a level that
    /// never introduces a new type pays nothing, and cached so four introductions in one level do
    /// not stack four full-screen canvases.
    ///
    /// <para>
    /// <b>Why adoption, and not just creation.</b> This beat is wired by a tool that does not always
    /// have the HUD's overlay to hand, so <c>_vignette</c> is routinely null on a scene that
    /// nonetheless already contains a perfectly good <see cref="TutorialSpotlightOverlay"/> — the
    /// onboarding beats use one for the base intro and the heart-loss demo. Creating a second gave
    /// Level 1 two live full-screen dims with different panel layouts fighting over the same screen,
    /// each convinced it owned it. One overlay per scene is the invariant; this is where it is kept.
    /// </para>
    /// </summary>
    private TutorialSpotlightOverlay ResolveVignette()
    {
        if (_vignette != null)
            return _vignette;

        if (_runtimeVignette == null)
            _runtimeVignette = FindFirstObjectByType<TutorialSpotlightOverlay>(
                FindObjectsInactive.Include);

        if (_runtimeVignette == null)
            _runtimeVignette = TutorialSpotlightOverlay.CreateRuntime();

        return _runtimeVignette;
    }

    private IEnumerator RampCard(float from, float to, float seconds)
    {
        if (seconds <= 0f)
        {
            _card.SetCardProgress(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _card.SetCardProgress(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds)));
            yield return null;
        }

        _card.SetCardProgress(to);
    }

    /// <summary>
    /// Moves <c>Time.timeScale</c> between two values over a wall-clock duration. The value the beat
    /// found on entry is remembered on the first ramp and is what the release ramps back to, rather
    /// than a hardcoded 1: a level that is already running slowed for its own reasons should be given
    /// back what it had.
    /// </summary>
    /// <summary>
    /// Test seam for the hold. A PlayMode fixture has no player, so a card driven to completion
    /// would wait for a press that never arrives and then fail on a card that is still up — which
    /// is exactly how BannerStandingAfterACard_StillLetsTheNextTypeIntroduceItself reported it.
    /// Mirrors <c>LevelFlowController.SetSkipReadyScreenForTests</c>.
    ///
    /// <para>
    /// Deliberately NOT set by every introduction fixture: ContinueHoldTests leaves it off and
    /// drives real input, so the gate is covered rather than skipped everywhere it appears.
    /// </para>
    /// </summary>
    private static bool s_skipContinueHoldForTests;

    /// <summary>Sets the hold seam. Fixtures that set it must reset it in teardown.</summary>
    public static void SetSkipContinueHoldForTests(bool skip) => s_skipContinueHoldForTests = skip;

    /// <summary>
    /// Stands in for the player's tap, so a fixture can let the hold actually happen and then end
    /// it. Consumed once by the waiting card.
    /// </summary>
    /// <remarks>
    /// This exists because the PlayMode test assembly does not reference the Input System — it sets
    /// <c>overrideReferences</c> and lists its precompiled assemblies explicitly — so a fixture
    /// cannot queue a real touch without reshaping the build config.
    ///
    /// <para>
    /// <b>What that leaves uncovered:</b> the device bindings in
    /// <see cref="ContinuePressedThisFrame"/>. A test using this proves the card holds, freezes the
    /// field and releases on a continue — not that a finger on a phone produces one. That last step
    /// is a play session.
    /// </para>
    /// </remarks>
    public static void RequestContinueForTests() => s_continueRequestedByTest = true;

    private static bool s_continueRequestedByTest;

    /// <summary>True while a card is holding for the player's tap. Diagnostic and test seam.</summary>
    public static bool IsHoldingForContinue { get; private set; }

    /// <summary>
    /// Freezes the field and holds the card until the player taps.
    /// </summary>
    /// <remarks>
    /// Time is set to 0 rather than left at <c>_introductionTimeScale</c> because an indefinite
    /// hold at 0.15 is still a field creeping toward the shrine while the player reads. The ramp in
    /// step 4 restores from whatever it finds, so starting that ramp from 0 needs no special case.
    ///
    /// <para>
    /// Input is polled through the Input System directly. The card view cannot take it: every
    /// graphic there is forced raycast-transparent on wake so the player can draw through the card,
    /// and a full-screen catcher would undo that for the one spawn that can never be retried.
    /// </para>
    /// </remarks>
    private IEnumerator HoldForContinue()
    {
        if (s_skipContinueHoldForTests)
            yield break;

        Time.timeScale = 0f;
        _card.ShowContinuePrompt();
        IsHoldingForContinue = true;

        s_continueRequestedByTest = false;
        float waited = 0f;
        while (!ContinuePressedThisFrame() && !s_continueRequestedByTest)
        {
            if (_continueHoldTimeoutSeconds > 0f && waited >= _continueHoldTimeoutSeconds)
            {
                DebugLogger.LogWarning(
                    "EnemyIntroductionBeat: no tap arrived within "
                    + $"{_continueHoldTimeoutSeconds:0.#}s, so the card releases itself.");
                break;
            }

            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        s_continueRequestedByTest = false;
        IsHoldingForContinue = false;
        _card.HideContinuePrompt();
    }

    /// <summary>
    /// A press on any device the player could plausibly be holding. Deliberately not a uGUI button:
    /// see <see cref="HoldForContinue"/>.
    /// </summary>
    private static bool ContinuePressedThisFrame()
    {
        UnityEngine.InputSystem.Touchscreen touch = UnityEngine.InputSystem.Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            return true;

        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;

        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard != null
            && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
    }

    private IEnumerator RampTimeScale(float from, float to, float seconds)
    {
        if (!_timeScaleTaken)
        {
            _restoreTimeScale = from;
            _timeScaleTaken = true;
        }

        if (seconds <= 0f)
        {
            Time.timeScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }

        Time.timeScale = to;
    }

    private void ReleaseTimeScale()
    {
        if (!_timeScaleTaken)
            return;

        Time.timeScale = _restoreTimeScale;
        _timeScaleTaken = false;
    }

    private void StopPlayback()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        // The banner outlives the run on its own coroutine now, so stopping the run no longer stops
        // it. A scene unload that left it running would strand a line describing an enemy that no
        // longer exists on a card the next level is about to reuse.
        StopBanner();

        _isPlaying = false;
        _routineActive = false;
        _lessonIsForcedReplay = false;
        _claimedEnemy = null;
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        yield return new WaitForSecondsRealtime(seconds);
    }
}
