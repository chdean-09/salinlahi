using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public sealed class ActiveClueDirectorTests
    {
        private readonly List<Object> _objectsToDestroy = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

            // ActiveCluePresenter.EnsureRuntimePanel builds a HUD panel for an armed level.
            // The test never receives a handle to those objects, so sweep them by name or
            // they leak into every later test in the run.
            GameObject runtimePanel = GameObject.Find("[Runtime] ActiveCluePanel");
            while (runtimePanel != null)
            {
                Object.DestroyImmediate(runtimePanel);
                runtimePanel = GameObject.Find("[Runtime] ActiveCluePanel");
            }

            // Same problem for the channel-independent mark: the presenter parents it to
            // nothing so it survives the presenter unless it is swept by name.
            GameObject runtimeMark = GameObject.Find("[Runtime] ActiveClueMark");
            while (runtimeMark != null)
            {
                Object.DestroyImmediate(runtimeMark);
                runtimeMark = GameObject.Find("[Runtime] ActiveClueMark");
            }

            // The word-restoration label is built on the HUD canvas, which the presenter does
            // not own, so a deferred Destroy could otherwise outlive this test.
            GameObject restoredCue = GameObject.Find("[Runtime] WordRestoredCue");
            while (restoredCue != null)
            {
                Object.DestroyImmediate(restoredCue);
                restoredCue = GameObject.Find("[Runtime] WordRestoredCue");
            }

            // InstantWinPresenter.EnsureOverlay builds this canvas when the beat has no
            // parent canvas, and the tap catcher is parented to it — destroying the beat
            // alone strands both, and the loose canvas can hijack a later test's
            // FindFirstObjectByType<Canvas> HUD lookup.
            GameObject instantWinCanvas = GameObject.Find("[Runtime] InstantWinCanvas");
            while (instantWinCanvas != null)
            {
                Object.DestroyImmediate(instantWinCanvas);
                instantWinCanvas = GameObject.Find("[Runtime] InstantWinCanvas");
            }

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
            ChallengeRuntimeState.Clear();
        }

        [Test]
        public void SpawnSequence_IncreasesStrictlyAcrossInitializations()
        {
            EnemyDataSO data = CreateEnemyData();
            Enemy first = CreateEnemyShell();
            Enemy second = CreateEnemyShell();

            Assert.IsTrue(first.Initialize(data));
            Assert.IsTrue(second.Initialize(data));

            Assert.That(second.SpawnSequence, Is.GreaterThan(first.SpawnSequence),
                "Spawn sequence is the sole tiebreaker for clue selection and must be monotonic.");
        }

        [Test]
        public void SpawnSequence_IsReassignedWhenAPooledEnemyIsReinitialized()
        {
            EnemyDataSO data = CreateEnemyData();
            Enemy enemy = CreateEnemyShell();

            Assert.IsTrue(enemy.Initialize(data));
            long firstUse = enemy.SpawnSequence;

            enemy.ResetForPool();
            Assert.IsTrue(enemy.Initialize(data));

            Assert.That(enemy.SpawnSequence, Is.GreaterThan(firstUse),
                "A pooled enemy returning to play is a new spawn and needs a new sequence.");
        }

        [Test]
        public void CurrentClue_SelectsClosestEligibleEnemy()
        {
            EnemyDataSO data = CreateEnemyData();
            Enemy far = CreateEnemyAt(data, y: 8f);
            Enemy near = CreateEnemyAt(data, y: 2f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();

            Assert.That(director.CurrentClue, Is.EqualTo(near));
            Assert.That(director.CurrentClue, Is.Not.EqualTo(far));
        }

        [Test]
        public void CurrentClue_IsNullWhenClueCombatIsInactive()
        {
            EnemyDataSO data = CreateEnemyData();
            CreateEnemyAt(data, y: 2f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: false);
            director.Reevaluate();

            Assert.IsNull(director.CurrentClue,
                "A level that never arms clue combat must not produce a mark.");
        }

        [Test]
        public void PhaseTransition_ClearsThenReDerivesTheMark()
        {
            EnemyDataSO data = CreateEnemyData();
            Enemy near = CreateEnemyAt(data, y: 2f);

            var source = new StubObjectiveSource { Active = true };
            GameObject go = new GameObject("ActiveClueDirector_Test");
            ActiveClueDirector director = go.AddComponent<ActiveClueDirector>();
            director.SetObjectiveSource(source);
            _objectsToDestroy.Add(go);

            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(near));

            source.Active = false;
            director.Reevaluate();
            Assert.IsNull(director.CurrentClue, "Leaving clue combat must clear the mark.");

            source.Active = true;
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(near),
                "Re-entering must re-derive from live state, not restore stale state.");
        }

        [Test]
        public void Reevaluate_WhileFrozen_KeepsTheExistingMark()
        {
            EnemyDataSO data = CreateEnemyData();
            Enemy first = CreateEnemyAt(data, y: 4f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(first));

            EventBus.RaiseDrawingStarted();
            Assert.IsTrue(director.IsFrozen);

            CreateEnemyAt(data, y: 1f);
            director.Reevaluate();

            Assert.That(director.CurrentClue, Is.EqualTo(first),
                "A faster enemy must not steal the mark mid-trace.");
        }

        [Test]
        public void TryConsumeClue_ReturnsTrueOnlyOnceForTheSameClue()
        {
            EnemyDataSO data = CreateEnemyData();
            Enemy enemy = CreateEnemyAt(data, y: 3f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();

            Assert.IsTrue(director.TryConsumeClue(enemy), "First consume must win.");
            Assert.IsFalse(director.TryConsumeClue(enemy),
                "The pronunciation lead is a double-credit window; the second consume must lose.");
        }

        [Test]
        public void ConsumedClueRemoved_MarkMovesAndNextClueIsConsumable()
        {
            EnemyDataSO data = CreateEnemyData();
            Enemy consumed = CreateEnemyAt(data, y: 2f);
            Enemy next = CreateEnemyAt(data, y: 8f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(consumed));
            Assert.IsTrue(director.TryConsumeClue(consumed));

            Object.DestroyImmediate(consumed.gameObject);
            director.Reevaluate();

            Assert.That(director.CurrentClue, Is.EqualTo(next),
                "Removing the consumed clue must re-derive the mark to the next eligible enemy.");
            Assert.IsTrue(director.TryConsumeClue(next),
                "Consumption is per clue instance; a freshly derived clue must credit again.");
        }

        [Test]
        public void TryConsumeClue_RejectsAnEnemyThatIsNotTheCurrentClue()
        {
            EnemyDataSO data = CreateEnemyData();
            CreateEnemyAt(data, y: 2f);
            Enemy other = CreateEnemyAt(data, y: 9f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();

            Assert.IsFalse(director.TryConsumeClue(other));
        }

        [UnityTest]
        public IEnumerator CorrectTraceOnActiveClue_DamagesTheMarkedEnemy()
        {
            EnemyDataSO data = CreateEnemyData();
            BaybayinCharacterSO character = GlyphBadgePlayModeTestHelpers.CreateCharacter(
                "ba", null, null);
            character.stableId = "symbol.ba";
            _objectsToDestroy.Add(character);
            data.assignedCharacter = character;

            Enemy marked = CreateEnemyAt(data, y: 2f);
            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(marked));

            GameObject resolverGo = new GameObject("CombatResolver_Test");
            resolverGo.AddComponent<CombatResolver>();
            _objectsToDestroy.Add(resolverGo);
            yield return null;

            EventBus.RaiseCharacterRecognized("ba");
            yield return new WaitForSeconds(0.2f);

            Assert.That(marked.CurrentHealth, Is.LessThan(data.maxHealth),
                "A correct trace must automatically target the active clue.");
        }

        [UnityTest]
        public IEnumerator CorrectTraceOnClosestEligibleCarrier_DamagesIt()
        {
            EnemyDataSO nearData = CreateEnemyData();
            nearData.maxHealth = 3;
            BaybayinCharacterSO markedCharacter = GlyphBadgePlayModeTestHelpers.CreateCharacter(
                "ba", null, null);
            BaybayinCharacterSO unmarkedCharacter = GlyphBadgePlayModeTestHelpers.CreateCharacter(
                "ma", null, null);
            markedCharacter.stableId = "symbol.ba";
            unmarkedCharacter.stableId = "symbol.ma";
            _objectsToDestroy.Add(markedCharacter);
            _objectsToDestroy.Add(unmarkedCharacter);

            nearData.assignedCharacter = markedCharacter;
            Enemy near = CreateEnemyAt(nearData, y: 2f);

            EnemyDataSO farData = CreateEnemyData();
            farData.maxHealth = 3;
            farData.assignedCharacter = unmarkedCharacter;
            Enemy far = CreateEnemyAt(farData, y: 9f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(near));

            GameObject resolverGo = new GameObject("CombatResolver_Test");
            resolverGo.AddComponent<CombatResolver>();
            _objectsToDestroy.Add(resolverGo);
            yield return null;

            bool missed = false;
            void OnMiss() => missed = true;
            EventBus.OnDrawingMissed += OnMiss;

            EventBus.RaiseCharacterRecognized("ma");
            yield return new WaitForSeconds(0.2f);

            EventBus.OnDrawingMissed -= OnMiss;

            Assert.IsFalse(missed, "A valid glyph with an eligible carrier must resolve, even when the carrier is not marked.");
            Assert.That(far.CurrentHealth, Is.LessThan(farData.maxHealth),
                "Correct recognition resolves against the closest eligible carrier rather than requiring the marked enemy.");
        }

        [UnityTest]
        public IEnumerator DrawingAnUnmarkedRealCarrier_CreditsItsRestorationOnce()
        {
            EnemyDataSO markedData = CreateEnemyData();
            markedData.maxHealth = 3;
            Enemy marked = CreateEnemyAt(markedData, y: 2f);

            EnemyDataSO otherData = CreateEnemyData();
            otherData.maxHealth = 3;
            otherData.assignedCharacter = CreateTestCharacter("NA", "symbol.na");
            Enemy other = CreateEnemyAt(otherData, y: 9f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(marked));

            int credited = 0;
            Enemy creditedCarrier = null;
            director.OnActiveClueResolved += carrier =>
            {
                credited++;
                creditedCarrier = carrier;
            };

            GameObject resolverGo = new GameObject("CombatResolver_UnmarkedCredit_Test");
            resolverGo.AddComponent<CombatResolver>();
            _objectsToDestroy.Add(resolverGo);
            yield return null;

            EventBus.RaiseCharacterRecognized("NA");
            EventBus.RaiseCharacterRecognized("NA");
            yield return new WaitForSeconds(0.2f);

            Assert.That(credited, Is.EqualTo(1),
                "Drawing a visible real character must credit restoration even when another glyph is marked.");
            Assert.That(creditedCarrier, Is.EqualTo(other));
            Assert.That(director.CurrentClue, Is.EqualTo(marked));
        }

        [Test]
        public void CachePausedRun_OrdersEnemiesByDistanceToBase()
        {
            EnemyDataSO data = CreateEnemyData();
            Enemy far = CreateEnemyAt(data, y: 9f);
            Enemy middle = CreateEnemyAt(data, y: 5f);
            Enemy near = CreateEnemyAt(data, y: 1f);

            var activeEnemies = new List<Enemy> { far, near, middle };

            GameObject managerGo = new GameObject("GameManager_Test");
            GameManager manager = managerGo.AddComponent<GameManager>();
            _objectsToDestroy.Add(managerGo);

            manager.CachePausedRunSnapshot(
                levelId: 1, currentHearts: 3, currentWaveIndex: 0,
                currentWaveSpawnedCount: 3, activeEnemies: activeEnemies);

            Assert.IsTrue(manager.TryGetPausedRunEnemies(
                1, out IReadOnlyList<GameManager.PausedEnemySnapshot> snapshots));
            Assert.That(snapshots.Count, Is.EqualTo(3));
            Assert.That(snapshots[0].Position.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(snapshots[1].Position.y, Is.EqualTo(5f).Within(0.001f));
            Assert.That(snapshots[2].Position.y, Is.EqualTo(9f).Within(0.001f),
                "Restore replays this list in order, so capture order decides the re-derived mark.");
        }

        [Test]
        public void Presenter_GlyphChannel_ReportsAnswerVisible()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = true;
            level.clueChannels = ClueChannels.Glyph | ClueChannels.LatinText;
            _objectsToDestroy.Add(level);

            GameObject go = new GameObject("ActiveCluePresenter_Test");
            ActiveCluePresenter presenter = go.AddComponent<ActiveCluePresenter>();
            _objectsToDestroy.Add(go);

            presenter.ApplyLevel(level);

            Assert.That(presenter.ResolvedChannels & ClueChannels.Glyph,
                Is.EqualTo(ClueChannels.Glyph));
            Assert.IsTrue(presenter.AnswerWasVisible,
                "A visible glyph makes the attempt recognition, not recall.");
        }

        [Test]
        public void Presenter_AudioOnlyChannel_AddsFallbackAndReportsAnswerHidden()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = true;
            level.clueChannels = ClueChannels.SpokenAudio;
            level.audioVisualFallback = ClueChannels.IncompleteWord;
            _objectsToDestroy.Add(level);

            GameObject go = new GameObject("ActiveCluePresenter_Test");
            ActiveCluePresenter presenter = go.AddComponent<ActiveCluePresenter>();
            _objectsToDestroy.Add(go);

            presenter.ApplyLevel(level);

            Assert.IsTrue(ClueChannelResolver.HasReadableVisual(presenter.ResolvedChannels),
                "A required sound clue must have a readable visual equivalent.");
            Assert.IsFalse(presenter.AnswerWasVisible,
                "An incomplete-word fallback is still a retrieval attempt.");
        }

        // Spec section 4: "Armor | Multi-hit enemy stays eligible until IsDying; mark holds".
        // Consumption guards the 0.06s double-credit window only. It must not make the clue
        // ineligible, or an armored enemy loses its mark after one hit and -- under the strict
        // gate -- becomes undrawable while it walks to the shrine.
        [Test]
        public void ArmoredClue_KeepsTheMarkAfterBeingConsumed()
        {
            EnemyDataSO data = CreateEnemyData();
            data.maxHealth = 3;

            Enemy armored = CreateEnemyAt(data, y: 2f);
            Enemy behind = CreateEnemyAt(data, y: 7f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(armored));

            Assert.IsTrue(director.TryConsumeClue(armored));
            director.Reevaluate();

            Assert.That(director.CurrentClue, Is.EqualTo(armored),
                "A consumed but still-living armored clue must keep the mark.");
            Assert.That(director.CurrentClue, Is.Not.EqualTo(behind));
        }

        [Test]
        public void ConsumedClue_StaysMarkedButCreditsOnlyOnce()
        {
            EnemyDataSO data = CreateEnemyData();
            data.maxHealth = 3;
            Enemy armored = CreateEnemyAt(data, y: 2f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();

            Assert.IsTrue(director.TryConsumeClue(armored), "First trace credits the objective.");
            director.Reevaluate();
            Assert.IsFalse(director.TryConsumeClue(armored),
                "Later traces still damage the enemy but must not credit the objective again.");
            Assert.That(director.CurrentClue, Is.EqualTo(armored));
        }

        // RecognitionManager raises DrawingFailed (not RecognitionResolved) for a degenerate
        // stroke. Without this subscription the mark stays frozen forever.
        [Test]
        public void DrawingFailed_ClearsTheFreeze()
        {
            EnemyDataSO data = CreateEnemyData();
            CreateEnemyAt(data, y: 4f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();

            EventBus.RaiseDrawingStarted();
            Assert.IsTrue(director.IsFrozen);

            EventBus.RaiseDrawingFailed();

            Assert.IsFalse(director.IsFrozen,
                "A failed stroke must release the mark, not deadlock it.");
        }

        // StrokeCapture.CompleteCurrentStroke discards a tap-like stroke and returns before
        // StartMultiStrokeTimer, so SubmitForRecognition never runs and RecognitionManager is
        // never invoked -- that path raises NEITHER RecognitionResolved NOR DrawingFailed.
        // A stray tap would otherwise freeze the mark permanently, so the freeze must expire.
        [UnityTest]
        public IEnumerator AbandonedStroke_FreezeExpiresSoTheMarkResumesTracking()
        {
            EnemyDataSO data = CreateEnemyData();
            Enemy first = CreateEnemyAt(data, y: 4f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(first));

            // Simulate the tap-discard path: drawing starts, nothing ever resolves.
            EventBus.RaiseDrawingStarted();
            Assert.IsTrue(director.IsFrozen);

            float timeout = Time.unscaledTime + ActiveClueDirector.MaxFreezeSeconds + 1f;
            while (director.IsFrozen && Time.unscaledTime < timeout)
                yield return null;

            Assert.IsFalse(director.IsFrozen,
                "An abandoned stroke must not freeze the mark permanently.");

            Enemy closer = CreateEnemyAt(data, y: 1f);
            first.TakeDamage(first.MaxHealth);
            yield return null;
            director.Reevaluate();

            Assert.That(director.CurrentClue, Is.EqualTo(closer),
                "After the freeze expires the mark must track threat again.");
        }

        // LevelFlowController now constructs a director on EVERY level, so the plan's
        // "Instance is null on existing levels" safety argument no longer holds. Default-off
        // now rests entirely on IsClueCombatActive, which makes this the key regression test.
        [UnityTest]
        public IEnumerator ClueCombatDisabled_CombatResolverUsesLegacyTargeting()
        {
            EnemyDataSO data = CreateEnemyData();
            data.maxHealth = 3;

            Enemy target = CreateEnemyAt(data, y: 6f);

            // A director exists but clue combat is off -- exactly the shipped configuration
            // for all 15 existing levels.
            ActiveClueDirector director = CreateDirector(clueCombatActive: false);
            director.Reevaluate();
            Assert.IsNull(director.CurrentClue);

            GameObject resolverGo = new GameObject("CombatResolver_Legacy_Test");
            resolverGo.AddComponent<CombatResolver>();
            _objectsToDestroy.Add(resolverGo);
            yield return null;

            EventBus.RaiseCharacterRecognized("BA");
            yield return new WaitForSeconds(0.2f);

            Assert.That(target.CurrentHealth, Is.LessThan(data.maxHealth),
                "With clue combat disabled the legacy closest-match path must still resolve.");
        }

        // A single finger-lift can raise OnCharacterRecognized more than once inside the
        // pronunciation-lead window. The echo must resolve once, or one user action both
        // double-damages and double-counts against the objective.
        [UnityTest]
        public IEnumerator EchoedRecognition_ResolvesTheClueOnlyOnce()
        {
            EnemyDataSO data = CreateEnemyData();
            data.maxHealth = 3;
            Enemy marked = CreateEnemyAt(data, y: 2f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(marked));

            GameObject resolverGo = new GameObject("CombatResolver_Echo_Test");
            resolverGo.AddComponent<CombatResolver>();
            _objectsToDestroy.Add(resolverGo);
            yield return null;

            EventBus.RaiseCharacterRecognized("BA");
            EventBus.RaiseCharacterRecognized("BA");
            yield return new WaitForSeconds(0.3f);

            Assert.That(marked.CurrentHealth, Is.EqualTo(data.maxHealth - 1),
                "An echoed recognition must not land a second hit.");
        }

        [Test]
        public void PausedGame_HoldsTheMarkWithoutReselectingOrClearing()
        {
            EnemyDataSO data = CreateEnemyData();
            Enemy first = CreateEnemyAt(data, y: 4f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(first));

            GameObject managerGo = new GameObject("GameManager_Pause_Test");
            GameManager manager = managerGo.AddComponent<GameManager>();
            _objectsToDestroy.Add(managerGo);

            try
            {
                manager.StartGame();
                manager.PauseGame();
                Assert.That(manager.CurrentState, Is.EqualTo(GameState.Paused));

                // A closer enemy appearing while paused must not steal the mark.
                CreateEnemyAt(data, y: 1f);
                director.Reevaluate();

                Assert.That(director.CurrentClue, Is.EqualTo(first),
                    "A paused run must neither re-select nor clear the mark.");
            }
            finally
            {
                manager.ResumeGame();
                Time.timeScale = 1f;
            }
        }

        // LevelFlowController creates a presenter on every level, so a legacy level's glyph
        // badges must survive it. The badge sweep hides every enemy badge that is not the
        // current clue, and on a legacy level the clue is always null -- which would blank
        // every glyph on screen if the armed-level guard were missing.
        [UnityTest]
        public IEnumerator Presenter_ClueCombatDisabled_LeavesEnemyBadgesVisible()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = false;
            _objectsToDestroy.Add(level);

            GlyphBadgeConfigSO badgeConfig = GlyphBadgePlayModeTestHelpers.CreateBadgeConfig();
            _objectsToDestroy.Add(badgeConfig);

            EnemyDataSO data = CreateEnemyData();
            Enemy enemy = CreateEnemyShell();
            (EnemyGlyphBadge badge, SpriteRenderer badgeRenderer) =
                GlyphBadgePlayModeTestHelpers.AddGlyphBadgeChild(enemy.gameObject, badgeConfig);
            enemy.transform.position = new Vector3(0f, 3f, 0f);
            Assert.IsTrue(enemy.Initialize(data));
            badge.ApplyLayout();
            badge.Refresh();
            badge.Show();
            yield return null;

            float alphaBefore = badgeRenderer.color.a;

            GameObject go = new GameObject("ActiveCluePresenter_Legacy_Test");
            ActiveCluePresenter presenter = go.AddComponent<ActiveCluePresenter>();
            _objectsToDestroy.Add(go);

            presenter.ApplyLevel(level);
            yield return null;

            Assert.That(badgeRenderer.color.a, Is.EqualTo(alphaBefore).Within(0.001f),
                "A legacy level's glyph badges must not be hidden by the clue presenter.");
        }

        // The mark latches, so an enemy that spawns while it is held raises no clue change and
        // the sweep in HandleActiveClueChanged never reaches it. "One visibly marked clue"
        // would fail in steady state on a glyph level.
        [UnityTest]
        public IEnumerator Presenter_EnemySpawnedWhileMarkLatched_AppliesCrowdLocalBadgePolicy()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = true;
            level.clueChannels = ClueChannels.Glyph;
            _objectsToDestroy.Add(level);

            GlyphBadgeConfigSO badgeConfig = GlyphBadgePlayModeTestHelpers.CreateBadgeConfig();
            _objectsToDestroy.Add(badgeConfig);

            EnemyDataSO data = CreateEnemyData();
            Sprite badgeSprite = GlyphBadgePlayModeTestHelpers.CreateSprite(Color.white);
            data.assignedCharacter.badgeSprite = badgeSprite;
            _objectsToDestroy.Add(badgeSprite);

            Enemy marked = CreateEnemyAt(data, y: 2f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(marked));

            GameObject go = new GameObject("ActiveCluePresenter_LateSpawn_Test");
            ActiveCluePresenter presenter = go.AddComponent<ActiveCluePresenter>();
            _objectsToDestroy.Add(go);

            presenter.ApplyLevel(level);
            yield return null;

            // Enemy.Awake caches the badge, and this shell was already active before the badge
            // child existed, so the cached reference has to be supplied directly.
            Enemy lateSpawn = CreateEnemyShell();
            (EnemyGlyphBadge badge, SpriteRenderer badgeRenderer) =
                GlyphBadgePlayModeTestHelpers.AddGlyphBadgeChild(lateSpawn.gameObject, badgeConfig);
            GlyphBadgePlayModeTestHelpers.SetPrivateField(lateSpawn, "_glyphBadge", badge);
            lateSpawn.transform.position = new Vector3(0f, 7f, 0f);
            Assert.IsTrue(lateSpawn.Initialize(data));
            yield return null;

            Assert.That(director.CurrentClue, Is.EqualTo(marked),
                "The mark must still be latched, or this test would not exercise the spawn path.");
            Assert.IsTrue(badgeRenderer.enabled,
                "The late spawn must actually carry a renderable badge for this test to bite.");
            Assert.That(badgeRenderer.color.a, Is.EqualTo(1f).Within(0.001f),
                "A distant late spawn remains readable under the crowd-local badge policy.");

            Enemy crowdedSpawn = CreateEnemyShell();
            (EnemyGlyphBadge crowdedBadge, SpriteRenderer crowdedRenderer) =
                GlyphBadgePlayModeTestHelpers.AddGlyphBadgeChild(crowdedSpawn.gameObject, badgeConfig);
            GlyphBadgePlayModeTestHelpers.SetPrivateField(crowdedSpawn, "_glyphBadge", crowdedBadge);
            crowdedSpawn.transform.position = new Vector3(0f, 3.5f, 0f);
            Assert.IsTrue(crowdedSpawn.Initialize(data));
            yield return null;

            Assert.That(crowdedRenderer.color.a, Is.EqualTo(0f).Within(0.001f),
                "A late spawn inside the active clue crowd radius suppresses its badge.");
        }

        // Spec section 3.5: the mark is a marker treatment on the active enemy, driven
        // independently of channel. On a SpokenAudio + LatinText level every badge is hidden,
        // so without it nothing on screen says which enemy is the clue.
        [UnityTest]
        public IEnumerator Presenter_NonGlyphChannels_StillMarkTheActiveEnemy()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = true;
            level.clueChannels = ClueChannels.SpokenAudio | ClueChannels.LatinText;
            _objectsToDestroy.Add(level);

            EnemyDataSO data = CreateEnemyData();
            Enemy marked = CreateEnemyAt(data, y: 2f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(marked));

            GameObject go = new GameObject("ActiveCluePresenter_Mark_Test");
            ActiveCluePresenter presenter = go.AddComponent<ActiveCluePresenter>();
            _objectsToDestroy.Add(go);

            // The ring is off by default now - it read as noise around the enemy art, and the
            // scroll badge marks the target on levels that reveal the glyph. This level does not
            // reveal it, which is exactly the case the ring still exists for, so opt in explicitly.
            presenter.ShowActiveClueMark = true;

            presenter.ApplyLevel(level);
            yield return null;

            Assert.That(presenter.ResolvedChannels & ClueChannels.Glyph,
                Is.EqualTo(ClueChannels.None),
                "This level must not reveal the glyph, or the badge would be doing the marking.");
            Assert.IsNotNull(presenter.ActiveClueMark,
                "A non-glyph level still needs one visibly marked active enemy.");
            Assert.IsTrue(presenter.ActiveClueMark.activeInHierarchy,
                "The mark must be on screen, not merely constructed.");

            // The enemy walks itself down the field in EnemyMover.Update, and a coroutine
            // resuming from `yield return null` runs after Update but before LateUpdate. So a
            // position sampled here is always one frame ahead of the LateUpdate that last moved
            // the mark -- a gap wider than any tolerance worth asserting. Park the mover, then
            // drive the enemy by hand so the comparison is between two stationary transforms.
            marked.GetComponent<EnemyMover>().Stop();
            float markBeforeMove = presenter.ActiveClueMark.transform.position.y;
            Assert.That(markBeforeMove, Is.GreaterThan(1f),
                "The mark must start at the latch position, or the follow assertion is vacuous.");

            marked.transform.position = new Vector3(0f, 0.5f, 0f);
            yield return null;

            // Deliberately 1.5 units of travel: a mark that is only placed once when the clue
            // latches stays up at y=2 and misses by a mile, so this still proves tracking.
            Assert.That(presenter.ActiveClueMark.transform.position.y,
                Is.EqualTo(marked.transform.position.y).Within(0.001f),
                "The mark must follow the active enemy, not sit where it first latched.");
        }

        // The mark is a presentation side effect like every other, so a level that never arms
        // clue combat must not construct one.
        [UnityTest]
        public IEnumerator Presenter_ClueCombatDisabled_BuildsNoMark()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = false;
            _objectsToDestroy.Add(level);

            EnemyDataSO data = CreateEnemyData();
            CreateEnemyAt(data, y: 2f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: false);
            director.Reevaluate();

            GameObject go = new GameObject("ActiveCluePresenter_NoMark_Test");
            ActiveCluePresenter presenter = go.AddComponent<ActiveCluePresenter>();
            _objectsToDestroy.Add(go);

            presenter.ApplyLevel(level);
            yield return null;

            Assert.IsNull(presenter.ActiveClueMark,
                "A legacy level must gain no clue mark.");
            Assert.IsNull(GameObject.Find("[Runtime] ActiveClueMark"));
        }

        // SALIN-135 AC1. An accepted draw owes the player two answers: the combat response the
        // enemy already plays, and a language response naming the word that just got its symbol
        // back. Before this the only "Restored:" surface was the end-of-level summary.
        [Test]
        public void AcceptedClueDraw_RaisesTheWordRestorationCueExactlyOnce()
        {
            EnemyDataSO data = CreateEnemyData();
            data.maxHealth = 3;
            Enemy marked = CreateEnemyAt(data, y: 2f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(marked));

            ActiveCluePresenter presenter = CreateFocusWordPresenter(data.assignedCharacter);
            Assert.That(presenter.WordRestoredCueCount, Is.EqualTo(0),
                "Setup: nothing has been restored yet.");

            Assert.IsTrue(director.TryConsumeClue(marked), "Setup: the first draw must credit.");

            Assert.That(presenter.WordRestoredCueCount, Is.EqualTo(1),
                "An accepted draw must announce the word it just restored.");
            Assert.That(presenter.LastWordRestoredMessage, Does.Contain("BAHAY"),
                "The cue must unmask the focus word the IncompleteWord channel was hiding.");

            // The pronunciation lead is a double-credit window; the cue rides the same guard.
            Assert.IsFalse(director.TryConsumeClue(marked));
            Assert.That(presenter.WordRestoredCueCount, Is.EqualTo(1),
                "A second consume inside the lead window must not repeat the cue.");
        }

        // AC1's "exactly once" across the real pipeline: recognition echo included.
        [UnityTest]
        public IEnumerator EchoedAcceptedDraw_RaisesTheWordRestorationCueOnce()
        {
            EnemyDataSO data = CreateEnemyData();
            data.maxHealth = 3;
            Enemy marked = CreateEnemyAt(data, y: 2f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(marked));

            ActiveCluePresenter presenter = CreateFocusWordPresenter(data.assignedCharacter);

            GameObject resolverGo = new GameObject("CombatResolver_Restore_Test");
            resolverGo.AddComponent<CombatResolver>();
            _objectsToDestroy.Add(resolverGo);
            yield return null;

            EventBus.RaiseCharacterRecognized("BA");
            EventBus.RaiseCharacterRecognized("BA");
            yield return new WaitForSeconds(0.3f);

            Assert.That(presenter.WordRestoredCueCount, Is.EqualTo(1),
                "One finger-lift must restore the word once, however many times it echoes.");
        }

        // SALIN-135 AC2. A rejected draw is a correction, not a setback: no restoration cue, and
        // the target the player must still draw does not move.
        [UnityTest]
        public IEnumerator RejectedDraw_ShowsNoRestorationCueAndDoesNotAdvanceTheTarget()
        {
            EnemyDataSO markedData = CreateEnemyData();
            markedData.maxHealth = 3;
            Enemy marked = CreateEnemyAt(markedData, y: 2f);

            EnemyDataSO otherData = CreateEnemyData();
            otherData.maxHealth = 3;
            otherData.assignedCharacter = CreateTestCharacter("TA", "symbol.ta");
            CreateEnemyAt(otherData, y: 9f);

            ActiveClueDirector director = CreateDirector(clueCombatActive: true);
            director.Reevaluate();
            Assert.That(director.CurrentClue, Is.EqualTo(marked));

            ActiveCluePresenter presenter = CreateFocusWordPresenter(markedData.assignedCharacter);

            GameObject resolverGo = new GameObject("CombatResolver_Reject_Test");
            resolverGo.AddComponent<CombatResolver>();
            _objectsToDestroy.Add(resolverGo);
            yield return null;

            bool missed = false;
            void OnMiss() => missed = true;
            EventBus.OnDrawingMissed += OnMiss;
            try
            {
                EventBus.RaiseCharacterRecognized("MA");
                yield return new WaitForSeconds(0.3f);
            }
            finally
            {
                EventBus.OnDrawingMissed -= OnMiss;
            }

            Assert.IsTrue(missed, "A wrong draw must raise the correction cue.");
            Assert.That(presenter.WordRestoredCueCount, Is.EqualTo(0),
                "Nothing was restored, so nothing may claim to have been.");
            Assert.That(director.CurrentClue, Is.EqualTo(marked),
                "A rejected draw must leave the player the same target to retry.");
            Assert.That(marked.CurrentHealth, Is.EqualTo(markedData.maxHealth),
                "A rejected draw is non-destructive on both sides of the fight.");
        }

        /// <summary>
        /// The standing "DRAW THE GLOWING SYMBOL TO DEFEND" line shares the bottom band with
        /// the challenge board — on the shipped screenshot it printed straight through the
        /// board's parchment. While a challenge runs, the instruction must stand down, and
        /// it must come back when the challenge ends rather than staying hidden forever.
        /// </summary>
        [UnityTest]
        public IEnumerator ClueInstruction_YieldsToTheChallengeBoard_AndReturns()
        {
            GameObject presenterObject = new GameObject("ActiveCluePresenter_Instruction_Test");
            _objectsToDestroy.Add(presenterObject);
            GameObject labelObject = new GameObject(
                "DrawGlowingSymbolInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(presenterObject.transform, false);
            presenterObject.AddComponent<ActiveCluePresenter>();

            yield return null;

            Assert.IsTrue(labelObject.activeSelf,
                "Settled combat HUD shows its standing instruction.");

            ChallengeRuntimeState.Begin(1);
            yield return null;

            Assert.IsFalse(labelObject.activeSelf,
                "The challenge board owns the band; the instruction must stand down.");

            ChallengeRuntimeState.Clear();
            yield return null;

            Assert.IsTrue(labelObject.activeSelf,
                "Once the board leaves, the instruction comes back — suppression is not a latch.");
        }

        /// <summary>
        /// The enemy introduction card parks on the same bottom band as the standing
        /// "DRAW THE GLOWING SYMBOL TO DEFEND" line — the gold instruction printed straight
        /// through the card's slate. While a card or lesson owns the screen the instruction
        /// must stand down, and it must come back the moment the beat ends: the lifetime
        /// banner deliberately outlives IsPlaying, and standing down for all of it would
        /// erase the instruction from ordinary combat.
        /// </summary>
        [UnityTest]
        public IEnumerator ClueInstruction_YieldsToTheEnemyIntroduction_AndReturns()
        {
            GameObject presenterObject = new GameObject("ActiveCluePresenter_IntroInstruction_Test");
            _objectsToDestroy.Add(presenterObject);
            GameObject labelObject = new GameObject(
                "DrawGlowingSymbolInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(presenterObject.transform, false);
            presenterObject.AddComponent<ActiveCluePresenter>();

            GameObject beatObject = new GameObject("EnemyIntroductionBeat_Test");
            _objectsToDestroy.Add(beatObject);
            EnemyIntroductionBeat beat = beatObject.AddComponent<EnemyIntroductionBeat>();

            yield return null;

            Assert.IsTrue(labelObject.activeSelf,
                "Settled combat HUD shows its standing instruction.");

            GlyphBadgePlayModeTestHelpers.SetPrivateField(beat, "_isPlaying", true);
            yield return null;

            Assert.IsFalse(labelObject.activeSelf,
                "The introduction card owns the band; the instruction must stand down.");

            GlyphBadgePlayModeTestHelpers.SetPrivateField(beat, "_isPlaying", false);
            yield return null;

            Assert.IsTrue(labelObject.activeSelf,
                "Once the beat ends the instruction comes back — suppression is not a latch.");
        }

        /// <summary>
        /// The story scroll owns the bottom band while it is live — the standing
        /// "DRAW THE GLOWING SYMBOL TO DEFEND" line would print through it, so the
        /// instruction yields for the dialogue's duration and comes back when it ends.
        /// </summary>
        [UnityTest]
        public IEnumerator ClueInstruction_YieldsToDialogue_AndReturns()
        {
            GameObject presenterObject = new GameObject("ActiveCluePresenter_DialogueInstruction_Test");
            _objectsToDestroy.Add(presenterObject);
            GameObject labelObject = new GameObject(
                "DrawGlowingSymbolInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(presenterObject.transform, false);
            presenterObject.AddComponent<ActiveCluePresenter>();

            GameObject dialogueObject = new GameObject("DialogueController_Test");
            _objectsToDestroy.Add(dialogueObject);
            DialogueController dialogue = dialogueObject.AddComponent<DialogueController>();
            DialogueSO dialogueData = ScriptableObject.CreateInstance<DialogueSO>();
            _objectsToDestroy.Add(dialogueData);

            yield return null;

            Assert.IsTrue(labelObject.activeSelf,
                "Settled combat HUD shows its standing instruction.");

            // IsPresenting reads only _currentDialogue, so field injection stands a live
            // dialogue up without driving the scroll's own UI.
            GlyphBadgePlayModeTestHelpers.SetPrivateField(
                dialogue, "_currentDialogue", dialogueData);
            yield return null;

            Assert.IsFalse(labelObject.activeSelf,
                "A live dialogue owns the band; the instruction must stand down.");

            GlyphBadgePlayModeTestHelpers.SetPrivateField(
                dialogue, "_currentDialogue", (DialogueSO)null);
            yield return null;

            Assert.IsTrue(labelObject.activeSelf,
                "Once the dialogue ends the instruction comes back — suppression is not a latch.");
        }

        /// <summary>
        /// The instant-win beat's banner parks on the restoration rail's top edge — the same
        /// bottom band the standing "DRAW THE GLOWING SYMBOL TO DEFEND" line occupies, which is
        /// where the shipped screenshot showed them colliding. While the beat presents, the
        /// instruction must stand down, and it must come back once the board is clear.
        /// </summary>
        [UnityTest]
        public IEnumerator ClueInstruction_YieldsToTheInstantWinBeat_AndReturns()
        {
            GameObject presenterObject = new GameObject("ActiveCluePresenter_WinInstruction_Test");
            _objectsToDestroy.Add(presenterObject);
            GameObject labelObject = new GameObject(
                "DrawGlowingSymbolInstruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(presenterObject.transform, false);
            presenterObject.AddComponent<ActiveCluePresenter>();

            GameObject beatObject = new GameObject("InstantWinPresenter_Test");
            _objectsToDestroy.Add(beatObject);
            InstantWinPresenter beat = beatObject.AddComponent<InstantWinPresenter>();

            yield return null;

            Assert.IsTrue(labelObject.activeSelf,
                "Settled combat HUD shows its standing instruction.");

            // IsPresenting is an auto-property; the backing field is the only reachable seam.
            GlyphBadgePlayModeTestHelpers.SetPrivateField(
                beat, "<IsPresenting>k__BackingField", true);
            yield return null;

            Assert.IsFalse(labelObject.activeSelf,
                "The win banner owns the band; the instruction must stand down.");

            GlyphBadgePlayModeTestHelpers.SetPrivateField(
                beat, "<IsPresenting>k__BackingField", false);
            yield return null;

            Assert.IsTrue(labelObject.activeSelf,
                "Once the beat clears, the instruction comes back — suppression is not a latch.");
        }

        /// <summary>
        /// The beat holds for a tap rather than dissolving on a timer — a fixed duration
        /// cannot know a reader's pace, and every other text surface in the game is already
        /// tap-gated. The tap then releases the dissolve and the beat ends.
        /// </summary>
        [UnityTest]
        public IEnumerator InstantWinBeat_HoldsForTap_ThenDissolvesOnCue()
        {
            GameObject beatObject = new GameObject("InstantWinPresenter_TapGate");
            _objectsToDestroy.Add(beatObject);
            InstantWinPresenter beat = beatObject.AddComponent<InstantWinPresenter>();

            beat.StartCoroutine(beat.Play(null, 6));

            // No rail means no celebration wait, and level 6 has no authored frozen hold,
            // so the read-gate arms after the 1.2 s arming pause alone. Three and a half
            // is generous — the point is the beat is still waiting at a moment a fixed
            // timer would already have dismissed it.
            yield return new WaitForSecondsRealtime(3.5f);

            Assert.IsTrue(beat.IsPresenting,
                "The banner's seconds are a floor, not a ceiling — the beat must hold "
                + "for a tap instead of auto-dismissing.");

            Button catcher =
                GlyphBadgePlayModeTestHelpers.GetPrivateField<Button>(beat, "_tapCatcher");
            Assert.IsNotNull(catcher, "The read-gate needs its tap catcher built.");
            Assert.IsTrue(catcher.gameObject.activeSelf,
                "The tap catcher arms once the minimum read time passes.");

            catcher.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.IsFalse(beat.IsPresenting,
                "The player's tap releases the dissolve and the beat ends.");
        }

        /// <summary>
        /// Regression for the stuck win: the read-gate's tap catcher was built UNDER the
        /// overlay's own CanvasGroup, which EnsureOverlay authors interactable=false and
        /// blocksRaycasts=false so the beat never swallows gameplay input. Both flags
        /// propagate to children, so the catcher could neither be hit by a raycast nor
        /// pressed — no real tap could ever release the hold. Play() waited on
        /// _waitingForTap forever with the board frozen, WaveManager's routine never
        /// reached CompleteRun, and the victory screen never appeared.
        ///
        /// onClick.Invoke() (the test above) cannot cover this: it bypasses the raycast
        /// and interactable checks entirely. This test drives the REAL input path — the
        /// CanvasGroup hit-validity and interactable predicates a raycast applies, plus an
        /// ExecuteEvents pointer click, the same call InputSystemUIInputModule makes for a
        /// physical tap.
        /// </summary>
        [UnityTest]
        public IEnumerator InstantWinBeat_TapCatcher_IsReachableByRealPointerInput()
        {
            GameObject beatObject = new GameObject("InstantWinPresenter_RealTap");
            _objectsToDestroy.Add(beatObject);
            InstantWinPresenter beat = beatObject.AddComponent<InstantWinPresenter>();

            // PointerEventData needs an EventSystem; no input module is required because
            // the click is delivered through ExecuteEvents directly — the same call an
            // input module makes once a raycast has picked the catcher.
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            _objectsToDestroy.Add(eventSystemObject);

            // StartCoroutine runs Play up to its first yield synchronously, so the
            // catcher already exists once this returns.
            beat.StartCoroutine(beat.Play(null, 6));
            Button catcher =
                GlyphBadgePlayModeTestHelpers.GetPrivateField<Button>(beat, "_tapCatcher");
            Assert.IsNotNull(catcher, "The read-gate needs its tap catcher built.");

            // Wait for the read-gate to arm, bounded so a broken gate fails rather than
            // hanging the run.
            yield return GlyphBadgePlayModeTestHelpers.WaitUntilOrTimeout(
                () => catcher.gameObject.activeSelf, 6f);
            Assert.IsTrue(catcher.gameObject.activeSelf,
                "The tap catcher arms once the minimum read time passes.");

            Canvas canvas = beat.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas, "EnsureOverlay must parent the beat under a canvas.");
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            Assert.IsNotNull(raycaster, "EnsureOverlay must guarantee a raycaster.");

            // GraphicRaycaster.Raycast itself cannot be exercised here: it skips graphics
            // whose CanvasRenderer never got a render depth (depth==-1), and nothing
            // renders a screen-space canvas in a PlayMode run — even a perfectly wired
            // button returns zero hits. Instead this asserts every condition the raycast
            // applies, through the same public APIs the raycaster itself calls:
            //   * IsRaycastLocationValid — the CanvasGroup-blocking check (this is the
            //     exact predicate GraphicRaycaster evaluates per graphic);
            //   * IsInteractable — the CanvasGroup interactable chain Selectable gates on;
            //   * RectangleContainsScreenPoint — the hit geometry;
            // then delivers a REAL pointer click through ExecuteEvents — the same call an
            // input module makes after its raycast picks the catcher.
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
            };
            Image catcherImage = catcher.GetComponent<Image>();
            Assert.IsNotNull(catcherImage, "The tap catcher needs its raycast-target Image.");

            var failures = new List<string>();
            if (!catcherImage.raycastTarget)
                failures.Add("the catcher's Image is not a raycast target");
            if (!catcherImage.IsRaycastLocationValid(pointer.position, null))
                failures.Add("a centre-screen raycast never reaches the catcher "
                    + "(an ancestor CanvasGroup has blocksRaycasts=false)");
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)catcher.transform, pointer.position))
                failures.Add("the catcher's rect does not cover the screen centre");
            if (!catcher.IsInteractable())
                failures.Add("the catcher is not interactable "
                    + "(an ancestor CanvasGroup has interactable=false)");

            ExecuteEvents.Execute(
                catcher.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            yield return null;

            if (beat.IsPresenting)
                failures.Add("a real pointer click did not release the read-gate — the "
                    + "beat holds forever and the run never reaches the victory screen");

            Assert.IsEmpty(failures, string.Join("; ", failures));
        }

        /// <summary>
        /// A presenter armed on a level whose single focus word contains the given symbol, so
        /// the restoration cue has something to unmask.
        /// </summary>
        private ActiveCluePresenter CreateFocusWordPresenter(BaybayinCharacterSO symbol)
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.activeClueCombatEnabled = true;
            level.clueChannels = ClueChannels.IncompleteWord;
            level.focusWords.Add(new FocusWordDefinition
            {
                stableId = "level.test.focus.bahay",
                latinSpelling = "bahay",
                displayLabel = "BAHAY",
                meaning = "test-house",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = symbol, spokenValueId = "value.test-ba" },
                },
            });
            _objectsToDestroy.Add(level);

            GameObject go = new GameObject("ActiveCluePresenter_Restore_Test");
            ActiveCluePresenter presenter = go.AddComponent<ActiveCluePresenter>();
            _objectsToDestroy.Add(go);

            presenter.ApplyLevel(level);
            return presenter;
        }

        private sealed class StubObjectiveSource : IClueObjectiveSource
        {
            public bool Active;
            public bool IsClueCombatActive => Active;
            public IReadOnlyCollection<string> CurrentObjectiveContentIds =>
                System.Array.Empty<string>();
        }

        private ActiveClueDirector CreateDirector(bool clueCombatActive)
        {
            GameObject go = new GameObject("ActiveClueDirector_Test");
            ActiveClueDirector director = go.AddComponent<ActiveClueDirector>();
            director.SetObjectiveSource(new StubObjectiveSource { Active = clueCombatActive });
            _objectsToDestroy.Add(go);
            return director;
        }

        private EnemyDataSO CreateEnemyData()
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "enemy.test.soldado";
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            data.assignedCharacter = CreateTestCharacter("BA", "symbol.ba");
            _objectsToDestroy.Add(data);
            return data;
        }

        private BaybayinCharacterSO CreateTestCharacter(string characterId, string stableId)
        {
            BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = characterId;
            character.stableId = stableId;
            character.syllable = characterId.ToLowerInvariant();
            _objectsToDestroy.Add(character);
            return character;
        }

        private Enemy CreateEnemyShell()
        {
            EnsureTracker();

            GameObject go = new GameObject("Enemy_ActiveClue_Test");
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

        private Enemy CreateEnemyAt(EnemyDataSO data, float y)
        {
            Enemy enemy = CreateEnemyShell();
            enemy.transform.position = new Vector3(0f, y, 0f);
            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private void EnsureTracker()
        {
            if (ActiveEnemyTracker.Instance != null)
                return;

            GameObject trackerObject = new GameObject("ActiveEnemyTracker_Test");
            trackerObject.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerObject);
        }
    }
}
