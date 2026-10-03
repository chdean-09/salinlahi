using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Salinlahi.Debug.Sandbox;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    /// <summary>
    /// SALIN-232. The Wave Cleared screen's load-bearing behaviour: defense completion
    /// presents the screen and NOTHING advances until the continue button is tapped
    /// (AC-5, AC-7), once per Defense-phase completion — i.e. once per segment.
    ///
    /// WHY THIS FIXTURE EXISTS SEPARATELY FROM THE EDITMODE TESTS. The obvious-looking
    /// place for this screen is the tail of ExecuteDefense, after its
    /// <c>WaitUntil(_machine.Phase != LevelPhase.Defense)</c>. That wait only releases
    /// AFTER HandleDefenseComplete has already advanced the machine, so a screen shown
    /// there is a screen shown one phase too late. It compiles, throws nothing, and passes
    /// every other test in the repo. PM-1 and PM-2 below are the only assertions that can
    /// tell that defect from a correct implementation, which is why PM-2 is written as an
    /// explicit negative control rather than folded into PM-1.
    /// </summary>
    [TestFixture]
    public sealed class WaveClearedScreenFlowTests
    {
        private const string MissingWaveManagerError =
            "[Salinlahi] LevelFlowController: WaveManager reference missing.";

        private readonly List<Object> _objectsToDestroy = new();

        [SetUp]
        public void SetUp()
        {
            SandboxMode.Deactivate();
            LevelFlowController.SetSkipReadyScreenForTests(true);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            SandboxMode.Deactivate();
            LevelFlowController.SetSkipReadyScreenForTests(false);
            ClearSingletonInstance<GameManager>();
            Time.timeScale = 1f;

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();

            // The screen builds its own GameObject and canvas at runtime; neither is owned
            // by this fixture, so both outlive the test unless they are cleared here.
            foreach (WaveClearedScreenUI screen in Object.FindObjectsByType<WaveClearedScreenUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (screen != null)
                    Object.DestroyImmediate(screen.gameObject);
            }

            GameObject canvas = GameObject.Find("[Runtime] WaveClearedCanvas");
            while (canvas != null)
            {
                Object.DestroyImmediate(canvas);
                canvas = GameObject.Find("[Runtime] WaveClearedCanvas");
            }

            ChallengeRuntimeState.Clear();
            TutorialRuntimeState.Clear();
        }

        // ------------------------------------------------------------------
        // PM-1 / PM-2 — the hold.
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DefenseCompletion_PresentsTheScreen_AndTheMachineStaysInDefense()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestFlowController controller = Bootstrap(ConfigureSingleSegment);

            yield return WaitFrames(10);
            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase,
                "Setup: the flow must be live in Defense before the completion.");

            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);

            WaveClearedScreenUI screen = FindScreen();
            Assert.IsNotNull(screen, "AC-1: defense completion must present the Wave Cleared screen.");
            Assert.IsTrue(screen.IsPresented);
            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase,
                "AC-5/AC-7: the machine must still be in Defense while the banner is up. "
                + "ContextChallenge here means the screen was shown AFTER the phase advanced "
                + "— the exact defect a screen placed at the tail of ExecuteDefense produces.");
        }

        /// <summary>
        /// PM-2 — negative control. Proves the hold is real and that PM-4 is not passing for
        /// free. Without this, an implementation that advanced a few frames later would look
        /// identical to a correct one.
        /// </summary>
        [UnityTest]
        public IEnumerator WithoutATap_NothingAdvances_AfterThirtyFrames()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestFlowController controller = Bootstrap(ConfigureSingleSegment);

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(30);

            Assert.IsTrue(FindScreen() != null && FindScreen().IsPresented,
                "Setup: the screen must still be up.");
            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase,
                "AC-7: nothing advances until the button is tapped.");
            Assert.AreEqual(0, controller.CommitCalls,
                "AC-7: an untapped screen must not let the level commit either.");
        }

        // ------------------------------------------------------------------
        // PM-4 — the release.
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TappingContinue_AdvancesToContextChallenge_ExactlyOnce()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestFlowController controller = Bootstrap(ConfigureSingleSegment);

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);

            WaveClearedScreenUI screen = FindScreen();
            Assert.IsNotNull(screen, "Setup: the screen must be presented before the tap.");
            screen.Continue();
            yield return WaitFrames(10);

            Assert.AreEqual(LevelPhase.ContextChallenge, MachineOf(controller).Phase,
                "AC-5: the tap must carry the flow into the context challenge.");
            Assert.IsFalse(screen.IsPresented, "The tap must dismiss the screen.");

            // A second tap on a stale reference must be inert: the gate is released, so the
            // deferred report must not fire again. Nothing in the machine would throw — it
            // would simply be a second ReportDefenseComplete against a phase that has moved.
            screen.Continue();
            yield return WaitFrames(10);

            Assert.AreEqual(LevelPhase.ContextChallenge, MachineOf(controller).Phase,
                "A repeated tap must be inert.");
            Assert.AreEqual(0, controller.CommitCalls,
                "No campaign progress may commit while the challenge is still open.");
        }

        // ------------------------------------------------------------------
        // PM-3 — once per segment, not once per level and not once per wave.
        // ------------------------------------------------------------------

        /// <summary>
        /// SALIN-226 / D-010. A segmented level re-enters Defense once per segment, and the
        /// per-segment signal is the same OnDefenseComplete
        /// (WaveManager.cs:451-453: "a segment deliberately adds no second signal"). The
        /// per-wave EventBus.OnWaveCleared is deliberately NOT used — nothing in the project
        /// has ever subscribed to it. So the screen must appear once per segment: on the
        /// authored two-segment shape (Level5_Config.asset:249-255) that is twice.
        /// </summary>
        [UnityTest]
        public IEnumerator TwoSegments_PresentTheScreenOncePerSegment()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestFlowController controller = Bootstrap(ConfigureTwoSegments);

            Assert.AreEqual(2, controller.SegmentCount,
                "Setup: the config must express two segments.");

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);

            Assert.IsTrue(FindScreen() != null && FindScreen().IsPresented,
                "Segment 1's clear must present the screen.");
            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase);
            FindScreen().Continue();
            yield return WaitFrames(10);

            Assert.AreEqual(LevelPhase.ContextChallenge, MachineOf(controller).Phase,
                "Setup: segment 1's restoration must open.");
            ChallengeFlowController challenge =
                GetPrivateField<ChallengeFlowController>(controller, "_challengeFlowController");
            Assert.IsNotNull(challenge);
            challenge.SubmitPlacement("w-1");
            yield return WaitFrames(10);

            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase,
                "Setup: clearing segment 1 must resume the next defense leg.");
            Assert.IsFalse(FindScreen().IsPresented,
                "The screen must not still be up when segment 2's defense begins.");

            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);

            Assert.IsTrue(FindScreen().IsPresented,
                "Segment 2's clear must present the screen a SECOND time. A once-per-level "
                + "gate would leave the flow advancing straight through here, which is the "
                + "case a re-entrancy guard written too broadly produces.");
            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase,
                "AC-7 holds on every segment, not only the first.");
            Assert.AreEqual(1, ScreenCount(),
                "The second presentation must reuse the one screen, not stack another.");

            FindScreen().Continue();
            yield return WaitFrames(10);
            Assert.AreEqual(LevelPhase.ContextChallenge, MachineOf(controller).Phase);
        }

        // ------------------------------------------------------------------
        // PM-5 — terminal cleanup.
        // ------------------------------------------------------------------

        /// <summary>
        /// A restart or a return to the menu can land while the banner is up. The overlay
        /// claims sortingOrder 300, so left active it sits over the terminal screens with a
        /// continue button whose deferred report the terminal machine will refuse — a dead
        /// modal over a level that has already ended.
        /// </summary>
        [UnityTest]
        public IEnumerator AbortWhilePresented_HidesTheScreen_AndDoesNotAdvance()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestFlowController controller = Bootstrap(ConfigureSingleSegment);

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);
            Assert.IsTrue(FindScreen().IsPresented, "Setup: the screen must be up before the abort.");

            GameManager.Instance.AbortCurrentLevelAttempt();
            yield return WaitFrames(10);

            Assert.AreEqual(LevelPhase.Exited, MachineOf(controller).Phase,
                "The abort must drive the machine terminal.");
            Assert.IsFalse(FindScreen().IsPresented,
                "Terminal cleanup must hide the banner.");
            Assert.AreEqual(0, controller.CommitCalls,
                "An abandoned attempt must never commit campaign progress.");

            // A straggler tap from the dismissed screen must not resurrect the flow.
            FindScreen().Continue();
            yield return WaitFrames(10);
            Assert.AreEqual(LevelPhase.Exited, MachineOf(controller).Phase,
                "A late tap must not advance an exited attempt into ContextChallenge.");
        }

        // ------------------------------------------------------------------
        // Fixture
        // ------------------------------------------------------------------

        private static WaveClearedScreenUI FindScreen()
        {
            return Object.FindFirstObjectByType<WaveClearedScreenUI>(FindObjectsInactive.Include);
        }

        private static int ScreenCount()
        {
            return Object.FindObjectsByType<WaveClearedScreenUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        private TestFlowController Bootstrap(System.Action<LevelConfigSO> configure)
        {
            GameManager gameManager = CreateComponent<GameManager>("GameManager");
            SetSingletonInstance(gameManager);

            LevelConfigSO config = ScriptableObject.CreateInstance<LevelConfigSO>();
            _objectsToDestroy.Add(config);

            // SALIN-223: ContextChallenge and MemoryReward are planned on every level, so a
            // config with no content authored refuses to complete. Authored here so these
            // tests measure the Wave Cleared gate rather than the content gate.
            CutsceneSO memory = ScriptableObject.CreateInstance<CutsceneSO>();
            _objectsToDestroy.Add(memory);
            config.contextMedia.cutscene = memory;
            config.rewardIds.Add("reward.fixture.content");

            configure(config);

            VictoryScreenUI victory = CreateComponent<VictoryScreenUI>("VictoryScreen");
            SetPrivateField(victory, "_panel", CreatePanel("VictoryPanel"));
            DefeatScreenUI defeat = CreateComponent<DefeatScreenUI>("DefeatScreen");
            SetPrivateField(defeat, "_panel", CreatePanel("DefeatPanel"));

            TestFlowController controller = CreateComponent<TestFlowController>("LevelFlowController");
            SetPrivateField(controller, "_victoryScreen", victory);
            SetPrivateField(controller, "_defeatScreen", defeat);

            InvokePrivate(controller, "BootstrapRuntimeFlow",
                new object[] { config, null, null, null });
            return controller;
        }

        private void ConfigureSingleSegment(LevelConfigSO config)
        {
            config.challengeSequence = CreateSequence(new[] { "place-1" });
        }

        /// <summary>
        /// The Level5_Config.asset:249-255 shape — three waves grouped as two segments —
        /// rebuilt synthetically. Level 5 itself still has an empty waves list until
        /// SALIN-247 authors them, so the engine is exercised rather than the asset.
        /// </summary>
        private void ConfigureTwoSegments(LevelConfigSO config)
        {
            config.challengeSequence = CreateSequence(new[] { "place-1", "place-2" });

            for (int i = 0; i < 3; i++)
                config.waves.Add(new WaveDefinition());

            config.flowSegments.Add(new LevelFlowSegment
            {
                waveCount = 2,
                challengeUnitIds = new[] { "place-1" },
            });
            config.flowSegments.Add(new LevelFlowSegment
            {
                waveCount = 1,
                challengeUnitIds = new[] { "place-2" },
            });
        }

        /// <summary>
        /// The challenge validator's uniqueness sets are sequence-global, so each unit needs
        /// its own tokens, slots and occurrence ids.
        /// </summary>
        private ChallengeSequenceSO CreateSequence(string[] unitIds)
        {
            ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            _objectsToDestroy.Add(sequence);
            sequence.sequenceId = "salin232-fixture";

            var units = new ChallengeUnitDefinition[unitIds.Length];
            for (int i = 0; i < unitIds.Length; i++)
            {
                string first = "w-" + (i * 2 + 1);
                string second = "w-" + (i * 2 + 2);
                units[i] = new ChallengeUnitDefinition
                {
                    unitId = unitIds[i],
                    mode = ChallengeMode.WordPlacement,
                    tokens = new[]
                    {
                        new ChallengeTokenDefinition
                        {
                            tokenId = "t" + first, displayText = "t" + first, occurrenceId = first,
                        },
                        new ChallengeTokenDefinition
                        {
                            tokenId = "t" + second, displayText = "t" + second, occurrenceId = second,
                        },
                    },
                    slots = new[]
                    {
                        new ChallengeSlotDefinition { slotId = "s" + i, expectedOccurrenceId = first },
                    },
                    candidateOccurrenceIds = new[] { first, second },
                    maxErrors = 3,
                    heartPenalty = 1,
                };
            }

            sequence.units = units;
            return sequence;
        }

        private static LevelFlowMachine MachineOf(LevelFlowController controller)
        {
            return GetPrivateField<LevelFlowMachine>(controller, "_machine")
                ?? throw new AssertionException("The flow has no running machine.");
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
                yield return null;
        }

        private GameObject CreatePanel(string name)
        {
            GameObject panel = new GameObject(name);
            panel.SetActive(false);
            _objectsToDestroy.Add(panel);
            return panel;
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            GameObject gameObject = new GameObject(name);
            T component = gameObject.AddComponent<T>();
            _objectsToDestroy.Add(gameObject);
            return component;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = FindField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = FindField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            return (T)field.GetValue(target);
        }

        private static FieldInfo FindField(System.Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    return field;
                type = type.BaseType;
            }

            return null;
        }

        private static void InvokePrivate(object target, string methodName, object[] args = null)
        {
            System.Type type = target.GetType();
            MethodInfo method = null;
            while (type != null && method == null)
            {
                method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} method not found.");
            method.Invoke(target, args ?? new object[0]);
        }

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

        /// <summary>Deterministic commit seam, mirroring LevelFlowControllerPhaseTests.</summary>
        private sealed class TestFlowController : LevelFlowController
        {
            public int CommitCalls { get; private set; }

            protected override CampaignOutcomeCommitResult CommitCompletion()
            {
                CommitCalls++;
                return CampaignOutcomeCommitResult.Committed(null);
            }
        }
    }
}
