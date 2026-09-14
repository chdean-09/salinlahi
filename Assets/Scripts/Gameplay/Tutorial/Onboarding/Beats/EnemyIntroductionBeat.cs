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

    [Tooltip("Seconds (wall-clock) waited after the enemy is positioned before the halt begins, so the card never lands on a shell still parked at its off-screen pool position.")]
    [SerializeField] private float _spawnSettleSeconds = 0.1f;

    [Header("Step 0 — Walk into frame")]
    [Tooltip("Seconds (wall-clock) the beat will let the introduced enemy keep walking until it is "
             + "fully inside the camera's view before halting it. The spawner releases enemies ABOVE "
             + "the visible play area, so halting on the settle frame alone freezes the subject "
             + "off-screen and the whole lesson plays against an empty field. Safety valve only: on "
             + "timeout the beat halts where the enemy stands and warns, rather than hanging.")]
    [SerializeField, Min(0f)] private float _onScreenWaitTimeoutSeconds = 6f;

    [Tooltip("World units of clearance required between the introduced enemy's own bounds (body AND "
             + "glyph badge, counted even while the badge is hidden for a late reveal) and the edge "
             + "of the camera's view before the halt begins. Buys the vignette and the beat-7 badge "
             + "reveal room to render without clipping the screen edge.")]
    [SerializeField, Min(0f)] private float _onScreenMarginWorld = 0.35f;

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

    private Enemy _claimedEnemy;
    private Coroutine _routine;
    private bool _isPlaying;
    private bool _routineActive;
    private bool _timeScaleTaken;
    private float _restoreTimeScale = 1f;
    private TutorialSpotlightOverlay _runtimeVignette;

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
    /// ends; <see cref="_routineActive"/> is what keeps a second introduction from starting
    /// underneath the banner.
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

            if (!EnemyIntroductionProgress.HasBeenIntroduced(lesson.enemy))
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
        // _routineActive, not _isPlaying: the run keeps a banner up after its last beat, and a
        // second introduction claimed under that banner would share Time.timeScale and the dim with
        // a coroutine that is still going to restore both on its way out.
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

        if (!EnemyIntroductionProgress.TryClaimIntroduction(data))
            return false;

        _claimedEnemy = enemy;
        return true;
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

        return !IsVerticallyInsideView(camera, ResolveEnemyWorldBounds(enemy), _onScreenMarginWorld);
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

            if (IsVerticallyInsideView(camera, ResolveEnemyWorldBounds(enemy), _onScreenMarginWorld))
                yield break;

            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        DebugLogger.LogWarning(
            $"EnemyIntroductionBeat: '{DescribeEnemy(enemy)}' was still outside the camera's view "
            + $"after {_onScreenWaitTimeoutSeconds:0.#}s, so the beat halts it where it stands and "
            + "the lesson may play against an empty field. Check the wave spawn height against the "
            + "camera's orthographic size, and that the enemy is actually walking.");
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

        LevelRoster.TryOpenRosterGate(
            GameManager.CurrentLevelConfig,
            EnemyIntroductionProgress.HasBeenIntroduced,
            coordinator.OpenGate);
    }

    /// <summary>
    /// The four-step card: Halt, Name, Ability, Release. Unchanged from before the eight-beat
    /// lesson existed; a level with no authored <see cref="EnemyLessonSO"/> for this type still
    /// gets exactly this.
    /// </summary>
    private IEnumerator PlayCard(Enemy enemy, EnemyDataSO data)
    {
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
        // screen before this wait — see IsPlaying.
        _isPlaying = false;
        _card.ShowBanner(data.abilityLine);
        yield return WaitWhileEnemyLives(enemy, data);
        _card.HideBanner();
    }

    /// <summary>
    /// The eight-beat lesson. Beats 1, 5 and 6 are the card's own steps; 2, 3, 4, 7 and 8 are the
    /// lesson's. Every wait is realtime, like the card's, because the beat holds Time.timeScale
    /// down and a scaled wait would stretch a two-second beat past thirteen.
    /// </summary>
    private IEnumerator PlayLesson(Enemy enemy, EnemyDataSO data, EnemyLessonSO lesson)
    {
        _card.PrepareCard(ResolveWalkSprite(data), data.displayName, data.discoverySubtitle);

        // Beat 7 is a reveal, so the badge goes dark before the player ever sees it.
        if (lesson.revealGlyphLate)
            enemy.GlyphBadge?.Hide();

        // Beat 1 — Appear. Halt and vignette, with NO card yet: the player must watch the
        // ability land on an enemy they cannot yet read anything about.
        HaltEnemy(enemy);
        RaiseVignette(enemy);
        yield return RampTimeScale(Time.timeScale, _introductionTimeScale, _haltRampSeconds);

        // Beat 2 — Ability. The ability is armed by IntroduceAndArm; this waits for it to actually
        // FIRE and then holds so the player can watch what it did — on Level 1, one Iligaw becoming
        // two. See PlayAbilityBeat for why a fixed hold is not enough.
        yield return PlayAbilityBeat(enemy, lesson);

        // Beats 3 and 4 — React, then the rule. Once per campaign.
        if (!EnemyIntroductionProgress.HasSeenAbilityRule())
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

        // Beat 8 — Draw. Time and movement come back first: the draw is real combat against a
        // real enemy, not a frozen exercise.
        LiftVignette();
        yield return RampTimeScale(Time.timeScale, _restoreTimeScale, _releaseRampSeconds);
        ReleaseTimeScale();
        ReleaseEnemy(enemy);

        if (lesson.drawStep != null)
            yield return PlayDrawStep(enemy, lesson.drawStep);

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
        _card.ShowBanner(data.abilityLine);
        yield return WaitWhileEnemyLives(enemy, data);
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
            yield return TutorialDrawWait.WaitForCorrectDraw(expectedID);

            // The draw landed — TutorialDrawWait only ever returns on the correct character. The
            // wrong-draw handler comes off FIRST, or a stray recognition resolved during the hold
            // would overwrite the success copy with a correction the player did not earn.
            if (feedback != null)
            {
                EventBus.OnRecognitionResolved -= feedback;
                feedback = null;
            }

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

        _isPlaying = false;
        _routineActive = false;
        _claimedEnemy = null;
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        yield return new WaitForSecondsRealtime(seconds);
    }
}
