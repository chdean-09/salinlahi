using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
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
        public IEnumerator CorrectTraceOnANonMarkedEnemy_Misses()
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

            Assert.IsTrue(missed, "A trace for a non-marked enemy must raise a miss.");
            Assert.That(far.CurrentHealth, Is.EqualTo(farData.maxHealth),
                "Only the active clue is drawable; the unmarked enemy takes no damage.");
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
