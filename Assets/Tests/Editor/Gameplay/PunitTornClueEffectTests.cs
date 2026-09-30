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
            ActiveCluePresenter presenter = CreateRailPresenter(out RectTransform[] fragments);
            Vector3[] original = CapturePositions(fragments);
            Enemy enemy = CreateShellEnemy();
            Assert.IsTrue(enemy.Initialize(CreateData("punit", EnemyLearningAbility.TornContext)));
            enemy.GetComponent<PunitTornController>().SetSuppressedForIntroductionSpawn(false);

            TickTorn(presenter, 0.14f);
            float activationProgress = GetPrivateFloat(presenter, "_punitTearProgress");
            Assert.That(activationProgress, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.IsTrue(PositionsDiffer(original, CapturePositions(fragments)),
                "Activation must pull the two halves away from the sentence seam.");
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
            Assert.AreEqual(0f, GetPrivateFloat(tear, "_splitX"), 0.001f,
                "The procedural edge must split between the two centred slot halves.");
            Assert.AreEqual(tear.rectTransform.rect.yMax, GetPrivateFloat(tear, "_topY"), 0.001f,
                "The edge must span the full height in its own RectTransform space.");
            Assert.AreEqual(tear.rectTransform.rect.yMin, GetPrivateFloat(tear, "_bottomY"), 0.001f,
                "The edge must start at the lower bound in its own RectTransform space.");
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

        private ActiveCluePresenter CreateRailPresenter(out RectTransform[] fragments)
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
            rail.sizeDelta = new Vector2(240f, 90f);
            _objectsToDestroy.Add(railObject);

            var left = CreateRailFragment(rail, "[Runtime] RestorationSlot_0", new Vector2(74f, -8f));
            var right = CreateRailFragment(rail, "[Runtime] RestorationSlot_1", new Vector2(138f, -8f));
            var leftLabel = CreateRailFragment(rail, "[Runtime] RestorationSlotLabel_0", new Vector2(74f, -52f));
            var rightLabel = CreateRailFragment(rail, "[Runtime] RestorationSlotLabel_1", new Vector2(138f, -52f));
            fragments = new[] { left, right, leftLabel, rightLabel };

            var presenterObject = new GameObject("PunitTorn_Rail_Presenter", typeof(RectTransform));
            presenterObject.transform.SetParent(canvasObject.transform, false);
            ActiveCluePresenter presenter = presenterObject.AddComponent<ActiveCluePresenter>();
            SetPrivateField(presenter, "_railRoot", railObject);
            SetPrivateField(presenter, "_punitTearTransitionSeconds", 0.28f);
            _objectsToDestroy.Add(presenterObject);

            IList slots = (IList)typeof(ActiveCluePresenter)
                .GetField("_railSlots", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(presenter);
            System.Type slotType = typeof(ActiveCluePresenter).GetNestedType(
                "RailSlot", BindingFlags.NonPublic);
            slots.Add(CreateRailSlot(slotType, left));
            slots.Add(CreateRailSlot(slotType, right));
            return presenter;
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

        private object CreateRailSlot(System.Type slotType, RectTransform anchor)
        {
            object slot = System.Activator.CreateInstance(slotType, nonPublic: true);
            slotType.GetField("Anchor", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(slot, anchor);
            return slot;
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
