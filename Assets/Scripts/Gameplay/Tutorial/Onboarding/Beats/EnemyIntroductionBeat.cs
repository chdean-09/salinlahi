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

    /// <summary>
    /// The single live runner. One scene holds at most one, because the beat takes over global state
    /// — <c>Time.timeScale</c> and a full-screen dim — that two runners could not share.
    /// </summary>
    private static EnemyIntroductionBeat s_instance;

    private Enemy _claimedEnemy;
    private Coroutine _routine;
    private bool _isPlaying;
    private bool _timeScaleTaken;
    private float _restoreTimeScale = 1f;
    private TutorialSpotlightOverlay _runtimeVignette;

    /// <summary>True while a card is on screen. Diagnostic and test seam.</summary>
    public static bool IsPlaying => s_instance != null && s_instance._isPlaying;

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

    /// <summary>True while this level authored a lesson that has not yet played.</summary>
    public static bool HasPendingLesson =>
        s_instance != null && s_instance.ResolvePendingLesson() != null;

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
        }

        if (s_instance == this)
            s_instance = null;
    }

    private bool TryClaim(Enemy enemy, EnemyDataSO data, EnemyLessonSO lesson)
    {
        if (_isPlaying)
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
        if (_claimedEnemy != enemy || _isPlaying)
            return;

        _routine = StartCoroutine(PlayIntroduction(enemy));
    }

    private IEnumerator PlayIntroduction(Enemy enemy)
    {
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

            yield return lesson != null
                ? PlayLesson(enemy, data, lesson)
                : PlayCard(enemy, data);
        }
        finally
        {
            // Every exit path — normal, aborted mid-card, or the coroutine stopped by a disable —
            // must hand back the enemy's movement and the two globals. Half the field frozen at 0.15
            // is not a recoverable state for a player. The glyph badge is restored here too, on every
            // exit path, so an abort mid-lesson never leaves an enemy permanently unmarked.
            ReleaseTimeScale();
            LiftVignette();
            ReleaseEnemy(enemy);
            if (enemy != null) enemy.GlyphBadge?.Show();
            _isPlaying = false;
            RaiseRosterGateIfComplete();
            _claimedEnemy = null;
            _routine = null;
        }
    }

    /// <summary>
    /// Opens Level 1's slot-3 gate once every introducible type in the wave roster has been
    /// introduced. Called after each introduction rather than counted, so a level whose roster
    /// changes mid-development cannot leave a stale count holding the final slot shut.
    /// </summary>
    private static void RaiseRosterGateIfComplete()
    {
        LevelConfigSO config = GameManager.CurrentLevelConfig;
        if (config == null)
            return;

        if (!LevelRoster.AllIntroduced(
                LevelRoster.BuildIntroducibleRoster(config),
                EnemyIntroductionProgress.HasBeenIntroduced))
            return;

        SpawnAssignmentCoordinator coordinator = FindFirstObjectByType<SpawnAssignmentCoordinator>(
            FindObjectsInactive.Include);
        coordinator?.OpenGate(SpawnGateRegistry.Level1RosterMet);
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
        // rather than to a stretch of time.
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

        // Beat 2 — Ability. Already armed by IntroduceAndArm; this is the hold that lets the
        // player watch the clue crumble.
        yield return WaitRealtime(lesson.abilityBeatSeconds);

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

        _card.ShowBanner(data.abilityLine);
        yield return WaitWhileEnemyLives(enemy, data);
        _card.HideBanner();
    }

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
    /// The scene's dim if one was wired, otherwise one created on first use and kept. Created lazily
    /// rather than on wake so a level that never introduces a new type pays nothing, and cached so
    /// four introductions in one level do not stack four full-screen canvases.
    /// </summary>
    private TutorialSpotlightOverlay ResolveVignette()
    {
        if (_vignette != null)
            return _vignette;

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
        _claimedEnemy = null;
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        if (seconds <= 0f)
            yield break;

        yield return new WaitForSecondsRealtime(seconds);
    }
}
