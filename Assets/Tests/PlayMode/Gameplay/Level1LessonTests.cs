using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    /// <summary>
    /// Task 10 (2026-09-14-level1-enemy-introduction-lesson-plan). PlayMode-only coverage for the
    /// eight-beat enemy introduction lesson: everything under test here depends on Awake, OnEnable
    /// or a coroutine, none of which EditMode runs, so this fixture is the only automated evidence
    /// the lesson's ordering, inversion and teardown behaviour will ever get.
    ///
    /// <para>
    /// Every scene actor is built by hand rather than loaded from the shipped Level1_Config /
    /// IligawLesson assets, matching the precedent in <c>AbongSimulaAshTests</c> and
    /// <c>Level1EndToEndTests</c>: the point under test is <c>EnemyIntroductionBeat</c> /
    /// <c>Enemy.Initialize</c> / <c>IntroductionDecision</c>'s own logic, not the specific authored
    /// content, so a synthetic level with the same *shape* (one lesson enemy, requiredRestoredSlots
    /// = 1, armAbilityOnIntroduction = true, revealGlyphLate = true) keeps the fixture independent of
    /// asset edits.
    /// </para>
    ///
    /// <para>
    /// <b>Isolation.</b> <see cref="EnemyIntroductionProgress"/> and <see cref="AshFirstSlotController"/>
    /// both carry static, cross-scene state (the one-shot introduction record and the "ash shown this
    /// level" latch/registry respectively) that has previously leaked between PlayMode fixtures in
    /// this project. Both are reset in <c>[SetUp]</c> and <c>[TearDown]</c>, <c>Time.timeScale</c> is
    /// restored unconditionally in <c>[TearDown]</c> even on failure, and every scene object is
    /// destroyed so no fixture depends on run order.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class Level1LessonTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        private GameManager _gameManager;
        private EnemyIntroductionBeat _beat;
        private EnemyIntroductionCardView _cardView;
        private CanvasGroup _cardGroup;
        private TutorialSpotlightOverlay _vignette;
        private ActiveCluePresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            ReleaseSingleton<GameManager>();
            TutorialRuntimeState.Clear();
            EnemyIntroductionProgress.ResetForTests();
            AshFirstSlotController.ResetRegistryForTests();
            ActiveCluePresenter.SetActiveForTests(null);
            Time.timeScale = 1f;

            _gameManager = CreateComponent<GameManager>("GameManager_Level1LessonTests");
            SetSingletonInstance(_gameManager);
            _gameManager.StartGame();

            // The card. A separate child GameObject holds the CanvasGroup so hiding the card
            // (HideCardImmediate deactivates the group's own GameObject) never disables the view
            // component that owns it.
            GameObject cardRoot = CreateTracked("IntroCard");
            _cardView = cardRoot.AddComponent<EnemyIntroductionCardView>();
            GameObject cardGroupGO = new GameObject("CardGroup");
            cardGroupGO.transform.SetParent(cardRoot.transform, false);
            CanvasGroup cardGroup = cardGroupGO.AddComponent<CanvasGroup>();
            SetPrivateField(_cardView, "_cardGroup", cardGroup);
            _cardGroup = cardGroup;

            // The beat. Wired with our own TutorialSpotlightOverlay (rather than letting it create
            // one at runtime) so tests can read TutorialSpotlightOverlay.IsVisible directly.
            GameObject beatGO = CreateTracked("EnemyIntroductionBeat");
            _beat = beatGO.AddComponent<EnemyIntroductionBeat>();
            SetPrivateField(_beat, "_card", _cardView);

            _vignette = TutorialSpotlightOverlay.CreateRuntime();
            _objectsToDestroy.Add(_vignette.gameObject);
            SetPrivateField(_beat, "_vignette", _vignette);

            // Deterministic timings: no test here should depend on real wall-clock ramps. Only the
            // ability-hold step (authored per lesson/test) is left long enough to catch the beat
            // mid-play before it advances on its own.
            SetPrivateField(_beat, "_spawnSettleSeconds", 0f);
            SetPrivateField(_beat, "_haltRampSeconds", 0f);
            SetPrivateField(_beat, "_releaseRampSeconds", 0f);

            // The clue presenter. EnemyIntroductionBeat.RestoredSlotCount() finds this by type, so
            // one live instance is always present; each test points its _level field and configures
            // RestorationState against its own synthetic focus words.
            GameObject presenterGO = CreateTracked("ActiveCluePresenter_Level1LessonTests");
            _presenter = presenterGO.AddComponent<ActiveCluePresenter>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }
            _objectsToDestroy.Clear();

            ClearSingletonInstance<GameManager>();
            TutorialRuntimeState.Clear();
            EnemyIntroductionProgress.ResetForTests();
            AshFirstSlotController.ResetRegistryForTests();
            ActiveCluePresenter.SetActiveForTests(null);

            // Unconditional, even on failure: a fixture that leaves the game slowed cascades into
            // every test after it.
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------------------------
        // 1. Deferral suppresses (+ required negative control)
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// An enemy type with no lesson of its own, spawned while the level's one authored lesson
        /// (here: a stand-in for Abo) is still pending, must defer: no card, and its own signature
        /// ability suppressed too. The negative control is required, not decorative — Salinlahi has
        /// shipped a suppression check that matched nothing before (see
        /// a-filter-that-matches-nothing-looks-like-a-pass): without a case that proves the ability
        /// CAN read armed, "suppressed" could be passing against a matcher that always reports
        /// suppressed regardless of state.
        /// </summary>
        [UnityTest]
        public IEnumerator DeferralWhileLessonPending_SuppressesOtherType_ArmsWithNothingPending()
        {
            yield return null;

            BaybayinCharacterSO iligawChar = MakeCharacter("EI", "symbol.test.defer.ei");
            BaybayinCharacterSO aboChar = MakeCharacter("A", "symbol.test.defer.a");

            EnemyDataSO iligawData = CreateEnemyData(
                "test_iligaw_defer", "Iligaw", iligawChar, spawnsMirrorDecoy: true);
            EnemyDataSO aboData = CreateEnemyData(
                "test_abo_defer", "Abo ng Simula", aboChar, ashesFirstSlot: true);
            EnemyLessonSO aboLesson = CreateAboShapedLesson(aboData);

            LevelConfigSO pendingConfig = CreateLevelConfig(
                new List<EnemyDataSO> { iligawData, aboData },
                new[] { aboLesson },
                new List<FocusWordDefinition>());
            _gameManager.SetLevel(pendingConfig);

            // --- Main case: Abo's lesson has not played yet, so Iligaw must defer. ---
            Enemy iligaw = CreateEnemyShell("Iligaw_Deferred");
            Assert.IsTrue(iligaw.Initialize(iligawData));

            Assert.AreEqual(IntroductionOutcome.DeferAndSuppress, iligaw.IntroductionOutcome,
                "A type with no lesson of its own, spawned while the level's lesson is still "
                + "pending, must defer rather than play its own card.");

            MirrorDecoyController decoy = iligaw.GetComponent<MirrorDecoyController>();
            Assert.IsNotNull(decoy, "spawnsMirrorDecoy should attach MirrorDecoyController.");
            Assert.IsTrue(decoy.IsSuppressedForIntroductionSpawn,
                "Deferral must suppress the deferred type's own signature ability, not just "
                + "withhold its card.");

            // --- Negative control: seed Iligaw as already met, with nothing else pending. ---
            // Seeded directly through the progress store (not by letting a first spawn's card
            // actually play) so this half of the test is isolated from the beat/coroutine machinery
            // exercised elsewhere in this fixture.
            Assert.IsTrue(EnemyIntroductionProgress.TryClaimIntroduction(iligawData),
                "setup: seed Iligaw as already introduced for the control");

            LevelConfigSO clearConfig = CreateLevelConfig(
                new List<EnemyDataSO> { iligawData },
                System.Array.Empty<EnemyLessonSO>(),
                new List<FocusWordDefinition>());
            _gameManager.SetLevel(clearConfig);

            Enemy iligawControl = CreateEnemyShell("Iligaw_Control");
            Assert.IsTrue(iligawControl.Initialize(iligawData));

            Assert.AreEqual(IntroductionOutcome.None, iligawControl.IntroductionOutcome,
                "Negative control precondition: with nothing pending, and this type already met, "
                + "this must be an ordinary spawn.");

            MirrorDecoyController controlDecoy = iligawControl.GetComponent<MirrorDecoyController>();
            Assert.IsFalse(controlDecoy.IsSuppressedForIntroductionSpawn,
                "Negative control: with no lesson pending the ability must be ARMED. Without this "
                + "case the suppression assertion above could be passing against a check that "
                + "never actually fires.");
        }

        // ------------------------------------------------------------------------------------
        // 2. The inversion
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Abo's own introduction spawn is the one deliberate exception to "an introduction spawn
        /// suppresses its ability": the lesson arms it instead, because beat 2 is the ability firing
        /// in front of the player. <see cref="IntroductionDecision"/> encodes both rules; this pins
        /// the arm branch through the real Enemy.Initialize path, not just the pure decision table.
        /// </summary>
        [UnityTest]
        public IEnumerator AboIntroduction_ReportsIntroduceAndArm_AbilityNotSuppressed()
        {
            yield return null;

            BaybayinCharacterSO aboChar = MakeCharacter("A", "symbol.test.inv.a");
            BaybayinCharacterSO naChar = MakeCharacter("NA", "symbol.test.inv.na");

            EnemyDataSO aboData = CreateEnemyData(
                "test_abo_inversion", "Abo ng Simula", aboChar, ashesFirstSlot: true);
            EnemyLessonSO aboLesson = CreateAboShapedLesson(aboData);

            FocusWordDefinition word = CreateWord("level.test.inv.ina", "ina", "INA", aboChar, naChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { aboData }, new[] { aboLesson },
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);

            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);
            // Satisfies the lesson's precondition (requiredRestoredSlots = 1): the first slot (A) is
            // already restored, exactly as Abo's real design requires.
            _presenter.RestorationState.Apply(aboChar.stableId);

            Enemy abo = CreateEnemyShell("Abo_Inversion");
            Assert.IsTrue(abo.Initialize(aboData));

            Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, abo.IntroductionOutcome,
                "Abo's lesson arms the ability on the introduction spawn instead of suppressing "
                + "it — the inversion IntroductionDecision documents.");
            Assert.IsTrue(abo.IsIntroductionSpawn);

            AshFirstSlotController ash = abo.GetComponent<AshFirstSlotController>();
            Assert.IsNotNull(ash, "ashesFirstSlot should attach AshFirstSlotController.");
            Assert.IsFalse(ash.IsSuppressedForIntroductionSpawn,
                "An ordinary introduction suppresses its ability; Abo's lesson explicitly does "
                + "not, because beat 2 IS the ability firing.");
        }

        // ------------------------------------------------------------------------------------
        // 3. The precondition does not burn the one-shot
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// An Abo who arrives before any slot is restored must be declined without spending his
        /// one-shot introduction. If the claim were spent here, Abo would never be introduced for
        /// the rest of the campaign — a silent, permanent, and very hard to notice bug, since nothing
        /// crashes and nothing logs.
        /// </summary>
        [UnityTest]
        public IEnumerator AboPrecondition_DeclinesWithZeroRestoredSlots_WithoutBurningTheOneShot()
        {
            yield return null;

            BaybayinCharacterSO aboChar = MakeCharacter("A", "symbol.test.pre.a");
            BaybayinCharacterSO naChar = MakeCharacter("NA", "symbol.test.pre.na");

            EnemyDataSO aboData = CreateEnemyData(
                "test_abo_precondition", "Abo ng Simula", aboChar, ashesFirstSlot: true);
            EnemyLessonSO aboLesson = CreateAboShapedLesson(aboData); // requiredRestoredSlots = 1

            FocusWordDefinition word = CreateWord("level.test.pre.ina", "ina", "INA", aboChar, naChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { aboData }, new[] { aboLesson },
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);

            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);
            // Deliberately restore nothing.

            Assert.AreEqual(0, _presenter.RestoredSlotCount, "precondition: nothing restored yet.");
            Assert.IsFalse(EnemyIntroductionProgress.HasBeenIntroduced(aboData),
                "precondition: Abo has never been introduced.");

            Enemy abo = CreateEnemyShell("Abo_Precondition");
            Assert.IsTrue(abo.Initialize(aboData));

            Assert.AreEqual(IntroductionOutcome.DeferAndSuppress, abo.IntroductionOutcome,
                "An Abo who arrives before the clue can lose anything must be declined — no card, "
                + "ability suppressed until the precondition is met.");
            Assert.IsFalse(abo.IsIntroductionSpawn);

            Assert.IsFalse(EnemyIntroductionProgress.HasBeenIntroduced(aboData),
                "The decline must NOT spend Abo's one-shot introduction, or Abo would never be "
                + "introduced for the rest of the campaign.");
        }

        // ------------------------------------------------------------------------------------
        // 4. Beat 2 is visible (+ required negative control)
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The exact reason <c>requiredRestoredSlots</c> exists: with one slot restored, ashing the
        /// word's first slot changes what the masked spelling renders; with nothing restored, the
        /// target slot is already unreadable and the ash is a no-op. The negative control IS the
        /// design rationale in <see cref="EnemyLessonSO.requiredRestoredSlots"/>'s own doc comment —
        /// asserting only the "differs" half would pass an ash that always renders differently
        /// regardless of restoration state, which is not what beat 2 needs to be true.
        /// </summary>
        [UnityTest]
        public IEnumerator Beat2MaskedSpelling_DiffersWithOneSlotRestored_EqualWithNothingRestored()
        {
            yield return null;

            BaybayinCharacterSO i = MakeCharacter("EI", "symbol.test.beat2.i");
            BaybayinCharacterSO na = MakeCharacter("NA", "symbol.test.beat2.na");
            FocusWordDefinition ina = CreateWord("level.test.beat2.ina", "ina", "INA", i, na);

            // One slot restored (I): the needed slot is now NA, INA's second symbol — the shape
            // Abo's requiredRestoredSlots = 1 exists to guarantee.
            var restoredOne = new ActiveClueRestorationState();
            restoredOne.Configure(new List<FocusWordDefinition> { ina });
            restoredOne.Apply(i.stableId);

            string ashedOn = InvokeBuildMaskedSpelling(ina, na.stableId, ashFirstSlot: true, restoredOne);
            string ashedOff = InvokeBuildMaskedSpelling(ina, na.stableId, ashFirstSlot: false, restoredOne);

            Assert.AreNotEqual(ashedOn, ashedOff,
                "With one slot restored, beat 2 must be visible: ashing the first slot must "
                + "change what the masked spelling renders.");

            // Negative control: nothing restored at all. With no progress made, the needed slot IS
            // the word's own first symbol (I) -- not NA, which is only "needed" after I is restored
            // above. Using the same needed-slot symbol here is the point: it is exactly the
            // coincidence AshFirstSlotController.WantsToArm refuses to arm on ("the needed slot
            // being its word's first symbol makes the ash a no-op"), which is why
            // requiredRestoredSlots exists at all.
            var restoredNone = new ActiveClueRestorationState();
            restoredNone.Configure(new List<FocusWordDefinition> { ina });

            string noneAshedOn = InvokeBuildMaskedSpelling(ina, i.stableId, ashFirstSlot: true, restoredNone);
            string noneAshedOff = InvokeBuildMaskedSpelling(ina, i.stableId, ashFirstSlot: false, restoredNone);

            Assert.AreEqual(noneAshedOn, noneAshedOff,
                "With nothing restored, the needed slot (I) is already the word's first symbol, so "
                + "the ash and the target mask coincide and ash-on/ash-off must render identically. "
                + "This equality is exactly why requiredRestoredSlots exists: arming here would be "
                + "an invisible event.");
        }

        // ------------------------------------------------------------------------------------
        // 4b. Beat 2 actually shows the ability, before beats 5-6 name it
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The premise the whole eight-beat design rests on: the ability fires UNPROMPTED, before
        /// the enemy is named or explained. Beat 2 used to be a fixed realtime hold of
        /// <c>abilityBeatSeconds</c>, which could not deliver that — <c>AshFirstSlotController</c>
        /// accrues its 1.5 s arm delay on SCALED time while the beat holds <c>Time.timeScale</c> at
        /// 0.15, so spawn settle + halt ramp + a 2.5 s hold bought roughly 0.8 scaled seconds and
        /// the gust landed four to six wall-clock seconds later — during beats 5-6, with the name
        /// card already on screen.
        ///
        /// <para>
        /// The old assertion ("the ability is not suppressed") could not catch that: an armed-but-
        /// not-yet-fired ability reads exactly the same. This asserts the ORDERING instead — the
        /// card stays down until the ash has actually armed, and comes up once it has.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator Beat2_KeepsTheNameCardDownUntilTheAbilityHasActuallyFired()
        {
            yield return null;

            BaybayinCharacterSO aboChar = MakeCharacter("A", "symbol.test.beat2.a");
            BaybayinCharacterSO naChar = MakeCharacter("NA", "symbol.test.beat2.na");

            EnemyDataSO aboData = CreateEnemyData(
                "test_abo_beat2", "Abo ng Simula", aboChar, ashesFirstSlot: true);
            EnemyLessonSO aboLesson = CreateAboShapedLesson(aboData);
            // Short post-arm hold: this test is about what gates beat 2, not how long it lingers
            // once the ability has landed.
            aboLesson.abilityBeatSeconds = 0.05f;

            FocusWordDefinition word = CreateWord("level.test.beat2.ina", "ina", "INA", aboChar, naChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { aboData }, new[] { aboLesson },
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);

            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);
            _presenter.RestorationState.Apply(aboChar.stableId);

            Enemy abo = CreateEnemyShell("Abo_Beat2");
            Assert.IsTrue(abo.Initialize(aboData));
            Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, abo.IntroductionOutcome,
                "setup: this must be a real lesson spawn, with the ability armed.");

            AshFirstSlotController ash = abo.GetComponent<AshFirstSlotController>();
            Assert.IsNotNull(ash, "setup: ashesFirstSlot should attach AshFirstSlotController.");
            Assert.IsFalse(ash.IsArmedThisSpawn,
                "setup: the ash has not fired yet — armed-for-this-spawn is not the same as fired.");

            // Ten times the authored hold, in WALL-CLOCK seconds rather than frames — batchmode
            // runs frames far faster than real time, so a frame count would let a fixed-duration
            // beat 2 pass this unchanged. While the ability has not fired, nothing may name the
            // enemy however long the beat has been waiting.
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.IsFalse(ash.IsArmedThisSpawn,
                "setup: the ash must still be unfired for this assertion to mean anything.");
            Assert.AreEqual(0f, _cardGroup.alpha,
                "Beat 2 must hold the name card down until the ability has actually fired. A card "
                + "on screen here means the player is being told what the enemy is called before "
                + "they have seen it do anything, which is the failure the lesson exists to avoid.");

            // The ability fires.
            ash.ArmAsh();

            bool cardAppeared = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!cardAppeared && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                cardAppeared = _cardGroup.alpha > 0.99f;
            }

            Assert.IsTrue(cardAppeared,
                "Once the ability has fired, beat 2 must release and beats 5-6 must name the enemy. "
                + "A beat that never releases would hang the lesson behind the ability.");
        }

        // ------------------------------------------------------------------------------------
        // 4c. Beat 2 waits on ANY signature ability, not just the ash
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The same ordering guarantee as the test above, for a lesson whose enemy has a
        /// <see cref="MirrorDecoyController"/> rather than an <see cref="AshFirstSlotController"/>
        /// — which is what Level 1 now ships (Iligaw, not Abo ng Simula).
        ///
        /// <para>
        /// This is the regression guard for a defect that would have shipped invisibly.
        /// <c>PlayAbilityBeat</c> used to look up <c>AshFirstSlotController</c> by CONCRETE TYPE and
        /// fall through to a blind fixed hold when it found none. For an Iligaw lesson that early
        /// return is taken every time, so beat 2 would not have waited for the copy to appear at
        /// all — reintroducing the exact bug the wait was added to remove, while every existing
        /// ash-based test kept passing. The seam is <c>IIntroducibleAbility</c>.
        /// </para>
        ///
        /// <para>
        /// No <c>EnemyPool</c> exists in this fixture, so the copy genuinely cannot be placed and
        /// <c>HasFiredThisSpawn</c> stays false on its own — the un-fired half needs no faking. The
        /// latch is then set directly to stand in for the copy landing on the field.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator Beat2_WaitsForAMirrorDecoyLesson_NotOnlyAnAshOne()
        {
            yield return null;

            BaybayinCharacterSO eiChar = MakeCharacter("EI", "symbol.test.beat2c.ei");
            BaybayinCharacterSO naChar = MakeCharacter("NA", "symbol.test.beat2c.na");

            EnemyDataSO iligawData = CreateEnemyData(
                "test_iligaw_beat2", "Iligaw", eiChar, spawnsMirrorDecoy: true);
            EnemyLessonSO iligawLesson = CreateIligawShapedLesson(iligawData);
            // Short post-fire hold, so anything still on screen after it is the WAIT, not the hold.
            iligawLesson.abilityBeatSeconds = 0.05f;

            FocusWordDefinition word = CreateWord(
                "level.test.beat2c.ina", "ina", "INA", eiChar, naChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { iligawData }, new[] { iligawLesson },
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);

            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);

            Enemy iligaw = CreateEnemyShell("Iligaw_Beat2");
            Assert.IsTrue(iligaw.Initialize(iligawData));
            Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, iligaw.IntroductionOutcome,
                "setup: requiredRestoredSlots = 0 means this spawn is the lesson, ability armed.");

            MirrorDecoyController decoy = iligaw.GetComponent<MirrorDecoyController>();
            Assert.IsNotNull(decoy, "setup: spawnsMirrorDecoy should attach MirrorDecoyController.");
            Assert.IsInstanceOf<IIntroducibleAbility>(decoy,
                "MirrorDecoyController must implement the seam beat 2 asks through, or the beat "
                + "silently degrades to a fixed hold for every Iligaw lesson.");
            Assert.IsFalse(((IIntroducibleAbility)decoy).HasFiredThisSpawn,
                "setup: no copy has been placed yet.");

            // Enemy.Initialize already started the beat (it calls BeginIntroduction itself once
            // the spawn is an introduction spawn), so there is nothing further to kick off here.

            // Ten times the authored hold, in WALL-CLOCK seconds: batchmode runs frames far faster
            // than real time, so a frame count would let a fixed-duration beat 2 pass unchanged.
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.IsFalse(((IIntroducibleAbility)decoy).HasFiredThisSpawn,
                "setup: the copy must still be unplaced for this assertion to mean anything.");
            Assert.AreEqual(0f, _cardGroup.alpha,
                "Beat 2 must hold the name card down until the mirror copy has actually appeared. "
                + "A card on screen here means the lesson named Iligaw before the player saw one "
                + "enemy become two — which is the whole reason the lesson moved to Iligaw.");

            // The copy lands.
            SetPrivateField(decoy, "_decoySpawnedThisSpawn", true);
            Assert.IsTrue(((IIntroducibleAbility)decoy).HasFiredThisSpawn);

            bool cardAppeared = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!cardAppeared && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                cardAppeared = _cardGroup.alpha > 0.99f;
            }

            Assert.IsTrue(cardAppeared,
                "Once the copy is on the field, beat 2 must release and beats 5-6 must name the "
                + "enemy. A beat that never releases would hang the lesson behind the ability.");
        }

        // ------------------------------------------------------------------------------------
        // 4d. A lesson that does NOT arm the ability must not wait for it
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The parked guard. When a lesson leaves <c>armAbilityOnIntroduction</c> false the standing
        /// suppression rule keeps the ability inert for the whole introduction spawn, so
        /// <c>HasFiredThisSpawn</c> can never become true. Waiting on it would spend the entire
        /// <c>_abilityBeatArmTimeoutSeconds</c> (15 s by default) with the field halted, the screen
        /// dimmed and no card up, and then continue anyway.
        /// </summary>
        [UnityTest]
        public IEnumerator Beat2_SkipsTheWaitEntirely_WhenTheLessonDoesNotArmTheAbility()
        {
            yield return null;

            BaybayinCharacterSO aboChar = MakeCharacter("A", "symbol.test.beat2d.a");
            BaybayinCharacterSO naChar = MakeCharacter("NA", "symbol.test.beat2d.na");

            EnemyDataSO aboData = CreateEnemyData(
                "test_abo_beat2d", "Abo ng Simula", aboChar, ashesFirstSlot: true);
            EnemyLessonSO lesson = CreateAboShapedLesson(aboData);
            lesson.requiredRestoredSlots = 0;
            lesson.armAbilityOnIntroduction = false;
            lesson.abilityBeatSeconds = 0.05f;

            FocusWordDefinition word = CreateWord(
                "level.test.beat2d.ina", "ina", "INA", aboChar, naChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { aboData }, new[] { lesson },
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);

            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);

            Enemy abo = CreateEnemyShell("Abo_Beat2d");
            Assert.IsTrue(abo.Initialize(aboData));
            Assert.AreEqual(IntroductionOutcome.IntroduceAndSuppress, abo.IntroductionOutcome,
                "setup: a lesson that does not arm leaves the standing suppression rule in force.");

            AshFirstSlotController ash = abo.GetComponent<AshFirstSlotController>();
            Assert.IsNotNull(ash);
            Assert.IsTrue(ash.IsSuppressedForIntroductionSpawn,
                "setup: the ash is inert for this spawn, so it can never report as fired.");

            // Well inside the 15 s arm timeout and well outside the 0.05 s authored hold: a beat
            // that waited on an ability that can never fire would still be dark here.
            bool cardAppeared = false;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!cardAppeared && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                cardAppeared = _cardGroup.alpha > 0.99f;
            }

            Assert.IsTrue(cardAppeared,
                "Beat 2 must take the fixed hold when the lesson suppresses the ability. Waiting "
                + "burns the full arm timeout behind a dimmed, halted screen and then proceeds "
                + "regardless, which is fifteen seconds of nothing for the player.");
        }

        // ------------------------------------------------------------------------------------
        // 4e. Beat 8's prompt actually reaches the screen
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Defect 1 from the 2026-09-15 visual check: across two clean runs the guide's root was
        /// INACTIVE for every sampled frame, so beat 8 asked for a draw with no instruction on
        /// screen — and in one run the enemy walked past and cost a heart. The prompt STRING was
        /// set correctly the whole time, which is why 1223 green string-asserting tests missed it.
        ///
        /// <para>
        /// The cause is re-entrancy, and this test reproduces it exactly: in Gameplay.unity the
        /// guide's <c>_root</c> is its own GameObject, authored inactive. A component on an inactive
        /// GameObject has never run <c>Awake</c>, so the first <c>SetActive(true)</c> runs it
        /// synchronously from inside <c>ShowMessage</c> — and <c>Awake</c>'s defensive
        /// <c>SetActive(false)</c> then undid the show. Asserting the STRING passes either way;
        /// only <c>activeSelf</c> tells them apart.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator Beat8Prompt_BecomesVisible_OnARootAuthoredInactive()
        {
            GameObject root = CreateTracked("Level1TutorialGuideUI_Beat8");
            root.SetActive(false);

            // Awake has not run and must not run until the first show — exactly the scene's state.
            Level1TutorialGuideUI guide = root.AddComponent<Level1TutorialGuideUI>();
            GameObject promptGO = new GameObject("DrawPromptText");
            promptGO.transform.SetParent(root.transform, false);
            TMPro.TextMeshProUGUI prompt = promptGO.AddComponent<TMPro.TextMeshProUGUI>();

            SetPrivateField(guide, "_root", root);
            SetPrivateField(guide, "_promptText", prompt);

            Assert.IsFalse(root.activeSelf, "setup: the root is authored inactive, as in the scene.");

            guide.ShowMessage("Draw EI", canSkip: false);
            yield return null;

            Assert.AreEqual("Draw EI", prompt.text,
                "setup: the prompt string was never the broken part.");
            Assert.IsTrue(root.activeSelf,
                "Beat 8's prompt must actually reach the screen. An inactive root means the player "
                + "is asked to draw with no instruction visible, which is what shipped.");

            guide.Hide();
            yield return null;

            Assert.IsFalse(root.activeSelf,
                "The guide must close after the draw rather than staying up over live combat.");
        }

        // ------------------------------------------------------------------------------------
        // 5. Teardown on abort
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Aborting a lesson mid-play (the beat's OnDisable, triggered here by disabling the
        /// component) must hand back every piece of state the lesson borrowed: real time, the
        /// vignette, the enemy's glyph badge, and its movement. A lesson that aborts holding the
        /// badge hidden leaves an enemy permanently unmarked for the rest of its life on the field.
        /// <para>
        /// Note: <c>EnemyIntroductionBeat.PlayIntroduction</c> restores the badge and the enemy's
        /// movement inside its coroutine's <c>finally</c> block. Unity does not run a coroutine's
        /// pending <c>finally</c> when the coroutine is stopped via <c>StopCoroutine</c> (which is
        /// what disabling the beat does) — only a normal completion or an exception unwinds it. This
        /// test exercises exactly that path, which is presumably why <c>OnDisable</c> explicitly
        /// re-does the timescale and vignette restoration rather than trusting the <c>finally</c> to
        /// run.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AbortMidLesson_RestoresTimeScaleVignetteBadgeAndMovement()
        {
            yield return null;

            BaybayinCharacterSO aboChar = MakeCharacter("A", "symbol.test.abort.a");
            BaybayinCharacterSO naChar = MakeCharacter("NA", "symbol.test.abort.na");
            aboChar.badgeSprite = GlyphBadgePlayModeTestHelpers.CreateSprite(Color.red);

            EnemyDataSO aboData = CreateEnemyData(
                "test_abo_abort", "Abo ng Simula", aboChar, ashesFirstSlot: true);
            EnemyLessonSO aboLesson = CreateAboShapedLesson(aboData);
            // Long enough that the abort below is guaranteed to land mid-beat-2, not race its
            // natural advance to beat 3.
            aboLesson.abilityBeatSeconds = 30f;

            FocusWordDefinition word = CreateWord("level.test.abort.ina", "ina", "INA", aboChar, naChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { aboData }, new[] { aboLesson },
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);

            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);
            _presenter.RestorationState.Apply(aboChar.stableId);

            Enemy abo = CreateEnemyShell("Abo_Abort");
            (EnemyGlyphBadge badge, SpriteRenderer badgeRenderer) = GlyphBadgePlayModeTestHelpers
                .AddGlyphBadgeChild(abo.gameObject, GlyphBadgePlayModeTestHelpers.CreateBadgeConfig());

            Assert.IsTrue(abo.Initialize(aboData));
            Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, abo.IntroductionOutcome,
                "setup: this must be a real lesson spawn to exercise the lesson's teardown path.");

            EnemyMover mover = abo.GetComponent<EnemyMover>();

            // Mid-lesson precondition: the beat's coroutine runs synchronously (no yields consumed)
            // from Initialize through beat 1's halt/vignette/timescale drop and beat 2's hidden
            // badge, stopping only at the wall-clock ability-hold wait — so this state is already
            // true the instant Initialize() returns, with no frame needed.
            Assert.AreNotEqual(1f, Time.timeScale, "setup: mid-lesson time scale must be dropped.");
            Assert.IsTrue(_vignette.IsVisible, "setup: mid-lesson the vignette must be up.");
            Assert.IsFalse(mover.IsMoving, "setup: mid-lesson the enemy must be halted.");
            Assert.IsTrue(IsBadgeHidden(badgeRenderer),
                "setup: revealGlyphLate must hide the badge through beats 1-6.");

            // Abort: disable the beat mid-play, the way a level abort or scene teardown would.
            _beat.enabled = false;
            yield return null;

            Assert.AreEqual(1f, Time.timeScale,
                "Aborting mid-lesson must restore real time.");
            Assert.IsFalse(_vignette.IsVisible,
                "Aborting mid-lesson must lift the vignette.");
            Assert.IsTrue(IsBadgeVisible(badgeRenderer),
                "Aborting mid-lesson must not leave the enemy's glyph badge permanently hidden — "
                + "an enemy left unmarked for the rest of its life on the field.");
            Assert.IsTrue(mover.IsMoving,
                "Aborting mid-lesson must hand the enemy's movement back.");
        }

        // ------------------------------------------------------------------------------------
        // Scene helpers
        // ------------------------------------------------------------------------------------

        private Enemy CreateEnemyShell(string name)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();
            GlyphBadgePlayModeTestHelpers.DisableDebugLabels(enemy);
            go.SetActive(true);
            _objectsToDestroy.Add(go);
            return enemy;
        }

        private EnemyDataSO CreateEnemyData(
            string enemyID,
            string displayName,
            BaybayinCharacterSO character,
            bool ashesFirstSlot = false,
            bool spawnsMirrorDecoy = false)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = enemyID;
            data.displayName = displayName;
            data.discoverySubtitle = "test subtitle";
            data.abilityLine = "test ability line";
            data.assignedCharacter = character;
            data.maxHealth = 10;
            data.moveSpeed = 1f;
            data.useHurtFeedback = false;
            data.ashesFirstSlot = ashesFirstSlot;
            data.spawnsMirrorDecoy = spawnsMirrorDecoy;
            _objectsToDestroy.Add(data);
            return data;
        }

        /// <summary>
        /// Same shape as the shipped IligawLesson.asset — Level 1's actual lesson since the
        /// 2026-09-15 retarget. The differences from the Abo shape are the two that matter:
        /// requiredRestoredSlots is 0 (a mirror copy reads cold, with no restored slot needed) and
        /// the ability beat 2 waits on is a MirrorDecoyController.
        /// </summary>
        private EnemyLessonSO CreateIligawShapedLesson(EnemyDataSO enemy)
        {
            var lesson = ScriptableObject.CreateInstance<EnemyLessonSO>();
            lesson.enemy = enemy;
            lesson.requiredRestoredSlots = 0;
            lesson.armAbilityOnIntroduction = true;
            lesson.abilityBeatSeconds = 2.5f;
            lesson.revealGlyphLate = true;
            lesson.drawStep = null; // beat 8 skipped; not exercised by this fixture
            _objectsToDestroy.Add(lesson);
            return lesson;
        }

        /// <summary>
        /// The pre-retarget shape, kept because several tests here are about the beat machinery
        /// rather than about which enemy carries it, and an ash-bearing lesson is the other half of
        /// the evidence that beat 2's wait is not hardcoded to one ability. See task-10-brief.md.
        /// </summary>
        private EnemyLessonSO CreateAboShapedLesson(EnemyDataSO enemy)
        {
            var lesson = ScriptableObject.CreateInstance<EnemyLessonSO>();
            lesson.enemy = enemy;
            lesson.requiredRestoredSlots = 1;
            lesson.armAbilityOnIntroduction = true;
            lesson.abilityBeatSeconds = 2.5f;
            lesson.revealGlyphLate = true;
            lesson.drawStep = null; // beat 8 skipped; not exercised by this fixture
            _objectsToDestroy.Add(lesson);
            return lesson;
        }

        private LevelConfigSO CreateLevelConfig(
            List<EnemyDataSO> waveRoster,
            EnemyLessonSO[] enemyLessons,
            List<FocusWordDefinition> focusWords)
        {
            var config = ScriptableObject.CreateInstance<LevelConfigSO>();
            config.levelNumber = 1;
            config.stableId = "level.test.lesson";
            config.enemyLessons = enemyLessons ?? System.Array.Empty<EnemyLessonSO>();
            config.focusWords = focusWords ?? new List<FocusWordDefinition>();

            var wave = new WaveDefinition { enemyTypes = new List<EnemyDataSO>(waveRoster) };
            config.waves = new List<WaveDefinition> { wave };

            _objectsToDestroy.Add(config);
            return config;
        }

        private static FocusWordDefinition CreateWord(
            string stableId,
            string latinSpelling,
            string displayLabel,
            BaybayinCharacterSO first,
            BaybayinCharacterSO second)
        {
            return new FocusWordDefinition
            {
                stableId = stableId,
                latinSpelling = latinSpelling,
                displayLabel = displayLabel,
                meaning = "test-word",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = first },
                    new SymbolValueReference { symbol = second },
                },
            };
        }

        private BaybayinCharacterSO MakeCharacter(string characterID, string stableId)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = characterID;
            character.syllable = characterID.ToLowerInvariant();
            character.stableId = stableId;
            _objectsToDestroy.Add(character);
            return character;
        }

        private GameObject CreateTracked(string name)
        {
            var go = new GameObject(name);
            _objectsToDestroy.Add(go);
            return go;
        }

        private static bool IsBadgeVisible(SpriteRenderer badgeRenderer) =>
            badgeRenderer != null && badgeRenderer.enabled && badgeRenderer.color.a > 0.5f;

        private static bool IsBadgeHidden(SpriteRenderer badgeRenderer) =>
            badgeRenderer != null && badgeRenderer.color.a < 0.5f;

        // ------------------------------------------------------------------------------------
        // Reflection helpers
        // ------------------------------------------------------------------------------------

        private static string InvokeBuildMaskedSpelling(
            FocusWordDefinition word,
            string symbolStableId,
            bool ashFirstSlot,
            ActiveClueRestorationState restorationState)
        {
            MethodInfo method = typeof(ActiveCluePresenter).GetMethod(
                "BuildMaskedSpellingWithRestoration",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing ActiveCluePresenter.BuildMaskedSpellingWithRestoration.");
            return (string)method.Invoke(
                null, new object[] { word, symbolStableId, ashFirstSlot, restorationState });
        }

        private static void SetPrivateField(object target, string fieldName, object value) =>
            GlyphBadgePlayModeTestHelpers.SetPrivateField(target, fieldName, value);

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            PropertyInfo property = typeof(Singleton<T>).GetProperty(
                "Instance", BindingFlags.Static | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            PropertyInfo property = typeof(Singleton<T>).GetProperty(
                "Instance", BindingFlags.Static | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { null });
        }

        /// <summary>
        /// Destroys every <typeparamref name="T"/> an earlier fixture left in the scene and clears
        /// the static field, so this fixture's own manager takes Singleton&lt;T&gt;.Awake's "I am
        /// the instance" branch instead of the duplicate-destroy branch.
        /// </summary>
        private static void ReleaseSingleton<T>() where T : MonoBehaviour
        {
            foreach (T existing in Object.FindObjectsByType<T>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (existing != null)
                    Object.DestroyImmediate(existing);
            }

            ClearSingletonInstance<T>();
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            GameObject go = new GameObject(name);
            T component = go.AddComponent<T>();
            _objectsToDestroy.Add(go);
            return component;
        }
    }
}
