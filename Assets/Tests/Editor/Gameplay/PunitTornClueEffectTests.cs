using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public sealed class PunitTornClueEffectTests
    {
        private readonly List<UnityEngine.Object> _objectsToDestroy =
            new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            PunitTornController.ResetRegistryForTests();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    UnityEngine.Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
            PunitTornController.ResetRegistryForTests();
        }

        [Test]
        public void Controller_ActivatesPersistsAndResetsOnDefeatAndReuse()
        {
            Enemy enemy = CreateShellEnemy();
            EnemyDataSO punitData = CreateData("punit", EnemyLearningAbility.TornContext);

            Assert.IsTrue(enemy.Initialize(punitData));
            PunitTornController controller = enemy.GetComponent<PunitTornController>();
            Assert.IsNotNull(controller, "TornContext must attach its state controller.");
            controller.SetSuppressedForIntroductionSpawn(false);

            Assert.IsTrue(PunitTornController.IsAnyActive(), "A live, armed Punit starts the effect.");
            Assert.IsTrue(PunitTornController.IsAnyActive(), "The effect persists while Punit remains live.");

            controller.SetSuppressedForIntroductionSpawn(true);
            Assert.IsFalse(PunitTornController.IsAnyActive(), "An introduction-suppressed spawn stays inert.");
            controller.SetSuppressedForIntroductionSpawn(false);
            Assert.IsTrue(PunitTornController.IsAnyActive(), "Lifting suppression activates the same spawn.");

            enemy.Defeat();
            Assert.IsFalse(PunitTornController.IsAnyActive(), "Defeat ends Punit's HUD state immediately.");

            enemy.gameObject.SetActive(true);
            EnemyDataSO ordinaryData = CreateData("ordinary", EnemyLearningAbility.None);
            Assert.IsTrue(enemy.Initialize(ordinaryData));
            Assert.IsFalse(enemy.GetComponent<PunitTornController>().enabled,
                "Reusing Punit's pooled shell for an ordinary enemy disables the ability.");
            Assert.IsFalse(PunitTornController.IsAnyActive(), "The new occupant cannot inherit Punit's state.");
        }

        [Test]
        public void RailFragments_TearPersistRepairAndResetToAuthoredPositions()
        {
            ActiveCluePresenter presenter = CreateRailPresenter(
                out RectTransform[] fragments, out RectTransform[] neighboringFragments);
            Vector3[] original = CapturePositions(fragments);
            Vector3[] neighboringOriginal = CapturePositions(neighboringFragments);
            Enemy enemy = CreateShellEnemy();
            Assert.IsTrue(enemy.Initialize(CreateData("punit", EnemyLearningAbility.TornContext)));
            enemy.GetComponent<PunitTornController>().SetSuppressedForIntroductionSpawn(false);

            TickTorn(presenter, 0.14f);
            float activationProgress = GetPrivateFloat(presenter, "_punitTearProgress");
            Assert.That(activationProgress, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.IsTrue(PositionsDiffer(original, CapturePositions(fragments)),
                "Activation must pull the two halves away from the sentence seam.");
            CollectionAssert.AreEqual(neighboringOriginal, CapturePositions(neighboringFragments),
                "Only the current word may move; neighbouring paragraph words stay readable.");
            Assert.Less(fragments[0].localPosition.x, original[0].x,
                "The left half must move left from the rail's centre seam.");
            Assert.Greater(fragments[1].localPosition.x, original[1].x,
                "The right half must move right from the rail's centre seam.");
            ProceduralClueTearGraphic tear = (ProceduralClueTearGraphic)typeof(ActiveCluePresenter)
                .GetField("_punitTearGraphic", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(presenter);
            RectTransform boundRail = (RectTransform)typeof(ActiveCluePresenter)
                .GetField("_punitTornBoundRail", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(presenter);
            IList tornFragments = (IList)typeof(ActiveCluePresenter)
                .GetField("_punitRailFragments", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(presenter);
            Assert.AreSame(fragments[0].parent, boundRail, "The presenter must bind the live rail.");
            Assert.That(tornFragments.Count, Is.GreaterThan(0), "The live rail must have tear fragments.");
            Assert.IsNotNull(tear, "The tear edge is drawn by the procedural HUD mesh.");
            Assert.AreSame(fragments[0].parent, tear.transform.parent,
                "The procedural mesh must be attached to the live rail's coordinate space.");
            RectTransform rail = (RectTransform)fragments[0].parent;
            float expectedSplit = tear.rectTransform.InverseTransformPoint(rail.TransformPoint(
                new Vector3(GetPrivateFloat(presenter, "_punitRailSplitLocalX"), 0f, 0f))).x;
            Assert.AreEqual(expectedSplit, GetPrivateFloat(tear, "_splitX"), 0.001f,
                "The seam is calculated inside the active word, not across the paragraph.");
            float expectedTop = Mathf.Min(tear.rectTransform.rect.yMax,
                GetPrivateFloat(presenter, "_punitRailTopY"));
            float expectedBottom = Mathf.Max(tear.rectTransform.rect.yMin,
                GetPrivateFloat(presenter, "_punitRailBottomY"));
            Assert.AreEqual(expectedTop, GetPrivateFloat(tear, "_topY"), 0.001f,
                "The edge is bounded to the current word's rail row.");
            Assert.AreEqual(expectedBottom, GetPrivateFloat(tear, "_bottomY"), 0.001f,
                "The edge does not cut across neighbouring paragraph rows.");
            Assert.IsTrue(tear.IsVisible);

            TickTorn(presenter, 0.14f);
            Assert.AreEqual(1f, GetPrivateFloat(presenter, "_punitTearProgress"), 0.001f);
            Vector3[] torn = CapturePositions(fragments);
            TickTorn(presenter, 0.14f);
            CollectionAssert.AreEqual(torn, CapturePositions(fragments),
                "An active Punit keeps both fragments torn instead of drifting each frame.");

            enemy.GetComponent<PunitTornController>().NotifyDefeated();
            TickTorn(presenter, 0.14f);
            Assert.AreEqual(0.5f, GetPrivateFloat(presenter, "_punitTearProgress"), 0.001f,
                "Removing the last active Punit should begin an animated repair.");
            Assert.IsTrue(PositionsDiffer(original, CapturePositions(fragments)),
                "The fragments remain partially separated during the repair animation.");

            TickTorn(presenter, 0.14f);
            Assert.AreEqual(0f, GetPrivateFloat(presenter, "_punitTearProgress"), 0.001f);
            CollectionAssert.AreEqual(original, CapturePositions(fragments),
                "Repair must return every rail fragment to its exact authored layout.");

            enemy.GetComponent<PunitTornController>().SetSuppressedForIntroductionSpawn(false);
            TickTorn(presenter, 0.28f);
            Assert.IsTrue(PositionsDiffer(original, CapturePositions(fragments)));
            InvokePrivate(presenter, "ResetPunitTornEffect");
            Assert.AreEqual(0f, GetPrivateFloat(presenter, "_punitTearProgress"), 0.001f);
            CollectionAssert.AreEqual(original, CapturePositions(fragments),
                "HUD reset must restore the normal slot layout immediately.");

            enemy.gameObject.SetActive(false);
        }

        [Test]
        public void ObjectiveRail_UsesCompletionOrderAndRebindsWithinTheSameUnit()
        {
            ActiveCluePresenter presenter = CreateMultiwordObjectiveRailPresenter(
                out RectTransform[] firstWordFragments,
                out RectTransform[] secondWordFragments,
                out RestorationObjectiveController objective);
            Vector3[] firstWordOriginal = CapturePositions(firstWordFragments);
            Vector3[] secondWordOriginal = CapturePositions(secondWordFragments);

            Enemy enemy = CreateShellEnemy();
            Assert.IsTrue(enemy.Initialize(CreateData("punit", EnemyLearningAbility.TornContext)));
            enemy.GetComponent<PunitTornController>().SetSuppressedForIntroductionSpawn(false);

            Assert.AreEqual("occurrence.ya", objective.State.NextTargetOccurrenceId,
                "The objective's lowest completion order is not the first displayed target.");
            MethodInfo getRequiredSlot = typeof(ActiveCluePresenter).GetMethod(
                "GetCurrentRequiredRailSlot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(getRequiredSlot);
            object requiredSlot = getRequiredSlot.Invoke(presenter, null);
            Assert.IsNotNull(requiredSlot);
            Assert.AreEqual("occurrence.ya", requiredSlot.GetType()
                .GetField("OccurrenceId", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(requiredSlot),
                "Readability safeguards must identify the actual next occurrence.");
            TextMeshProUGUI requiredLabel = (TextMeshProUGUI)requiredSlot.GetType()
                .GetField("Label", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(requiredSlot);
            Assert.AreEqual("YA", requiredLabel.text);

            TickTorn(presenter, 0.28f);
            CollectionAssert.AreEqual(firstWordOriginal, CapturePositions(firstWordFragments),
                "Punit must not select the first display-order word when another target is due first.");
            Assert.IsTrue(PositionsDiffer(secondWordOriginal, CapturePositions(secondWordFragments)),
                "Targets separated only by a non-whitespace literal belong to the required word.");

            Assert.IsTrue(objective.TryRestore("symbol.punit.ya").Applied);
            Assert.AreEqual("paragraph-line", objective.State.ActiveUnitId);
            Assert.AreEqual("occurrence.ra", objective.State.NextTargetOccurrenceId,
                "Advancing within the word keeps its remaining target active.");
            TickTorn(presenter, 0.01f);
            CollectionAssert.AreEqual(firstWordOriginal, CapturePositions(firstWordFragments));
            Assert.IsTrue(PositionsDiffer(secondWordOriginal, CapturePositions(secondWordFragments)));

            Assert.IsTrue(objective.TryRestore("symbol.punit.ra").Applied);
            Assert.AreEqual("paragraph-line", objective.State.ActiveUnitId,
                "Completing one word must not advance past its still-incomplete paragraph line.");
            Assert.AreEqual("occurrence.ba", objective.State.NextTargetOccurrenceId);

            TickTorn(presenter, 0.01f);
            Assert.IsTrue(PositionsDiffer(firstWordOriginal, CapturePositions(firstWordFragments)),
                "The next target word is rebound after its predecessor is completed.");
            CollectionAssert.AreEqual(secondWordOriginal, CapturePositions(secondWordFragments),
                "Changing words within one unit restores the previous word's exact positions.");

            enemy.gameObject.SetActive(false);
        }

        [Test]
        public void TmpClueText_TearsWithoutChangingSourceAndRestoresVerticesAfterRepair()
        {
            var canvasObject = new GameObject("PunitTorn_TMP_TestCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _objectsToDestroy.Add(canvasObject);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var clueObject = new GameObject("PunitTorn_TMP_Clue", typeof(RectTransform));
            clueObject.transform.SetParent(canvasObject.transform, false);
            TextMeshProUGUI clue = clueObject.AddComponent<TextMeshProUGUI>();
            clue.font = TMP_Settings.defaultFontAsset;
            clue.fontSize = 34f;
            clue.rectTransform.sizeDelta = new Vector2(720f, 90f);
            clue.text = "Keep the whole sentence";
            clue.ForceMeshUpdate(true, true);
            _objectsToDestroy.Add(clueObject);

            var presenterObject = new GameObject("PunitTorn_TMP_Presenter", typeof(RectTransform));
            presenterObject.transform.SetParent(canvasObject.transform, false);
            ActiveCluePresenter presenter = presenterObject.AddComponent<ActiveCluePresenter>();
            SetPrivateField(presenter, "_clueText", clue);
            SetPrivateField(presenter, "_punitTearTransitionSeconds", 0.2f);
            _objectsToDestroy.Add(presenterObject);

            Vector3[][] original = CaptureTextVertices(clue, forceMeshUpdate: true);
            Enemy enemy = CreateShellEnemy();
            Assert.IsTrue(enemy.Initialize(CreateData("punit", EnemyLearningAbility.TornContext)));
            enemy.GetComponent<PunitTornController>().SetSuppressedForIntroductionSpawn(false);

            TickTorn(presenter, 0.1f);
            TickTorn(presenter, 0.1f);
            Assert.AreEqual("Keep the whole sentence", clue.text,
                "The effect must not mutate the authored or active clue string.");
            Assert.IsTrue(TextVerticesDiffer(original, CaptureTextVertices(clue, forceMeshUpdate: false)),
                "The TMP fallback must split the rendered glyph meshes while preserving their styles.");

            enemy.GetComponent<PunitTornController>().NotifyDefeated();
            TickTorn(presenter, 0.1f);
            TickTorn(presenter, 0.1f);
            Assert.AreEqual("Keep the whole sentence", clue.text);
            AssertTextVerticesEqual(original, CaptureTextVertices(clue, forceMeshUpdate: false));
            Assert.AreEqual(0f, GetPrivateFloat(presenter, "_punitTearProgress"), 0.001f);

            enemy.gameObject.SetActive(false);
        }

        private ActiveCluePresenter CreateRailPresenter(
            out RectTransform[] fragments, out RectTransform[] neighboringFragments)
        {
            var canvasObject = new GameObject("PunitTorn_Rail_TestCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1200f, 600f);
            _objectsToDestroy.Add(canvasObject);

            var railObject = new GameObject("PunitTorn_Rail", typeof(RectTransform));
            railObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rail = railObject.GetComponent<RectTransform>();
            rail.pivot = new Vector2(0.5f, 0f);
            rail.sizeDelta = new Vector2(360f, 90f);
            _objectsToDestroy.Add(railObject);

            var firstCharacter = CreateCharacter("symbol.punit.ba", "ba");
            var secondCharacter = CreateCharacter("symbol.punit.ha", "ha");
            var neighborFirstCharacter = CreateCharacter("symbol.punit.ya", "ya");
            var neighborSecondCharacter = CreateCharacter("symbol.punit.ra", "ra");
            var currentWord = new FocusWordDefinition
            {
                stableId = "current-word",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = firstCharacter },
                    new SymbolValueReference { symbol = secondCharacter },
                },
            };
            var neighborWord = new FocusWordDefinition
            {
                stableId = "neighbor-word",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = neighborFirstCharacter },
                    new SymbolValueReference { symbol = neighborSecondCharacter },
                },
            };
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.focusWords.Add(currentWord);
            level.focusWords.Add(neighborWord);
            _objectsToDestroy.Add(level);

            var left = CreateRailFragment(rail, "[Runtime] RestorationSlot_0", new Vector2(28f, -8f));
            var right = CreateRailFragment(rail, "[Runtime] RestorationSlot_1", new Vector2(92f, -8f));
            var neighborLeft = CreateRailFragment(rail, "[Runtime] RestorationSlot_2", new Vector2(220f, -8f));
            var neighborRight = CreateRailFragment(rail, "[Runtime] RestorationSlot_3", new Vector2(284f, -8f));
            var leftLabel = CreateRailLabel(rail, "[Runtime] RestorationSlotLabel_0", new Vector2(28f, -52f), "BA");
            var rightLabel = CreateRailLabel(rail, "[Runtime] RestorationSlotLabel_1", new Vector2(92f, -52f), "HA");
            var neighborLeftLabel = CreateRailLabel(rail, "[Runtime] RestorationSlotLabel_2", new Vector2(220f, -52f), "YA");
            var neighborRightLabel = CreateRailLabel(rail, "[Runtime] RestorationSlotLabel_3", new Vector2(284f, -52f), "RA");
            fragments = new[] { left, right, leftLabel, rightLabel };
            neighboringFragments = new[]
                { neighborLeft, neighborRight, neighborLeftLabel, neighborRightLabel };

            var presenterObject = new GameObject("PunitTorn_Rail_Presenter", typeof(RectTransform));
            presenterObject.transform.SetParent(canvasObject.transform, false);
            ActiveCluePresenter presenter = presenterObject.AddComponent<ActiveCluePresenter>();
            SetPrivateField(presenter, "_railRoot", railObject);
            SetPrivateField(presenter, "_punitTearTransitionSeconds", 0.28f);
            SetPrivateField(presenter, "_level", level);
            presenter.RestorationState.Configure(level.focusWords);
            _objectsToDestroy.Add(presenterObject);

            Enemy currentClue = CreateShellEnemy();
            currentClue.AssignCharacter(firstCharacter);
            SetPrivateField(presenter, "_currentClue", currentClue);

            IList slots = (IList)typeof(ActiveCluePresenter)
                .GetField("_railSlots", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(presenter);
            System.Type slotType = typeof(ActiveCluePresenter).GetNestedType(
                "RailSlot", BindingFlags.NonPublic);
            slots.Add(CreateRailSlot(slotType, left, leftLabel.GetComponent<TextMeshProUGUI>(), currentWord, 0));
            slots.Add(CreateRailSlot(slotType, right, rightLabel.GetComponent<TextMeshProUGUI>(), currentWord, 1));
            slots.Add(CreateRailSlot(slotType, neighborLeft, neighborLeftLabel.GetComponent<TextMeshProUGUI>(), neighborWord, 0));
            slots.Add(CreateRailSlot(slotType, neighborRight, neighborRightLabel.GetComponent<TextMeshProUGUI>(), neighborWord, 1));
            return presenter;
        }

        private ActiveCluePresenter CreateMultiwordObjectiveRailPresenter(
            out RectTransform[] firstWordFragments,
            out RectTransform[] secondWordFragments,
            out RestorationObjectiveController objective)
        {
            var canvasObject = new GameObject("PunitTorn_Objective_TestCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1200f, 600f);
            _objectsToDestroy.Add(canvasObject);

            var railObject = new GameObject("PunitTorn_Objective_Rail", typeof(RectTransform));
            railObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rail = railObject.GetComponent<RectTransform>();
            rail.pivot = new Vector2(0.5f, 0f);
            rail.sizeDelta = new Vector2(360f, 90f);
            _objectsToDestroy.Add(railObject);

            BaybayinCharacterSO ba = CreateCharacter("symbol.punit.ba", "ba");
            BaybayinCharacterSO ha = CreateCharacter("symbol.punit.ha", "ha");
            BaybayinCharacterSO ya = CreateCharacter("symbol.punit.ya", "ya");
            BaybayinCharacterSO ra = CreateCharacter("symbol.punit.ra", "ra");
            const string unitId = "paragraph-line";
            var unit = new RestorationObjectiveUnit
            {
                stableId = unitId,
                tokens = new List<RestorationObjectiveToken>
                {
                    CreateObjectiveTarget("occurrence.ba", ba, completionOrder: 2),
                    new RestorationObjectiveToken
                    {
                        kind = RestorationTokenKind.Literal,
                        literalText = "-",
                    },
                    CreateObjectiveTarget("occurrence.ha", ha, completionOrder: 3),
                    new RestorationObjectiveToken
                    {
                        kind = RestorationTokenKind.Literal,
                        literalText = " and ",
                    },
                    CreateObjectiveTarget("occurrence.ya", ya, completionOrder: 0),
                    new RestorationObjectiveToken
                    {
                        kind = RestorationTokenKind.Literal,
                        literalText = "-",
                    },
                    CreateObjectiveTarget("occurrence.ra", ra, completionOrder: 1),
                },
            };

            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.restorationObjective = new RestorationObjectiveDefinition
            {
                displayMode = RestorationDisplayMode.MarkedContext,
                units = new List<RestorationObjectiveUnit> { unit },
            };
            _objectsToDestroy.Add(level);

            var firstLeft = CreateRailFragment(rail, "ObjectiveSlot_0", new Vector2(28f, -8f));
            var firstRight = CreateRailFragment(rail, "ObjectiveSlot_1", new Vector2(92f, -8f));
            var secondLeft = CreateRailFragment(rail, "ObjectiveSlot_2", new Vector2(220f, -8f));
            var secondRight = CreateRailFragment(rail, "ObjectiveSlot_3", new Vector2(284f, -8f));
            var firstLeftLabel = CreateRailLabel(rail, "ObjectiveLabel_0", new Vector2(28f, -52f), "BA");
            var firstRightLabel = CreateRailLabel(rail, "ObjectiveLabel_1", new Vector2(92f, -52f), "HA");
            var secondLeftLabel = CreateRailLabel(rail, "ObjectiveLabel_2", new Vector2(220f, -52f), "YA");
            var secondRightLabel = CreateRailLabel(rail, "ObjectiveLabel_3", new Vector2(284f, -52f), "RA");
            firstWordFragments = new[] { firstLeft, firstRight, firstLeftLabel, firstRightLabel };
            secondWordFragments = new[] { secondLeft, secondRight, secondLeftLabel, secondRightLabel };

            var objectiveObject = new GameObject("PunitTorn_ObjectiveController");
            objective = objectiveObject.AddComponent<RestorationObjectiveController>();
            objective.Configure(level);
            _objectsToDestroy.Add(objectiveObject);

            var presenterObject = new GameObject("PunitTorn_Objective_Presenter", typeof(RectTransform));
            presenterObject.transform.SetParent(canvasObject.transform, false);
            ActiveCluePresenter presenter = presenterObject.AddComponent<ActiveCluePresenter>();
            SetPrivateField(presenter, "_railRoot", railObject);
            SetPrivateField(presenter, "_punitTearTransitionSeconds", 0.28f);
            SetPrivateField(presenter, "_level", level);
            presenter.RestorationState.Configure(level.focusWords);
            presenter.SetRestorationObjectiveController(objective);
            _objectsToDestroy.Add(presenterObject);

            Enemy currentClue = CreateShellEnemy();
            currentClue.AssignCharacter(ba);
            SetPrivateField(presenter, "_currentClue", currentClue);

            IList slots = (IList)typeof(ActiveCluePresenter)
                .GetField("_railSlots", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(presenter);
            System.Type slotType = typeof(ActiveCluePresenter).GetNestedType(
                "RailSlot", BindingFlags.NonPublic);
            slots.Add(CreateObjectiveRailSlot(
                slotType,
                firstLeft,
                firstLeftLabel.GetComponent<TextMeshProUGUI>(),
                unitId,
                "occurrence.ba",
                0));
            slots.Add(CreateObjectiveRailSlot(
                slotType,
                firstRight,
                firstRightLabel.GetComponent<TextMeshProUGUI>(),
                unitId,
                "occurrence.ha",
                1));
            slots.Add(CreateObjectiveRailSlot(
                slotType,
                secondLeft,
                secondLeftLabel.GetComponent<TextMeshProUGUI>(),
                unitId,
                "occurrence.ya",
                2));
            slots.Add(CreateObjectiveRailSlot(
                slotType,
                secondRight,
                secondRightLabel.GetComponent<TextMeshProUGUI>(),
                unitId,
                "occurrence.ra",
                3));
            return presenter;
        }

        private static RestorationObjectiveToken CreateObjectiveTarget(
            string occurrenceId, BaybayinCharacterSO character, int completionOrder = -1)
        {
            return new RestorationObjectiveToken
            {
                kind = RestorationTokenKind.Target,
                occurrenceId = occurrenceId,
                completionOrder = completionOrder,
                target = new SymbolValueReference { symbol = character },
            };
        }

        private object CreateObjectiveRailSlot(
            System.Type slotType,
            RectTransform anchor,
            TextMeshProUGUI label,
            string unitId,
            string occurrenceId,
            int decompositionIndex)
        {
            object slot = CreateRailSlot(slotType, anchor, label, null, decompositionIndex);
            slotType.GetField("UnitId", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(slot, unitId);
            slotType.GetField("OccurrenceId", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(slot, occurrenceId);
            return slot;
        }

        private RectTransform CreateRailLabel(
            Transform parent, string name, Vector2 position, string text)
        {
            RectTransform rect = CreateRailFragment(parent, name, position);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.ForceMeshUpdate(true, true);
            return rect;
        }

        private RectTransform CreateRailFragment(Transform parent, string name, Vector2 position)
        {
            var fragmentObject = new GameObject(name, typeof(RectTransform));
            fragmentObject.transform.SetParent(parent, false);
            RectTransform rect = fragmentObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(28f, 28f);
            rect.anchoredPosition = position;
            return rect;
        }

        private object CreateRailSlot(
            System.Type slotType,
            RectTransform anchor,
            TextMeshProUGUI label,
            FocusWordDefinition word,
            int decompositionIndex)
        {
            object slot = System.Activator.CreateInstance(slotType, nonPublic: true);
            slotType.GetField("Anchor", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(slot, anchor);
            slotType.GetField("Label", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(slot, label);
            slotType.GetField("Word", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(slot, word);
            slotType.GetField("DecompositionIndex", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(slot, decompositionIndex);
            return slot;
        }

        private BaybayinCharacterSO CreateCharacter(string stableId, string syllable)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.stableId = stableId;
            character.syllable = syllable;
            _objectsToDestroy.Add(character);
            return character;
        }

        private Enemy CreateShellEnemy()
        {
            var enemyObject = new GameObject("PunitTorn_Enemy_Test");
            enemyObject.SetActive(false);
            enemyObject.AddComponent<SpriteRenderer>();
            enemyObject.AddComponent<BoxCollider2D>();
            enemyObject.AddComponent<EnemyMover>();
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            enemyObject.SetActive(true);
            _objectsToDestroy.Add(enemyObject);
            return enemy;
        }

        private EnemyDataSO CreateData(string enemyId, EnemyLearningAbility ability)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = enemyId;
            data.displayName = enemyId;
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            data.useHurtFeedback = false;
            data.learningAbility = ability;
            _objectsToDestroy.Add(data);
            return data;
        }

        private static Vector3[] CapturePositions(IReadOnlyList<RectTransform> transforms)
        {
            var result = new Vector3[transforms.Count];
            for (int i = 0; i < transforms.Count; i++)
                result[i] = transforms[i].localPosition;
            return result;
        }

        private static bool PositionsDiffer(IReadOnlyList<Vector3> first, IReadOnlyList<Vector3> second)
        {
            for (int i = 0; i < first.Count; i++)
            {
                if ((first[i] - second[i]).sqrMagnitude > 0.001f)
                    return true;
            }

            return false;
        }

        private static Vector3[][] CaptureTextVertices(TMP_Text text, bool forceMeshUpdate)
        {
            if (forceMeshUpdate)
                text.ForceMeshUpdate(true, true);
            TMP_TextInfo info = text.textInfo;
            var result = new Vector3[info.meshInfo.Length][];
            for (int i = 0; i < result.Length; i++)
                result[i] = (Vector3[])info.meshInfo[i].vertices.Clone();
            return result;
        }

        private static bool TextVerticesDiffer(Vector3[][] first, Vector3[][] second)
        {
            for (int mesh = 0; mesh < first.Length; mesh++)
            {
                for (int vertex = 0; vertex < first[mesh].Length; vertex++)
                {
                    if ((first[mesh][vertex] - second[mesh][vertex]).sqrMagnitude > 0.001f)
                        return true;
                }
            }

            return false;
        }

        private static void AssertTextVerticesEqual(Vector3[][] expected, Vector3[][] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (int mesh = 0; mesh < expected.Length; mesh++)
            {
                Assert.AreEqual(expected[mesh].Length, actual[mesh].Length);
                for (int vertex = 0; vertex < expected[mesh].Length; vertex++)
                {
                    Assert.That(Vector3.Distance(expected[mesh][vertex], actual[mesh][vertex]),
                        Is.LessThan(0.001f), $"Mesh {mesh}, vertex {vertex} did not reconnect.");
                }
            }
        }

        private static void TickTorn(ActiveCluePresenter presenter, float deltaTime)
        {
            MethodInfo method = typeof(ActiveCluePresenter).GetMethod(
                "WatchPunitTornState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(presenter, new object[] { deltaTime });
        }

        private static float GetPrivateFloat(object target, string fieldName)
        {
            return (float)target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing {target.GetType().Name}.{fieldName}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing {target.GetType().Name}.{methodName}.");
            method.Invoke(target, null);
        }
    }
}
