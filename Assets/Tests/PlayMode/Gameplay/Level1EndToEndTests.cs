using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    /// <summary>
    /// SALIN-134 / BL-E1-S1: the complete LF-CONTRACT-v2 traversal for an
    /// INA/AMA level — story, focus-word preview, learning stubs, defense
    /// evidence, context challenge, memory, atomic save with populated rewards,
    /// Results, and the next-level unlock. Combat internals (marking, freezing,
    /// crediting) are pinned by ActiveClueDirectorTests; this fixture drives the
    /// same seams the defense layer reports through.
    /// </summary>
    [TestFixture]
    public sealed class Level1EndToEndTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [SetUp]
        public void SetUp()
        {
            LevelTutorialProgress.ResetLevel1TutorialForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<GameManager>();
            ClearSingletonInstance<ProgressManager>();
            LevelTutorialProgress.ResetLevel1TutorialForTests();
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = false;

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();

            GameObject runtimePanel = GameObject.Find("[Runtime] ActiveCluePanel");
            while (runtimePanel != null)
            {
                Object.DestroyImmediate(runtimePanel);
                runtimePanel = GameObject.Find("[Runtime] ActiveCluePanel");
            }

            ChallengeRuntimeState.Clear();
            foreach (Level1TutorialGuideUI guide in Object.FindObjectsByType<Level1TutorialGuideUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (guide != null)
                    Object.DestroyImmediate(guide.gameObject);
            }

            // The flow's AtomicSave raises OnLevelComplete, which the legacy
            // ProgressManager path persists to PlayerPrefs; leave no residue.
            for (int level = 1; level <= 15; level++)
            {
                PlayerPrefs.DeleteKey($"salinlahi.progress.unlocked.{level}");
                PlayerPrefs.DeleteKey($"salinlahi.progress.stars.{level}");
            }

            PlayerPrefs.DeleteKey(ProgressManager.SelectedLevelKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator CompleteLevelOne_FromStoryToResultsAndNextLevelUnlock()
        {
            // WaveManager config-resolution errors in a bare scene are not the
            // subject under test; the traversal is.
            LogAssert.ignoreFailingMessages = true;

            GameManager gameManager = CreateComponent<GameManager>("GameManager");
            SetSingletonInstance(gameManager);
            ProgressManager progressManager = CreateComponent<ProgressManager>("ProgressManager");
            SetSingletonInstance(progressManager);

            DialogueController dialogue = CreateComponent<DialogueController>("DialogueController");
            SetPrivateField(dialogue, "_overlayPanel", CreatePanel("DialogueOverlay"));

            VictoryScreenUI victory = CreateComponent<VictoryScreenUI>("VictoryScreen");
            GameObject victoryPanel = CreatePanel("VictoryPanel");
            SetPrivateField(victory, "_panel", victoryPanel);
            DefeatScreenUI defeat = CreateComponent<DefeatScreenUI>("DefeatScreen");
            SetPrivateField(defeat, "_panel", CreatePanel("DefeatPanel"));

            LevelConfigSO config = BuildInaAmaConfig(
                out string[] symbolIds, out ChallengeSequenceSO _);

            TestE2EFlowController controller = CreateComponent<TestE2EFlowController>("LevelFlowController");
            SetPrivateField(controller, "_victoryScreen", victory);
            SetPrivateField(controller, "_defeatScreen", defeat);
            SetPrivateField(controller, "_dialogueController", dialogue);
            InvokePrivate(controller, "BootstrapRuntimeFlow", new object[] { config, null, null, null });

            // 1. Story: the family intro plays before anything else.
            yield return WaitFrames(5);
            Assert.AreEqual(LevelPhase.Story, MachineOf(controller).Phase);
            EventBus.RaiseDialogueComplete();
            yield return WaitFrames(10);

            // 2. Focus words: INA and AMA presented before combat, input locked.
            Assert.AreEqual(LevelPhase.FocusWords, MachineOf(controller).Phase);
            FocusWordPreviewController preview =
                Object.FindFirstObjectByType<FocusWordPreviewController>();
            Assert.IsNotNull(preview);
            StringAssert.Contains("INA", preview.RenderedText);
            StringAssert.Contains("AMA", preview.RenderedText);
            Assert.IsFalse(gameManager.AcceptsDrawingInput,
                "Both words must be readable before drawing begins.");
            preview.Continue();

            // 3. Learning/practice stubs auto-advance into Defense.
            yield return WaitFrames(15);
            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase);
            Assert.IsTrue(gameManager.AcceptsDrawingInput,
                "Defense opens drawing input exactly once.");

            // 4. Defense: correct traces on the active clue record Form evidence
            //    (the CombatResolver seam pinned by ActiveClueDirectorTests).
            foreach (string symbolId in symbolIds)
            {
                progressManager.LevelEvidence.RecordAttempt(
                    symbolId, LearningContentKind.Symbol, MasteryDimension.Form,
                    success: true, answerWasVisible: false);
            }

            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);

            // 5. Context challenge: restore INA then AMA.
            Assert.AreEqual(LevelPhase.ContextChallenge, MachineOf(controller).Phase);
            ChallengeFlowController challenge =
                GetPrivateField<ChallengeFlowController>(controller, "_challengeFlowController");
            Assert.IsNotNull(challenge);
            challenge.SubmitPlacement("e2e-ina");
            yield return WaitFrames(5);
            challenge.SubmitPlacement("e2e-ama");
            yield return WaitFrames(15);

            // 6-8. Memory (graceful skip without a cutscene player), atomic save,
            //      Results — reachable only through the accepted save.
            Assert.AreEqual(LevelPhase.Completed, MachineOf(controller).Phase);
            Assert.AreEqual(1, controller.CommitCalls);
            Assert.IsTrue(victoryPanel.activeSelf, "Results must be shown after the accepted save.");

            // Metrics come from the same evidence the save carries.
            Assert.IsNotNull(controller.LastResults);
            Assert.AreEqual(3, controller.LastResults.Stars,
                "A flawless run earns three stars under the documented formula.");
            Assert.AreEqual(1f,
                controller.LastResults.Metrics[LevelResultsCalculator.TracingAccuracyMetricId], 0.0001f);
            Assert.AreEqual(1f,
                controller.LastResults.Metrics[LevelResultsCalculator.ContextAccuracyMetricId], 0.0001f);

            // Rewards: all four symbols unlock, the family memory becomes reviewable.
            Assert.IsNotNull(controller.LastRewardGrant);
            CollectionAssert.AreEquivalent(symbolIds, controller.LastRewardGrant.UnlockedSymbolIds.ToList());
            CollectionAssert.AreEqual(new[] { "memory.e2e.ugat01" },
                controller.LastRewardGrant.UnlockedMemoryIds.ToList());

            // The results summary names the restored words.
            GameObject summary = GameObject.Find("[Runtime] ResultsSummary");
            Assert.IsNotNull(summary);
            StringAssert.Contains("INA", summary.GetComponent<TMPro.TextMeshProUGUI>().text);

            // Level 2 becomes available (legacy unlock path persists on level complete).
            Assert.IsTrue(progressManager.IsLevelUnlocked(2),
                "Completing Level 1 must make Level 2 available.");
        }

        // ---------------------------------------------------------------------
        // Config
        // ---------------------------------------------------------------------

        private LevelConfigSO BuildInaAmaConfig(
            out string[] symbolIds, out ChallengeSequenceSO sequence)
        {
            var config = ScriptableObject.CreateInstance<LevelConfigSO>();
            _objectsToDestroy.Add(config);
            config.stableId = "level.e2e.ugat01";
            config.levelNumber = 1;

            var dialogueAsset = ScriptableObject.CreateInstance<DialogueSO>();
            dialogueAsset.lines = new[]
            {
                new DialogueLine { speakerName = "Tagapagsalaysay", text = "Alaala ng pamilya." },
            };
            _objectsToDestroy.Add(dialogueAsset);
            config.introDialogue = dialogueAsset;

            BaybayinCharacterSO ei = Symbol("EI", "i", "symbol.e2e-ei", config.stableId);
            BaybayinCharacterSO na = Symbol("NA", "na", "symbol.e2e-na", config.stableId);
            BaybayinCharacterSO a = Symbol("A", "a", "symbol.e2e-a", config.stableId);
            BaybayinCharacterSO ma = Symbol("MA", "ma", "symbol.e2e-ma", config.stableId);
            symbolIds = new[] { ei.stableId, na.stableId, a.stableId, ma.stableId };

            config.focusWords.Add(Word("level.e2e.ugat01.focus.01", "INA", "mother", ei, na));
            config.focusWords.Add(Word("level.e2e.ugat01.focus.02", "AMA", "father", a, ma));
            foreach (BaybayinCharacterSO symbol in new[] { ei, na, a, ma })
            {
                config.cumulativeSymbolPool.Add(Reference(symbol));
                config.learningRequirements.Add(new ContentRequirement
                {
                    kind = ContentRequirementKind.Instruction,
                    requiredSuccesses = 1,
                    symbolValue = Reference(symbol),
                });
                config.practiceRequirements.Add(new ContentRequirement
                {
                    kind = ContentRequirementKind.Practice,
                    requiredSuccesses = 1,
                    symbolValue = Reference(symbol),
                });
            }

            config.rewardIds.Add("memory.e2e.ugat01");
            config.activeClueCombatEnabled = true;
            config.challengePolicy = ChallengeTierPolicy.ForTier(1);

            sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            _objectsToDestroy.Add(sequence);
            sequence.sequenceId = "e2e-context";
            sequence.units = new[]
            {
                PlacementUnit("e2e-place-ina", "level.e2e.ugat01.focus.01", "e2e-ina", "INA", "e2e-ama-decoy", "AMA"),
                PlacementUnit("e2e-place-ama", "level.e2e.ugat01.focus.02", "e2e-ama", "AMA", "e2e-ina-decoy", "INA"),
            };
            config.challengeSequence = sequence;
            return config;
        }

        private static ChallengeUnitDefinition PlacementUnit(
            string unitId, string evidenceId, string correctId, string correctText,
            string decoyId, string decoyText)
        {
            return new ChallengeUnitDefinition
            {
                unitId = unitId,
                mode = ChallengeMode.WordPlacement,
                evidenceContentId = evidenceId,
                tokens = new[]
                {
                    new ChallengeTokenDefinition
                    {
                        tokenId = correctId, displayText = correctText, occurrenceId = correctId,
                        role = ChallengeTokenRole.Focus,
                    },
                    new ChallengeTokenDefinition
                    {
                        tokenId = decoyId, displayText = decoyText, occurrenceId = decoyId,
                    },
                },
                slots = new[]
                {
                    new ChallengeSlotDefinition { slotId = unitId + "-slot", expectedOccurrenceId = correctId },
                },
                candidateOccurrenceIds = new[] { correctId, decoyId },
                maxErrors = 3,
                heartPenalty = 1,
            };
        }

        private BaybayinCharacterSO Symbol(
            string characterId, string syllable, string stableId, string introLevelId)
        {
            var symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.characterID = characterId;
            symbol.syllable = syllable;
            symbol.stableId = stableId;
            symbol.firstIntroductionLevelId = introLevelId;
            _objectsToDestroy.Add(symbol);
            return symbol;
        }

        private static FocusWordDefinition Word(
            string stableId, string label, string meaning,
            BaybayinCharacterSO first, BaybayinCharacterSO second)
        {
            return new FocusWordDefinition
            {
                stableId = stableId,
                latinSpelling = label,
                displayLabel = label,
                meaning = meaning,
                decomposition = new List<SymbolValueReference> { Reference(first), Reference(second) },
            };
        }

        private static SymbolValueReference Reference(BaybayinCharacterSO symbol)
        {
            return new SymbolValueReference
            {
                symbol = symbol,
                spokenValueId = "value." + symbol.stableId.Substring("symbol.".Length),
            };
        }

        // ---------------------------------------------------------------------
        // Harness helpers
        // ---------------------------------------------------------------------

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

        private static LevelFlowMachine MachineOf(LevelFlowController controller)
        {
            return GetPrivateField<LevelFlowMachine>(controller, "_machine")
                ?? throw new AssertionException("The flow has no running machine.");
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
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    return field;
                type = type.BaseType;
            }

            return null;
        }

        private static void InvokePrivate(object target, string methodName, object[] args)
        {
            System.Type type = target.GetType();
            MethodInfo method = null;
            while (type != null && method == null)
            {
                method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} method not found.");
            method.Invoke(target, args);
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

        private sealed class TestE2EFlowController : LevelFlowController
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
