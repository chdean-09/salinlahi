using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
            // Beat 2 now asks IIntroducibleAbility.CanFireThisSpawn before it waits, and a mirror
            // copy's only source is EnemyPool — so whether one exists decides which branch the beat
            // takes. Start every test from "no pool", explicitly, rather than inheriting whatever an
            // earlier fixture left in the scene.
            ReleaseSingleton<EnemyPool>();
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
            // The stand-in pool below is deliberately never woken, so Singleton.OnDestroy never runs
            // for it and destroying its GameObject does not clear the static. Clear it by hand, or
            // the next fixture in this PlayMode run inherits a dangling EnemyPool.Instance.
            ClearSingletonInstance<EnemyPool>();
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

            // Negative control: nothing restored at all. A slot is readable only once it has been
            // RESTORED, so an untouched INA renders "____" whole and there is nothing left for the
            // ash to cover -- ash-on and ash-off must be identical. This is a stronger version of
            // the same control, not a weaker one: it used to rest on the needed slot coinciding
            // with the word's first symbol, and now holds for every symbol in an untouched word.
            // It is still exactly why requiredRestoredSlots exists -- arming before the player has
            // earned a slot is an invisible event.
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

        /// <summary>
        /// The word is blank until each syllable is EARNED, one restoration at a time.
        ///
        /// <para>
        /// Only the currently-needed slot used to be masked, so INA opened as "__na": NA could be
        /// read off the panel before the player had ever drawn it, and the Iligaw lesson's beat-9
        /// line — "one character, one piece of the memory — restored" — was said over a word that
        /// was already most of the way on screen. The three assertions below are the three states
        /// the player actually passes through.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator FocusWord_ReadsBlank_UntilEachSlotIsRestored()
        {
            yield return null;

            BaybayinCharacterSO i = MakeCharacter("I", "symbol.test.earned.i");
            BaybayinCharacterSO na = MakeCharacter("NA", "symbol.test.earned.na");
            FocusWordDefinition ina = CreateWord("level.test.earned.ina", "ina", "INA", i, na);

            var state = new ActiveClueRestorationState();
            state.Configure(new List<FocusWordDefinition> { ina });

            Assert.AreEqual(
                "____",
                InvokeBuildMaskedSpelling(ina, i.stableId, ashFirstSlot: false, state),
                "At level start nothing has been restored, so no syllable may be readable. "
                + "This is the assertion the old rule failed: it rendered \"__na\".");

            state.Apply(i.stableId);
            Assert.AreEqual(
                "i__",
                InvokeBuildMaskedSpelling(ina, na.stableId, ashFirstSlot: false, state),
                "Drawing I restores I's slot and only I's slot; NA stays hidden until its "
                + "carrier falls.");

            state.Apply(na.stableId);
            Assert.AreEqual(
                "ina",
                InvokeBuildMaskedSpelling(ina, null, ashFirstSlot: false, state),
                "Both slots restored: the whole word is readable.");
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
        /// The pool has to be present for this test to mean anything. Beat 2 asks
        /// <c>CanFireThisSpawn</c> BEFORE it waits, and a mirror copy's only source is
        /// <c>EnemyPool</c> — so with no pool the beat takes its fast fallback and never reaches the
        /// wait at all, which is correct behaviour but a different test (see
        /// <see cref="Beat2_FallsBackImmediately_WhenTheMirrorCopyHasNoPoolToComeFrom"/>). With the
        /// dependency in place, the copy is held off by <c>_spawnAttempted</c> — Update has already
        /// had its one look and placed nothing — which is exactly the state the wait exists for. The
        /// latch is then set directly to stand in for the copy landing on the field.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator Beat2_WaitsForAMirrorDecoyLesson_NotOnlyAnAshOne()
        {
            yield return null;

            GiveTheDecoyAPoolToDrawFrom();

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
            Assert.IsTrue(((IIntroducibleAbility)decoy).CanFireThisSpawn,
                "setup: the pool must be reachable, or beat 2 takes its fast fallback and this "
                + "test proves nothing about the wait.");

            // Hold the copy off without removing the dependency: _spawnAttempted is the state
            // MirrorDecoyController.Update leaves behind when it has looked once and placed nothing.
            // Set before the first yield, so it lands ahead of that Update.
            SetPrivateField(decoy, "_spawnAttempted", true);

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

        /// <summary>
        /// The other half of the test above: the same Iligaw lesson with the copy's dependency
        /// MISSING must fail fast rather than slowly.
        ///
        /// <para>
        /// A mirror copy comes from <c>EnemyPool</c> and nowhere else. On a level with no pool the
        /// copy can never be placed, <c>HasFiredThisSpawn</c> can never turn true, and a beat that
        /// simply waited would spend the whole <c>_abilityBeatArmTimeoutSeconds</c> — fifteen
        /// seconds of a halted field under a dimmed screen with no card up — before continuing
        /// anyway. Unlike an ability whose own trigger has not come round yet, a missing dependency
        /// cannot arrive mid-beat, so there is nothing to wait for: <c>CanFireThisSpawn</c> is asked
        /// once, up front, and the beat drops to its authored hold and warns.
        /// </para>
        ///
        /// <para>
        /// This is the ash's failure mode made worse by the retarget, so it is pinned here rather
        /// than left to a reviewer's reading. The three-second deadline is far inside the fifteen:
        /// a beat that regressed to waiting would still be dark when it expires.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator Beat2_FallsBackImmediately_WhenTheMirrorCopyHasNoPoolToComeFrom()
        {
            yield return null;

            // Deliberately NO GiveTheDecoyAPoolToDrawFrom() — SetUp already released the singleton,
            // so the dependency is genuinely absent rather than mocked away.
            Assert.IsNull(EnemyPool.Instance,
                "setup: this test is about a level with no pool, so there must not be one.");

            BaybayinCharacterSO eiChar = MakeCharacter("EI", "symbol.test.beat2f.ei");
            BaybayinCharacterSO naChar = MakeCharacter("NA", "symbol.test.beat2f.na");

            EnemyDataSO iligawData = CreateEnemyData(
                "test_iligaw_beat2f", "Iligaw", eiChar, spawnsMirrorDecoy: true);
            EnemyLessonSO iligawLesson = CreateIligawShapedLesson(iligawData);
            iligawLesson.abilityBeatSeconds = 0.05f;

            FocusWordDefinition word = CreateWord(
                "level.test.beat2f.ina", "ina", "INA", eiChar, naChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { iligawData }, new[] { iligawLesson },
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);

            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);

            Enemy iligaw = CreateEnemyShell("Iligaw_Beat2f");
            Assert.IsTrue(iligaw.Initialize(iligawData));
            Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, iligaw.IntroductionOutcome,
                "setup: the lesson still arms the ability; only its dependency is missing.");

            MirrorDecoyController decoy = iligaw.GetComponent<MirrorDecoyController>();
            Assert.IsNotNull(decoy);
            Assert.IsFalse(((IIntroducibleAbility)decoy).CanFireThisSpawn,
                "setup: with no EnemyPool the copy has nowhere to come from, which is the whole "
                + "premise of this test.");

            // The beat also warns on this path, but that is NOT asserted here. DebugLogger.LogWarning
            // carries [Conditional("ENABLE_SALINLAHI_LOG")], and ProjectSettings defines that symbol
            // for Standalone only — so whether the call exists at all depends on the active build
            // target, and a LogAssert.Expect would fail this test on Android or iOS while the beat
            // behaved perfectly. The timing below is the part that holds on every target.
            bool cardAppeared = false;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!cardAppeared && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                cardAppeared = _cardGroup.alpha > 0.99f;
            }

            Assert.IsTrue(cardAppeared,
                "Beat 2 must drop to its fixed hold at once when the ability's dependency is "
                + "missing. Waiting spends the full fifteen-second arm timeout behind a dimmed, "
                + "halted screen for a copy that can never appear, and then continues anyway.");
        }

        // ------------------------------------------------------------------------------------
        // 4c-2. Beat 2 — the glyph rule lands BEFORE the ability is ever let go
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The teaching order the lesson was reordered into: the UNIVERSAL rule (every enemy carries
        /// a mark) is stated first, and only then does this enemy spring its own trick. Put the
        /// other way round — the split first, the rule after — the player learns the exception
        /// before the mechanic, which is the order this beat exists to replace.
        ///
        /// <para>
        /// <b>The observable is the hold, not a timestamp.</b> <c>MirrorDecoyController</c> is held
        /// from the claim until <c>PlayAbilityBeat</c>'s very first statement releases it, so
        /// "_introductionHold is still true" is a direct statement that the beat has not yet reached
        /// the ability at all. It cannot pass by coincidence of timing the way a frame count could,
        /// and it holds however long the player takes to tap through the line.
        /// </para>
        ///
        /// <para>
        /// The badge assertions ride along deliberately. Beat 2 tells the player every enemy carries
        /// a mark while THIS enemy's mark is still hidden for beat 7's reveal; a well-meaning edit
        /// that "helpfully" showed the badge alongside the line it illustrates would spend the
        /// reveal and go unnoticed by every other test here.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator Beat2GlyphRule_PlaysBeforeTheAbilityBeat_WithTheBadgeStillHidden()
        {
            yield return null;

            GiveTheDecoyAPoolToDrawFrom();
            (DialogueController dialogue, GameObject dialoguePanel) = GiveTheLessonADialogueSurface();

            BaybayinCharacterSO eiChar = MakeCharacter("EI", "symbol.test.glyphrule.ei");
            BaybayinCharacterSO naChar = MakeCharacter("NA", "symbol.test.glyphrule.na");
            eiChar.badgeSprite = GlyphBadgePlayModeTestHelpers.CreateSprite(Color.red);

            EnemyDataSO iligawData = CreateEnemyData(
                "test_iligaw_glyphrule", "Iligaw", eiChar, spawnsMirrorDecoy: true);
            EnemyLessonSO lesson = CreateIligawShapedLesson(iligawData);
            lesson.glyphRuleLine = new OnboardingBeatCopy
            {
                fallbackText = "Bawat kaaway ay may dalang titik. Iyon ang kanilang kahinaan.",
            };
            // Short post-fire hold: this test is about what precedes the ability, not how long the
            // beat lingers once it has landed.
            lesson.abilityBeatSeconds = 0.05f;

            FocusWordDefinition word = CreateWord(
                "level.test.glyphrule.ina", "ina", "INA", eiChar, naChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { iligawData }, new[] { lesson },
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);

            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);

            Enemy iligaw = CreateEnemyShell("Iligaw_GlyphRule");
            (_, SpriteRenderer badgeRenderer) = GlyphBadgePlayModeTestHelpers
                .AddGlyphBadgeChild(iligaw.gameObject, GlyphBadgePlayModeTestHelpers.CreateBadgeConfig());

            Assert.IsTrue(iligaw.Initialize(iligawData));
            Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, iligaw.IntroductionOutcome,
                "setup: requiredRestoredSlots = 0 means this spawn is the lesson, ability armed.");

            MirrorDecoyController decoy = iligaw.GetComponent<MirrorDecoyController>();
            Assert.IsNotNull(decoy, "setup: spawnsMirrorDecoy should attach MirrorDecoyController.");
            Assert.IsTrue(((IIntroducibleAbility)decoy).CanFireThisSpawn,
                "setup: the pool must be reachable, or beat 2's ordering proves nothing about a "
                + "wait the beat would have skipped anyway.");
            Assert.IsTrue(GetPrivateField<bool>(decoy, "_introductionHold"),
                "setup: the claim holds the ability, and that hold is what this test watches.");

            // The stand-in pool was never woken, so letting Update actually take a copy from it
            // logs an error and fails the test. _spawnAttempted is the state Update leaves behind
            // when it has looked once and placed nothing — same device as the test above — and it
            // keeps the pool untouched without touching the hold this test reads.
            SetPrivateField(decoy, "_spawnAttempted", true);

            // Wall-clock, not frames: batchmode runs frames far faster than real time, so a frame
            // count would let a beat that had already moved on slip through.
            yield return new WaitForSecondsRealtime(0.4f);

            Assert.IsTrue(dialoguePanel.activeSelf,
                "Beat 2's glyph rule must be on screen before anything else happens. A blank "
                + "screen here means the universal rule was skipped or ran too late to be beat 2.");
            Assert.IsTrue(GetPrivateField<bool>(decoy, "_introductionHold"),
                "The ability must still be HELD while the glyph rule is being read. A released "
                + "hold here means the beat has reached beat 3 and the split fires under the rule "
                + "that is supposed to precede it, which is the ordering this beat was rewritten "
                + "to fix.");
            Assert.IsFalse(((IIntroducibleAbility)decoy).HasFiredThisSpawn,
                "Nothing may have fired yet: beat 2 is copy, beat 3 is the ability.");
            Assert.AreEqual(0f, _cardGroup.alpha,
                "The name card belongs to beat 6 and must not be up during beat 2.");
            Assert.IsTrue(IsBadgeHidden(badgeRenderer),
                "Beat 2 states the rule while THIS enemy's mark is still hidden — rule first, "
                + "instance later. Revealing the badge here spends beat 7 four beats early.");

            // The player taps through the line.
            CompleteDialogue(dialogue);

            bool abilityReleased = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!abilityReleased && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                abilityReleased = !GetPrivateField<bool>(decoy, "_introductionHold");
            }

            Assert.IsTrue(abilityReleased,
                "Beat 3 must follow beat 2: once the glyph rule has cleared, the ability is let go "
                + "and the split happens. A hold that is never released hangs the lesson.");
            Assert.IsTrue(IsBadgeHidden(badgeRenderer),
                "The badge stays hidden into beat 3 as well — beat 7 is the only reveal.");
        }

        // ------------------------------------------------------------------------------------
        // 4c-3. Blank glyph-rule copy no-ops, exactly like the other lines
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// A lesson that leaves <c>glyphRuleLine</c> unauthored must play exactly as it did before
        /// the beat existed. <c>OnboardingDialogueRunner</c> already no-ops on blank copy — that is
        /// how <c>restorationLine</c> stays optional — and beat 2 sits in front of everything else
        /// in the lesson, so a blank line that waited on a completion nobody will ever raise would
        /// not degrade the lesson, it would wedge it shut before the ability ever fired.
        ///
        /// <para>
        /// The DialogueController is present on purpose. With none in the scene the runner bails on
        /// the null controller and a blocking blank line would be indistinguishable from a
        /// well-behaved one, so the test would pass for the wrong reason.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator Beat2GlyphRule_BlankCopy_DoesNotBlockTheLesson()
        {
            yield return null;

            GiveTheDecoyAPoolToDrawFrom();
            (_, GameObject dialoguePanel) = GiveTheLessonADialogueSurface();

            BaybayinCharacterSO eiChar = MakeCharacter("EI", "symbol.test.blankrule.ei");
            BaybayinCharacterSO naChar = MakeCharacter("NA", "symbol.test.blankrule.na");

            EnemyDataSO iligawData = CreateEnemyData(
                "test_iligaw_blankrule", "Iligaw", eiChar, spawnsMirrorDecoy: true);
            // CreateIligawShapedLesson leaves glyphRuleLine at its default: blank text, no
            // DialogueSO. That IS the case under test; nothing here authors it.
            EnemyLessonSO lesson = CreateIligawShapedLesson(iligawData);
            lesson.abilityBeatSeconds = 0.05f;
            Assert.IsTrue(string.IsNullOrWhiteSpace(lesson.glyphRuleLine.fallbackText)
                && lesson.glyphRuleLine.dialogue == null,
                "setup: this test means nothing unless the glyph rule is genuinely unauthored.");

            FocusWordDefinition word = CreateWord(
                "level.test.blankrule.ina", "ina", "INA", eiChar, naChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { iligawData }, new[] { lesson },
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);

            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);

            Enemy iligaw = CreateEnemyShell("Iligaw_BlankRule");
            Assert.IsTrue(iligaw.Initialize(iligawData));
            Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, iligaw.IntroductionOutcome,
                "setup: requiredRestoredSlots = 0 means this spawn is the lesson, ability armed.");

            MirrorDecoyController decoy = iligaw.GetComponent<MirrorDecoyController>();
            Assert.IsNotNull(decoy, "setup: spawnsMirrorDecoy should attach MirrorDecoyController.");
            // As above: keep Update away from a pool that was never woken. The hold is what this
            // test watches, and _spawnAttempted does not touch it.
            SetPrivateField(decoy, "_spawnAttempted", true);

            // Nothing is ever tapped through here. If a blank line waits on a completion, this
            // never finishes.
            bool abilityReleased = false;
            bool dialogueEverShown = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!abilityReleased && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                dialogueEverShown |= dialoguePanel.activeSelf;
                abilityReleased = !GetPrivateField<bool>(decoy, "_introductionHold");
            }

            Assert.IsTrue(abilityReleased,
                "A blank glyphRuleLine must no-op, exactly like a blank restorationLine. A lesson "
                + "that never reaches its ability beat is hung, not degraded.");
            Assert.IsFalse(dialogueEverShown,
                "Blank copy must put nothing on screen — an empty dialogue box counts as a beat "
                + "the player has to dismiss.");
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
        /// <summary>
        /// The lesson must not begin against an empty field.
        ///
        /// <para>
        /// <b>The defect.</b> The wave spawner releases enemies ABOVE the visible play area and
        /// lets them walk in. The beat halted its subject on the settle frame, which on a first
        /// spawn is that off-screen release height — measured live at y = 11.40 against a camera
        /// seeing to y = 10.03. All eight beats then played against nothing: the vignette dimmed an
        /// empty lane, the mirror copy appeared and was never seen, the glyph reveal was clipped by
        /// the screen edge, and the card named an enemy the player had never seen. Every existing
        /// test in this fixture passed throughout, because none of them had a camera.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator LessonWaitsForTheEnemyToWalkIntoTheCameraView_BeforeHaltingIt()
        {
            yield return null;

            // Level 1's measured camera: orthographic, size 10.025, at the origin.
            GameObject cameraGO = CreateTracked("LessonFramingCamera");
            cameraGO.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraGO.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10.025f;
            SetPrivateField(_beat, "_worldCamera", camera);

            BaybayinCharacterSO iChar = MakeCharacter("I", "symbol.test.framing.i");
            EnemyDataSO iligawData = CreateEnemyData(
                "test_iligaw_framing", "Iligaw", iChar, spawnsMirrorDecoy: false);
            EnemyLessonSO lesson = CreateIligawShapedLesson(iligawData);
            lesson.abilityBeatSeconds = 30f;

            FocusWordDefinition word = CreateWord("level.test.framing.ina", "ina", "INA", iChar, iChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { iligawData }, new[] { lesson },
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);
            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);

            Enemy iligaw = CreateEnemyShell("Iligaw_Framing");

            // The spawner's release height, above the top of the frame.
            iligaw.transform.position = new Vector3(0f, 11.40f, 0f);
            Assert.IsTrue(iligaw.Initialize(iligawData));
            Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, iligaw.IntroductionOutcome,
                "setup: this must be a real lesson spawn.");

            yield return null;
            yield return null;

            Assert.AreEqual(1f, Time.timeScale,
                "The beat must not drop the time scale while its subject is above the frame.");
            Assert.IsFalse(_vignette.IsVisible,
                "The beat must not dim the field around an enemy nobody can see.");

            // It walks in.
            iligaw.transform.position = new Vector3(0f, 5f, 0f);
            yield return null;
            yield return null;

            Assert.AreNotEqual(1f, Time.timeScale,
                "Once the enemy is inside the camera's view the beat halts it and time slows.");
            Assert.IsTrue(_vignette.IsVisible,
                "…and the vignette comes up around something the player can actually see.");

            EnemyMover mover = iligaw.GetComponent<EnemyMover>();
            Assert.IsFalse(mover.IsMoving, "Beat 1 halts the enemy where the player can see it.");

            _beat.enabled = false;
            yield return null;
        }

        /// <summary>
        /// The lesson must not begin against a subject the HUD is drawn on top of.
        ///
        /// <para>
        /// <b>The defect, measured.</b> The previous fix halted the enemy the instant its bounds
        /// cleared <c>cameraTop - margin</c>, so it always stopped in the top 11-14 % of the screen
        /// — the band <c>ActiveCluePanel</c> owns, and the clue panel is drawn in FRONT of the lane.
        /// Live reads: at 1284x2778 the enemy halted at y = 9.51 against a camera seeing to 12.17;
        /// at the shipped 900x1604 portrait aspect it halted at y = 7.29 against a camera seeing to
        /// 10.03. Both are inside the camera and both are behind the HUD — at 900x1604 only a wedge
        /// of the enemy's head showed above the panel, and beat 2's before/after split frames were
        /// the same picture.
        /// </para>
        ///
        /// <para>
        /// <b>The negative control matters here.</b> The old camera-only predicate is asserted to
        /// answer TRUE at the very height this test rejects, so "not framed" cannot be passing
        /// against a rule that rejects everything.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator LessonHaltsTheEnemyBelowTheCluePanel_AtTheNarrowShippedAspect()
        {
            yield return null;

            // The 900x1604 run's measured camera: orthographic, size 10.025, at the origin. The
            // aspect is forced to that run's, though it cannot change the answer — an orthographic
            // camera's screen-y-to-world-y mapping reads only its pixel height and ortho size.
            GameObject cameraGO = CreateTracked("LessonHudFramingCamera");
            cameraGO.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraGO.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10.025f;
            camera.aspect = 900f / 1604f;
            SetPrivateField(_beat, "_worldCamera", camera);

            // The clue panel, as a band across the top of the screen. 0.7857 of the screen height is
            // where ActiveCluePanel's authored bottom edge (+550 in 1080x1920 HUD units) lands at
            // 900x1604 — the aspect the visual check found the defect at.
            RectTransform cluePanel = CreateTopBandPanel("ActiveCluePanel_Test", 0.7857f);
            SetPrivateField(_beat, "_hudOcclusionRect", cluePanel);
            SetPrivateField(_beat, "_onScreenWaitTimeoutSeconds", 60f);
            yield return null;

            Assert.IsTrue(
                EnemyIntroductionBeat.TryGetCameraWorldRect(camera, out Rect view),
                "setup: the framing rule needs an orthographic camera rect.");
            float ceiling = EnemyIntroductionBeat.ResolveHaltCeilingWorldY(
                camera, view, cluePanel, 0.25f);
            Assert.Less(ceiling, view.yMax,
                "setup: the HUD band must actually lower the ceiling below the camera's top edge, "
                + "or this test proves nothing about HUD awareness.");

            BaybayinCharacterSO iChar = MakeCharacter("I", "symbol.test.hudframing.i");
            EnemyDataSO iligawData = CreateEnemyData(
                "test_iligaw_hudframing", "Iligaw", iChar, spawnsMirrorDecoy: false);
            EnemyLessonSO lesson = CreateIligawShapedLesson(iligawData);
            lesson.abilityBeatSeconds = 30f;

            FocusWordDefinition word = CreateWord(
                "level.test.hudframing.ina", "ina", "INA", iChar, iChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { iligawData }, new[] { lesson },
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);
            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);

            Enemy iligaw = CreateEnemyShell("Iligaw_HudFraming");
            iligaw.transform.position = new Vector3(0f, 11.40f, 0f);
            Assert.IsTrue(iligaw.Initialize(iligawData));
            Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, iligaw.IntroductionOutcome,
                "setup: this must be a real lesson spawn.");

            yield return null;
            yield return null;

            // The old halt height at this aspect, live-measured. Inside the camera, behind the HUD.
            iligaw.transform.position = new Vector3(0f, 7.29f, 0f);
            yield return null;
            yield return null;

            Assert.IsTrue(
                EnemyIntroductionBeat.IsVerticallyInsideView(
                    camera, new Bounds(iligaw.transform.position, Vector3.zero), 0.35f),
                "negative control: the old camera-only rule considered y = 7.29 framed. If this "
                + "fails, the test below is passing for the wrong reason.");
            Assert.AreEqual(1f, Time.timeScale,
                "The beat must not drop the time scale while its subject is parked behind the "
                + "clue panel — on camera is not the same as visible.");
            Assert.IsFalse(_vignette.IsVisible,
                "The beat must not spotlight a subject the clue panel is drawn over.");

            // It keeps walking, down past the HUD's band.
            iligaw.transform.position = new Vector3(0f, ceiling - 1f, 0f);
            yield return null;
            yield return null;

            Assert.AreNotEqual(1f, Time.timeScale,
                "Below the clue panel's occluded band the beat halts its subject and time slows.");
            Assert.IsTrue(_vignette.IsVisible,
                "…and the vignette finally closes around something the player can see.");
            Assert.Less(iligaw.transform.position.y, ceiling,
                "The halt position must be below the clue panel's occluded band.");

            _beat.enabled = false;
            yield return null;
        }

        /// <summary>
        /// The HUD-derived ceiling must land in the same place at both aspects the visual check
        /// exercised. The canvas scaler ties the HUD to the world's constant width, so the band the
        /// clue panel occupies is a different FRACTION of each screen and the same world y — which
        /// is the whole reason the rule is derived from the live rect rather than from a fraction.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHudCeilingLandsAtTheSameWorldHeight_AtBothCheckedAspects()
        {
            yield return null;

            GameObject cameraGO = CreateTracked("LessonAspectCamera");
            cameraGO.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraGO.AddComponent<Camera>();
            camera.orthographic = true;

            // 1284x2778: ortho size 12.170, clue panel bottom at 0.7354 of the screen height.
            camera.orthographicSize = 12.170f;
            camera.aspect = 1284f / 2778f;
            RectTransform tallPanel = CreateTopBandPanel("CluePanel_1284x2778", 0.7354f);
            yield return null;
            Assert.IsTrue(EnemyIntroductionBeat.TryGetCameraWorldRect(camera, out Rect tallView));
            float tallCeiling = EnemyIntroductionBeat.ResolveHaltCeilingWorldY(
                camera, tallView, tallPanel, 0.25f);

            // 900x1604: ortho size 10.025, clue panel bottom at 0.7857 of the screen height.
            camera.orthographicSize = 10.025f;
            camera.aspect = 900f / 1604f;
            RectTransform narrowPanel = CreateTopBandPanel("CluePanel_900x1604", 0.7857f);
            yield return null;
            Assert.IsTrue(EnemyIntroductionBeat.TryGetCameraWorldRect(camera, out Rect narrowView));
            float narrowCeiling = EnemyIntroductionBeat.ResolveHaltCeilingWorldY(
                camera, narrowView, narrowPanel, 0.25f);

            Assert.That(narrowCeiling, Is.EqualTo(tallCeiling).Within(0.1f),
                "The HUD-derived halt ceiling must agree across aspects. Measured: "
                + $"1284x2778 -> {tallCeiling:0.###}, 900x1604 -> {narrowCeiling:0.###}.");
            Assert.Less(narrowCeiling, narrowView.yMax - 4f,
                "The ceiling must sit well below the camera's top edge, in the playfield, not at "
                + "the screen edge where the HUD is.");
            Assert.Greater(narrowCeiling, 0f,
                "…and in the UPPER part of the playfield, not halfway down it.");
        }

        /// <summary>
        /// Beat 8 must not hold the wave schedule open behind a prompt that can no longer be
        /// satisfied.
        ///
        /// <para>
        /// <b>The defect.</b> In one observed run the introduced Iligaw walked into the shrine
        /// instead of being drawn. The enemy count went to zero and the beat sat
        /// <c>IsPlaying = True</c>, <c>IsHoldingSpawnSchedule = True</c> for 135 seconds and
        /// counting: prompt on screen, no enemies, no spawns, nothing to do. The level was
        /// unplayable until the player happened to draw the right glyph at nothing.
        /// </para>
        ///
        /// <para>
        /// The wait-forever half is asserted first, deliberately: if the beat had already finished
        /// on its own the release below would prove nothing.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator Beat8_ReleasesTheSpawnHold_WhenItsSubjectLeavesWithoutBeingDrawn()
        {
            yield return null;

            // Every authored hold to zero: this test is about what happens at beat 8, and the beats
            // before it are already covered elsewhere in this fixture.
            SetPrivateField(_beat, "_nameStepSeconds", 0f);
            SetPrivateField(_beat, "_abilityStepSeconds", 0f);
            SetPrivateField(_beat, "_drawSuccessHoldSeconds", 0f);
            // No camera in this test, and none wanted: step 0's framing wait is covered above.
            SetPrivateField(_beat, "_onScreenWaitTimeoutSeconds", 0f);

            BaybayinCharacterSO iChar = MakeCharacter("I", "symbol.test.beat8.i");
            EnemyDataSO iligawData = CreateEnemyData(
                "test_iligaw_beat8", "Iligaw", iChar, spawnsMirrorDecoy: false);
            EnemyLessonSO lesson = CreateIligawShapedLesson(iligawData);
            lesson.abilityBeatSeconds = 0f;
            lesson.drawStep = CreateDrawStep(iChar);

            FocusWordDefinition word = CreateWord("level.test.beat8.ina", "ina", "INA", iChar, iChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { iligawData }, new[] { lesson },
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);
            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);

            Enemy iligaw = CreateEnemyShell("Iligaw_Beat8");
            iligaw.transform.position = Vector3.zero;
            Assert.IsTrue(iligaw.Initialize(iligawData));
            Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, iligaw.IntroductionOutcome,
                "setup: this must be a real lesson spawn.");

            // Beat 2 warns when the lesson arms an ability the spawn does not carry; this fixture's
            // shell deliberately carries none, so the warning is expected and is not the subject.
            LogAssert.ignoreFailingMessages = true;

            // Run the lesson down to beat 8's wait.
            for (int frame = 0; frame < 60; frame++)
                yield return null;

            Assert.IsTrue(EnemyIntroductionBeat.IsHoldingSpawnSchedule,
                "setup: the beat must still be parked at beat 8 waiting for the draw. If it has "
                + "already finished, the release assertion below proves nothing.");

            // The subject leaves without being drawn: it reached the shrine, or its pooled shell
            // was recycled. Either way the identity beat 8 is waiting on is gone.
            iligaw.gameObject.SetActive(false);

            for (int frame = 0; frame < 10; frame++)
                yield return null;

            Assert.IsFalse(EnemyIntroductionBeat.IsHoldingSpawnSchedule,
                "Beat 8 must notice its subject is gone and release the spawn hold. Left holding, "
                + "the level sits dead: no enemies, no spawns, and nothing the player can do.");
            Assert.IsFalse(EnemyIntroductionBeat.IsPlaying,
                "…and the lesson must stop claiming the screen with it.");
            Assert.AreEqual(1f, Time.timeScale,
                "…and hand real time back.");

            LogAssert.ignoreFailingMessages = false;
            _beat.enabled = false;
            yield return null;
        }

        /// <summary>
        /// The heart-loss demo's stand-in is a scripted prop, not a first meeting.
        ///
        /// <para>
        /// <b>The defect.</b> In one observed run the introduction machinery fired for Hati DURING
        /// the heart-loss demo — a full card, time slow and vignette, at t = 80.5-87.7 inside the
        /// tutorial preamble, on the enemy the player is being shown losing to. Worse than the
        /// cosmetic problem, it SPENT Hati's campaign-wide one-shot, so the type could never
        /// introduce itself properly again.
        /// </para>
        ///
        /// <para>
        /// The negative control is the second half: the same spawn, with the demo window closed,
        /// must still be introducible. Without it "declined" could be passing against a rule that
        /// declines everything.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator HeartLossDemoSpawn_ClaimsNoIntroduction_AndSpendsNoOneShot()
        {
            yield return null;

            SetPrivateField(_beat, "_onScreenWaitTimeoutSeconds", 0f);

            BaybayinCharacterSO hChar = MakeCharacter("HA", "symbol.test.demo.ha");
            EnemyDataSO hatiData = CreateEnemyData("test_hati_demo", "Hati", hChar);

            FocusWordDefinition word = CreateWord("level.test.demo.ina", "ina", "INA", hChar, hChar);
            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO> { hatiData },
                System.Array.Empty<EnemyLessonSO>(),
                new List<FocusWordDefinition> { word });
            _gameManager.SetLevel(config);
            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);

            TutorialRuntimeState.SetHeartLossDemoActive(true);

            Enemy demoStandIn = CreateEnemyShell("Hati_HeartLossDemoStandIn");
            Assert.IsTrue(demoStandIn.Initialize(hatiData));

            Assert.AreEqual(IntroductionOutcome.None, demoStandIn.IntroductionOutcome,
                "The heart-loss demo's stand-in must never claim its type's introduction. None, "
                + "not a suppressing outcome: the prop's ability stays exactly as armed as it "
                + "would have been with no introduction machinery in the scene at all.");
            Assert.IsFalse(EnemyIntroductionProgress.HasBeenIntroduced(hatiData),
                "…and declining must not spend the type's one-shot, or Hati can never be "
                + "introduced properly for the rest of the campaign.");

            yield return null;
            yield return null;

            Assert.IsFalse(EnemyIntroductionBeat.IsPlaying,
                "No card may play over the heart-loss demo.");
            Assert.AreEqual(1f, Time.timeScale,
                "…and the demo's own pacing must not be taken over by an introduction's time slow.");

            // Negative control: the same type, the same spawn, outside the demo window.
            TutorialRuntimeState.SetHeartLossDemoActive(false);

            Enemy realSpawn = CreateEnemyShell("Hati_RealFirstSpawn");
            Assert.IsTrue(realSpawn.Initialize(hatiData));

            // IntroduceAndSuppress, not IntroduceAndArm: Hati has no authored lesson, and the
            // standing rule is that a type's ability is inert on the spawn that introduces it.
            // What matters here is that the type WAS introducible.
            Assert.AreEqual(IntroductionOutcome.IntroduceAndSuppress, realSpawn.IntroductionOutcome,
                "With the demo window closed the type must still be introducible — the one-shot "
                + "was never spent.");
            Assert.IsTrue(EnemyIntroductionProgress.HasBeenIntroduced(hatiData),
                "…and this is the spawn that spends it.");

            _beat.enabled = false;
            yield return null;
        }

        // ------------------------------------------------------------------------------------
        // alwaysShowTutorial replays the lesson (+ required negative control)
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The reported defect: "when first time seeing the Iligaw, why is there no intro?" — asked
        /// by a player whose save already listed <c>iligaw</c> under
        /// <c>salinlahi.tutorial.enemy_introductions_shown</c>. Level 1 sets
        /// <c>alwaysShowTutorial</c>, so its pre-combat onboarding replayed on every visit while the
        /// lesson embedded in the same level's combat — gated only by the campaign-wide one-shot —
        /// stayed spent forever. The player got all the framing and silence where the teaching was.
        ///
        /// <para>
        /// <b>The negative control is the load-bearing half.</b> A replay rule with no control is
        /// indistinguishable from "the lesson always plays", which is a worse bug than the one being
        /// fixed: every level would re-teach its lesson to a player who has known the type for
        /// hours. The control uses a DIFFERENT type from the main case on purpose — sharing one
        /// would let the per-attempt budget, rather than the flag, be what declines it, and the
        /// assertion would pass against a rule that never reads the flag at all.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AlwaysShowTutorialLevel_ReplaysTheLesson_ForAnAlreadyIntroducedType()
        {
            yield return null;

            SetPrivateField(_beat, "_onScreenWaitTimeoutSeconds", 0f);

            // --- Main case: alwaysShowTutorial = true, type already introduced. ---
            BaybayinCharacterSO eiChar = MakeCharacter("EI", "symbol.test.replay.ei");
            EnemyDataSO iligawData = CreateEnemyData(
                "test_iligaw_replay", "Iligaw", eiChar, spawnsMirrorDecoy: true);
            EnemyLessonSO iligawLesson = CreateIligawShapedLesson(iligawData);

            LevelConfigSO replayConfig = CreateLevelConfig(
                new List<EnemyDataSO> { iligawData },
                new[] { iligawLesson },
                new List<FocusWordDefinition>());
            replayConfig.alwaysShowTutorial = true;
            _gameManager.SetLevel(replayConfig);

            // Exactly the save state the defect was reported from.
            Assert.IsTrue(EnemyIntroductionProgress.TryClaimIntroduction(iligawData),
                "setup: spend Iligaw's campaign-wide one-shot, as the reported save had");
            Assert.IsTrue(EnemyIntroductionProgress.HasBeenIntroduced(iligawData),
                "setup precondition: the type must read as already introduced");

            Enemy replaySpawn = CreateEnemyShell("Iligaw_Replay");
            Assert.IsTrue(replaySpawn.Initialize(iligawData));

            Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, replaySpawn.IntroductionOutcome,
                "On a level whose config sets alwaysShowTutorial, the authored lesson must play "
                + "again even though the type's campaign-wide introduction is already spent — the "
                + "lesson IS that level's tutorial, and the rest of it already replays.");

            // Once per PLAY, not once per spawn: the second Iligaw of the same attempt is an
            // ordinary enemy, or the level would re-run its lesson on every wave.
            Enemy secondSpawn = CreateEnemyShell("Iligaw_SecondSpawnSameAttempt");
            Assert.IsTrue(secondSpawn.Initialize(iligawData));

            Assert.AreEqual(IntroductionOutcome.None, secondSpawn.IntroductionOutcome,
                "The replay is once per level attempt. A second spawn in the same play must be an "
                + "ordinary enemy, not a second lesson.");

            // --- Negative control: alwaysShowTutorial = false, type already introduced. ---
            BaybayinCharacterSO haChar = MakeCharacter("HA", "symbol.test.replay.ha");
            EnemyDataSO hatiData = CreateEnemyData(
                "test_hati_noreplay", "Hati", haChar, spawnsMirrorDecoy: true);
            EnemyLessonSO hatiLesson = CreateIligawShapedLesson(hatiData);

            LevelConfigSO onceOnlyConfig = CreateLevelConfig(
                new List<EnemyDataSO> { hatiData },
                new[] { hatiLesson },
                new List<FocusWordDefinition>());
            onceOnlyConfig.alwaysShowTutorial = false;
            _gameManager.SetLevel(onceOnlyConfig);

            Assert.IsTrue(EnemyIntroductionProgress.TryClaimIntroduction(hatiData),
                "setup: spend the control type's one-shot too");

            Enemy controlSpawn = CreateEnemyShell("Hati_NoReplay");
            Assert.IsTrue(controlSpawn.Initialize(hatiData));

            Assert.AreEqual(IntroductionOutcome.None, controlSpawn.IntroductionOutcome,
                "Negative control: without alwaysShowTutorial the one-shot must still be final. "
                + "Without this case the assertion above could be passing against a rule that "
                + "replays every lesson on every level, which is the worse bug.");

            _beat.enabled = false;
            yield return null;
        }

        // ------------------------------------------------------------------------------------

        /// <summary>
        /// A screen-space-overlay canvas with one full-width band pinned to the top of the screen,
        /// standing in for the HUD rect that occludes the top of the playfield.
        /// </summary>
        private RectTransform CreateTopBandPanel(string name, float bottomViewportY)
        {
            GameObject canvasGO = CreateTracked(name + "_Canvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject panelGO = new GameObject(name, typeof(RectTransform));
            panelGO.transform.SetParent(canvasGO.transform, false);

            RectTransform panel = panelGO.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0f, bottomViewportY);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            return panel;
        }

        private Level1TutorialStepSO CreateDrawStep(BaybayinCharacterSO target)
        {
            var step = ScriptableObject.CreateInstance<Level1TutorialStepSO>();
            step.promptText = "Draw E/I. Follow the guide.";
            step.successText = "Great job. Drawing protects the base.";
            step.targetCharacter = target;
            _objectsToDestroy.Add(step);
            return step;
        }

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

        /// <summary>
        /// Satisfies <c>MirrorDecoyController.CanFireThisSpawn</c>, which asks only whether an
        /// <c>EnemyPool</c> exists at all.
        ///
        /// <para>
        /// Built on a GameObject that is never activated, so <c>EnemyPool.Awake</c> never runs — a
        /// woken pool with no prefab assigned logs an error, which NUnit would fail the test on, and
        /// none of these tests wants a pool that can actually hand out enemies. The singleton field
        /// is set directly instead, which is exactly the fact under test: the dependency is
        /// reachable. <c>TearDown</c> clears it.
        /// </para>
        /// </summary>
        private EnemyPool GiveTheDecoyAPoolToDrawFrom()
        {
            GameObject go = CreateTracked("EnemyPool_Level1LessonTests");
            go.SetActive(false);
            EnemyPool pool = go.AddComponent<EnemyPool>();
            SetSingletonInstance(pool);
            Assert.IsNotNull(EnemyPool.Instance, "setup: the decoy's dependency must be reachable.");
            return pool;
        }

        /// <summary>
        /// A DialogueController the lesson's copy beats can actually play through, plus the overlay
        /// panel whose active state is the only observable "a line is on screen" this fixture needs.
        /// <c>OnboardingDialogueRunner</c> bails on a null controller, so without one every copy
        /// beat silently no-ops and an ordering test proves nothing.
        /// </summary>
        private (DialogueController, GameObject) GiveTheLessonADialogueSurface()
        {
            DialogueController dialogue = CreateComponent<DialogueController>(
                "DialogueController_Level1LessonTests");

            // A parent Canvas, because Play() promotes the panel to its own overrideSorting canvas
            // and a nested canvas with no parent canvas is a warning nobody needs to read.
            Canvas rootCanvas = dialogue.gameObject.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject panel = new GameObject("DialogueOverlay");
            panel.transform.SetParent(dialogue.transform, false);
            panel.AddComponent<RectTransform>();
            panel.SetActive(false);
            SetPrivateField(dialogue, "_overlayPanel", panel);

            return (dialogue, panel);
        }

        /// <summary>
        /// Stands in for the player tapping through the last line. <c>EndDialogue</c> is the same
        /// path the tap-catcher takes — it hides the overlay, lifts the dialogue pause and raises
        /// the completion the runner is waiting on — so completing this way leaves the controller
        /// in the state the next beat's line expects, which raising the bus event alone would not.
        /// </summary>
        private static void CompleteDialogue(DialogueController dialogue)
        {
            MethodInfo endDialogue = typeof(DialogueController).GetMethod(
                "EndDialogue", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(endDialogue,
                "DialogueController.EndDialogue is the seam this fixture completes a line through.");
            endDialogue.Invoke(dialogue, null);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"expected a private field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static bool IsBadgeVisible(SpriteRenderer badgeRenderer) =>
            badgeRenderer != null && badgeRenderer.enabled && badgeRenderer.color.a > 0.5f;

        private static bool IsBadgeHidden(SpriteRenderer badgeRenderer) =>
            badgeRenderer != null && badgeRenderer.color.a < 0.5f;

        // ------------------------------------------------------------------------------------
        // Heart-loss demo: the stand-in carries no glyph
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The heart-loss demo enemy exists to walk through and cost a heart. A glyph badge over
        /// its head says "draw this to stop me", which is the opposite of what the beat is
        /// teaching, and — because nothing locks drawing input during the beat — it was not even a
        /// lie: <c>CombatResolver</c> matches on the assigned character alone, so the player could
        /// kill the prop mid-walk and, on Level 1, split Hati on top of the shrine.
        ///
        /// <para>
        /// The restore is asserted through the COMPONENT-DISABLE path rather than through a clean
        /// finish, because that is the path that actually breaks: a coroutine stopped by Unity
        /// never runs its <c>finally</c>, so a beat that only unwound there would leave a live
        /// enemy unmarked and unkillable for the rest of the level.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator HeartLossDemoEnemy_CarriesNoGlyph_AndIsRestoredOnAnAbortedDemo()
        {
            yield return null;

            BaybayinCharacterSO demoChar = MakeCharacter("MA", "symbol.test.heartdemo.ma");
            EnemyDataSO demoData = CreateEnemyData("test_heartdemo", "Hati", demoChar);

            Enemy demoEnemy = CreateEnemyShell("HeartLossDemo_Enemy");
            (_, SpriteRenderer badgeRenderer) = GlyphBadgePlayModeTestHelpers
                .AddGlyphBadgeChild(demoEnemy.gameObject, GlyphBadgePlayModeTestHelpers.CreateBadgeConfig());
            Assert.IsTrue(demoEnemy.Initialize(demoData));
            Assert.IsNotNull(demoEnemy.GlyphBadge, "setup: the demo enemy must have a badge to hide.");

            GameObject host = CreateTracked("HeartLossDemoBeatHost");
            var beat = host.AddComponent<HeartLossDemoBeat>();
            yield return null;

            InvokePrivateVoid(beat, "ClaimDemoEnemy", demoEnemy);

            Assert.IsTrue(IsBadgeHidden(badgeRenderer),
                "The heart-loss demo enemy must carry no glyph: a badge implies the player could "
                + "have stopped it, and the beat's whole point is that they could not.");
            Assert.IsTrue(demoEnemy.IsResolutionBlocked,
                "The demo enemy must not be killable by drawing its glyph while the demo runs — "
                + "a dead prop mid-walk is a demo that never reaches the base.");

            // The abort: Unity disabling the component, which drops the beat's coroutine without
            // unwinding it.
            beat.enabled = false;
            yield return null;

            Assert.Greater(badgeRenderer.color.a, 0.5f,
                "An aborted demo must hand the badge back — an enemy left permanently unmarked is "
                + "the exact bug this project has already shipped once.");
            Assert.IsFalse(demoEnemy.IsResolutionBlocked,
                "An aborted demo must lift the resolution block, or the enemy is unkillable for "
                + "the rest of the level.");
        }

        // ------------------------------------------------------------------------------------
        // Restoration rail: the glyph's flight into its box
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// The flight is presentation and nothing else, so the rail it leaves behind has to be the
        /// rail an un-animated fill would have left. If it is not, a slot restored while the player
        /// was looking somewhere else ends up in a different state from one they watched land.
        /// </summary>
        [UnityTest]
        public IEnumerator SlotGlyphFlight_LeavesTheSameEndState_AsAnImmediateFill()
        {
            yield return null;

            RailFixture rail = BuildRailFixture("flightend");

            // The immediate fill: apply the symbol and repaint, with no flight involved at all.
            _presenter.RestorationState.Apply(rail.First.stableId);
            InvokePrivateVoid(_presenter, "RepaintRail", false);
            yield return null;

            string immediate = DescribeSlot(0);

            // Now the same box, filled again with the flight running. A repeat Apply is idempotent,
            // so the state the rail is repainted from is identical; only the presentation differs.
            _presenter.RestorationState.Apply(rail.First.stableId);
            InvokePrivateVoid(_presenter, "RepaintRail", false);
            InvokePrivateVoid(
                _presenter,
                "LaunchSlotGlyphFlights",
                rail.First.stableId,
                new Vector3(0f, 3f, 0f));

            Assert.IsTrue(HasFlierInFlight(0),
                "setup: the flight must actually have started, or this test proves nothing about "
                + "what happens after one.");

            // Unscaled, because the flight is unscaled — generously past the ~0.70s budget.
            float waited = 0f;
            while (waited < 1.5f && HasFlierInFlight(0))
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsFalse(HasFlierInFlight(0),
                "The flight must finish and clean up its flier on its own inside the budget.");

            Assert.AreEqual(immediate, DescribeSlot(0),
                "A box the player watched the glyph fly into must end up identical to a box that "
                + "simply filled. Anything else means the animation owns part of the rest state.");
        }

        /// <summary>
        /// The trap this project has already been bitten by: Unity does NOT run a coroutine's
        /// <c>finally</c> when it stops the coroutine. The flight parks the resting glyph hidden
        /// while its flier stands in, so a flight killed mid-air with no external repair would
        /// leave a permanently blank box that the restoration state insists is filled.
        ///
        /// <para>
        /// Killed with <c>StopAllCoroutines</c> rather than by disabling the presenter, and that is
        /// the harder case on purpose. Disabling runs <c>OnDisable</c>, which repairs the slots by
        /// hand and then tears the rail down, so afterwards there is no rail left to be wrong.
        /// Stopping the coroutines leaves the presenter, the rail, the slot and the flier all alive
        /// and structurally perfect — the only thing that has happened is that the glyph stopped
        /// moving. Nothing about the scene graph reveals it, so only the flight's deadline can.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator SlotGlyphFlight_AbortedMidAir_StillLeavesTheBoxFilled()
        {
            yield return null;

            RailFixture rail = BuildRailFixture("flightabort");

            _presenter.RestorationState.Apply(rail.First.stableId);
            InvokePrivateVoid(_presenter, "RepaintRail", false);
            InvokePrivateVoid(
                _presenter,
                "LaunchSlotGlyphFlights",
                rail.First.stableId,
                new Vector3(0f, 3f, 0f));

            yield return null;

            Assert.IsTrue(HasFlierInFlight(0),
                "setup: the glyph must be in the air before the abort, or there is nothing to "
                + "strand.");
            Assert.IsFalse(GetSlotGlyph(0).gameObject.activeSelf,
                "setup: the resting glyph must be the one standing aside for the flier — that is "
                + "the state the abort has to repair.");

            // The abort: the routine dropped where it stood, with no tail run.
            _presenter.StopAllCoroutines();

            float waited = 0f;
            while (waited < 2.5f && HasFlierInFlight(0))
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsFalse(HasFlierInFlight(0),
                "An aborted flight must not leave its flier behind in the rail. Nothing was going "
                + "to notice this one structurally — the deadline is what has to catch it.");

            Image glyph = GetSlotGlyph(0);
            Assert.IsTrue(glyph.gameObject.activeSelf,
                "An aborted flight must hand the box its glyph back. A box the state calls "
                + "restored and the player sees empty is the bug, not the animation.");
            Assert.AreEqual(Vector3.one, glyph.transform.localScale,
                "An aborted flight must leave the glyph at resting scale, not frozen mid-squash.");
        }

        /// <summary>
        /// Two fills landing on the same box in quick succession must not put two gliding glyphs on
        /// one slot, and must not let the first one's cleanup blank the box the second just filled.
        /// </summary>
        [UnityTest]
        public IEnumerator SlotGlyphFlight_SecondFillOnTheSameBox_RetiresTheFirstRatherThanRacingIt()
        {
            yield return null;

            RailFixture rail = BuildRailFixture("flightqueue");

            _presenter.RestorationState.Apply(rail.First.stableId);
            InvokePrivateVoid(_presenter, "RepaintRail", false);
            InvokePrivateVoid(
                _presenter, "LaunchSlotGlyphFlights", rail.First.stableId, new Vector3(0f, 3f, 0f));
            yield return null;

            GameObject firstFlier = GetSlotFlier(0);
            Assert.IsNotNull(firstFlier, "setup: the first flight must be in the air.");

            InvokePrivateVoid(
                _presenter, "LaunchSlotGlyphFlights", rail.First.stableId, new Vector3(4f, 3f, 0f));

            GameObject secondFlier = GetSlotFlier(0);
            Assert.IsNotNull(secondFlier, "The second fill must get a flight of its own.");
            Assert.AreNotSame(firstFlier, secondFlier,
                "The first flier must be retired the instant a second fill claims the same box — "
                + "two gliding glyphs for one slot is the fight this guards against.");

            // Checked a frame later, not immediately: in play mode Destroy defers to the end of the
            // frame, so the retired flier is still parented here on the call that retired it.
            yield return null;

            Assert.AreEqual(1, CountFliersInRail(),
                "Exactly one glyph may be in the air for one box once the retired one has gone.");

            float waited = 0f;
            while (waited < 1.5f && HasFlierInFlight(0))
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(GetSlotGlyph(0).gameObject.activeSelf,
                "After both flights have settled the box must be filled, not blanked by the "
                + "retired one's cleanup.");
        }

        // ---- rail fixture helpers ----

        private readonly struct RailFixture
        {
            public RailFixture(BaybayinCharacterSO first, BaybayinCharacterSO second)
            {
                First = first;
                Second = second;
            }

            public BaybayinCharacterSO First { get; }
            public BaybayinCharacterSO Second { get; }
        }

        /// <summary>
        /// Stands up the minimum the rail needs to exist and be visible: a canvas to build into, a
        /// main camera for the world-to-screen hop the flight's origin needs, a two-syllable focus
        /// word, and the rail itself switched on.
        /// </summary>
        private RailFixture BuildRailFixture(string tag)
        {
            GameObject cameraGO = CreateTracked("RailFixtureCamera");
            var camera = cameraGO.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            cameraGO.tag = "MainCamera";

            GameObject canvasGO = CreateTracked("HUDCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();

            BaybayinCharacterSO first = MakeCharacter("I", $"symbol.test.{tag}.i");
            BaybayinCharacterSO second = MakeCharacter("NA", $"symbol.test.{tag}.na");
            first.almanacSprite = MakeTestSprite($"{tag}-i");
            second.almanacSprite = MakeTestSprite($"{tag}-na");

            FocusWordDefinition ina = CreateWord(
                $"word.test.{tag}.ina", "INA", "INA", first, second);

            LevelConfigSO config = CreateLevelConfig(
                new List<EnemyDataSO>(), System.Array.Empty<EnemyLessonSO>(),
                new List<FocusWordDefinition> { ina });
            config.activeClueRestorationEnabled = true;

            SetPrivateField(_presenter, "_level", config);
            _presenter.RestorationState.Configure(config.focusWords);

            InvokePrivateVoid(_presenter, "EnsureRestorationRail");

            var railRoot = GetPrivateField<GameObject>(_presenter, "_railRoot");
            Assert.IsNotNull(railRoot, "setup: the restoration rail must have been built.");
            railRoot.SetActive(true);

            return new RailFixture(first, second);
        }

        private Sprite MakeTestSprite(string name)
        {
            var texture = new Texture2D(4, 4);
            texture.name = name + "Texture";
            _objectsToDestroy.Add(texture);

            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            _objectsToDestroy.Add(sprite);
            return sprite;
        }

        private object GetRailSlot(int index)
        {
            FieldInfo field = typeof(ActiveCluePresenter).GetField(
                "_railSlots", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing ActiveCluePresenter._railSlots.");

            var slots = (System.Collections.IList)field.GetValue(_presenter);
            Assert.Greater(slots.Count, index, $"setup: the rail must have a slot {index}.");
            return slots[index];
        }

        private static T GetSlotField<T>(object slot, string fieldName) where T : class
        {
            FieldInfo field = slot.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(field, $"Missing RailSlot.{fieldName}.");
            return field.GetValue(slot) as T;
        }

        private Image GetSlotGlyph(int index) => GetSlotField<Image>(GetRailSlot(index), "Glyph");

        private GameObject GetSlotFlier(int index) =>
            GetSlotField<GameObject>(GetRailSlot(index), "Flier");

        private bool HasFlierInFlight(int index) => GetSlotFlier(index) != null;

        private int CountFliersInRail()
        {
            var railRoot = GetPrivateField<GameObject>(_presenter, "_railRoot");
            if (railRoot == null)
                return 0;

            int count = 0;
            foreach (Transform child in railRoot.transform)
            {
                if (child.name.Contains("GlyphFlier"))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Everything about a slot that the player can actually see, flattened to one string so the
        /// animated and un-animated end states can be compared as a whole rather than field by
        /// field — a comparison that only checks the fields someone remembered to check is how a
        /// difference in the tenth one gets shipped.
        /// </summary>
        private string DescribeSlot(int index)
        {
            object slot = GetRailSlot(index);
            var glyph = GetSlotField<Image>(slot, "Glyph");
            var plate = GetSlotField<Image>(slot, "Plate");
            var frame = GetSlotField<Image>(slot, "Frame");
            var label = GetSlotField<TMPro.TextMeshProUGUI>(slot, "Label");

            return string.Join(
                "|",
                $"glyphActive={glyph.gameObject.activeSelf}",
                $"glyphSprite={(glyph.sprite == null ? "none" : glyph.sprite.name)}",
                $"glyphColor={glyph.color}",
                $"glyphScale={glyph.transform.localScale}",
                $"plateActive={(plate == null ? "none" : plate.gameObject.activeSelf.ToString())}",
                $"plateColor={(plate == null ? "none" : plate.color.ToString())}",
                $"frameColor={frame.color}",
                $"labelText={(label == null ? "none" : label.text)}",
                $"labelColor={(label == null ? "none" : label.color.ToString())}",
                $"slotScale={GetSlotField<RectTransform>(slot, "Anchor").localScale}");
        }

        // ------------------------------------------------------------------------------------
        // Reflection helpers
        // ------------------------------------------------------------------------------------

        private static void InvokePrivateVoid(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing {target.GetType().Name}.{methodName}.");
            method.Invoke(target, args);
        }

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
