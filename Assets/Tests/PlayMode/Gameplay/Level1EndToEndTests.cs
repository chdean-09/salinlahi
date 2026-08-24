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
        private const string MissingOnboardingControllerError =
            "[Salinlahi] LevelFlowController: Level 1 tutorial is due, but Level1OnboardingController "
            + "is not in the scene. Run Salinlahi → Tutorial → 5. Wire Level Scene.";

        private const string MissingWaveManagerError =
            "[Salinlahi] LevelFlowController: WaveManager reference missing.";

        private readonly List<Object> _objectsToDestroy = new();

        [SetUp]
        public void SetUp()
        {
            // An earlier PlayMode fixture can leave a live manager behind —
            // ElInquisidorTest loads the production Bootstrap and Gameplay scenes and
            // never tears them down, so their DontDestroyOnLoad singletons outlive it,
            // and only GameManager's static field gets cleared downstream. With
            // ProgressManager.Instance still occupied, Singleton<T>.Awake treats the
            // manager this fixture creates as a duplicate: it schedules
            // Destroy(gameObject) on it and returns before DontDestroyOnLoad. One frame
            // later that object's OnDestroy nulls Instance, ProgressManagerEvidenceSink
            // drops every challenge attempt, and ComputeCompletionResults falls back to
            // an empty batch — which LevelResultsCalculator reads as a flawless run
            // (both accuracies default to 1) and awards three stars. The fixture has to
            // own its singletons outright for any of its metrics to mean anything.
            ReleaseSingleton<GameManager>();
            ReleaseSingleton<ProgressManager>();

            LevelTutorialProgress.ResetLevel1TutorialForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<GameManager>();
            ClearSingletonInstance<ProgressManager>();
            LevelTutorialProgress.ResetLevel1TutorialForTests();
            Time.timeScale = 1f;

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
            // The bare scene has neither an onboarding controller nor a WaveManager;
            // the Defense executor reports both and carries on. Every other error is
            // a real failure of the traversal.
            LogAssert.Expect(LogType.Error, MissingOnboardingControllerError);
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);

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

            // 3. Symbol learning: one card per Level 1 learning requirement
            //    (SALIN-157) — the shipped config authors four. Level 1 ships no
            //    approved pronunciation clips, so every card is the visual-only
            //    path with the replay control hidden. The practice stub then
            //    auto-advances into Defense.
            yield return WaitFrames(10);
            Assert.AreEqual(LevelPhase.SymbolLearning, MachineOf(controller).Phase,
                "The four authored learning requirements must hold the flow on their cards.");
            SymbolLearningCardController learningCards =
                Object.FindFirstObjectByType<SymbolLearningCardController>();
            Assert.IsNotNull(learningCards, "The flow must provide the learning card surface.");
            Assert.AreEqual(4, learningCards.CardCount);
            Assert.IsFalse(gameManager.AcceptsDrawingInput,
                "Every card must be readable before drawing begins.");
            for (int card = 0; card < 4; card++)
            {
                Assert.AreEqual(card, learningCards.CurrentCardIndex);
                Assert.IsFalse(learningCards.IsReplayAvailable,
                    "Level 1 has no approved clips yet; its cards must stay visual-only.");
                learningCards.Continue();
                yield return WaitFrames(3);
            }

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

            // 5. Context challenge: one misplacement on INA — a tier-1 supportive
            //    retry that costs no heart — then restore INA and AMA.
            Assert.AreEqual(LevelPhase.ContextChallenge, MachineOf(controller).Phase);
            ChallengeFlowController challenge =
                GetPrivateField<ChallengeFlowController>(controller, "_challengeFlowController");
            Assert.IsNotNull(challenge);

            // The challenge reaches the recorder through ProgressManagerEvidenceSink,
            // which silently drops every attempt once the singleton is gone. Pin the
            // seam here so a lost instance is reported as itself rather than as an
            // unexplained star count forty lines below.
            Assert.AreSame(progressManager, ProgressManager.Instance,
                "The fixture's ProgressManager must still own the singleton: the "
                + "challenge's evidence sink writes through ProgressManager.Instance.");

            challenge.SubmitPlacement("e2e-ama-decoy");
            yield return WaitFrames(5);
            challenge.SubmitPlacement("e2e-ina");
            yield return WaitFrames(5);
            challenge.SubmitPlacement("e2e-ama");
            yield return WaitFrames(15);

            // 6-8. Memory (graceful skip without a cutscene player), atomic save,
            //      Results — reachable only through the accepted save.
            Assert.AreEqual(LevelPhase.Completed, MachineOf(controller).Phase);
            Assert.AreEqual(ChallengePlayResult.Completed, challenge.LastPlayResult,
                "Phase 6 must play the sequence through, not fall through on a rejected asset.");
            Assert.AreEqual(1, controller.CommitCalls);
            Assert.IsTrue(victoryPanel.activeSelf, "Results must be shown after the accepted save.");

            // Metrics come from the same evidence the save carries, so the evidence is
            // checked first: a challenge that silently records nothing carries no
            // entries at all, and every accuracy below it would then be the calculator's
            // no-attempts default of 1 rather than anything this traversal earned.
            Assert.AreSame(progressManager, ProgressManager.Instance,
                "ComputeCompletionResults reads ProgressManager.Instance's recorder; a "
                + "lost singleton hands Results an empty batch that scores as flawless.");
            LearningEvidenceBatch evidence = progressManager.LevelEvidence.Build();
            LearningEvidenceEntry inaPlacement =
                PlacementEvidence(evidence, "level.e2e.ugat01.focus.01");
            Assert.AreEqual(2, inaPlacement.attemptCount, "The misplacement and the correction.");
            Assert.AreEqual(1, inaPlacement.successCount);
            LearningEvidenceEntry amaPlacement =
                PlacementEvidence(evidence, "level.e2e.ugat01.focus.02");
            Assert.AreEqual(1, amaPlacement.attemptCount);
            Assert.AreEqual(1, amaPlacement.successCount);

            Assert.IsNotNull(controller.LastResults);
            Assert.AreEqual(1f,
                controller.LastResults.Metrics[LevelResultsCalculator.TracingAccuracyMetricId], 0.0001f);
            Assert.AreEqual(2f / 3f,
                controller.LastResults.Metrics[LevelResultsCalculator.ContextAccuracyMetricId], 0.0001f,
                "Two of the three placements were correct.");
            Assert.AreEqual(1f,
                controller.LastResults.Metrics[LevelResultsCalculator.HeartsRatioMetricId], 0.0001f,
                "The tier-1 misplacement is a supportive retry: it spends no heart.");
            Assert.AreEqual(90f,
                controller.LastResults.Metrics[LevelResultsCalculator.ScoreMetricId], 0.01f,
                "0.5*1 + 0.3*(2/3) + 0.2*1, with no emergency-hint penalty.");
            Assert.AreEqual(2, controller.LastResults.Stars,
                "Context accuracy 2/3 clears the two-star band (>= 0.6) and misses the "
                + "three-star band (>= 0.8); a challenge that recorded nothing would read 1.");

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

        /// <summary>
        /// SALIN-201 / BL-E2-S7: the failure-and-retry half of the Level 1 slice.
        /// The happy path above proves a won attempt commits once; the phase
        /// fixture proves a defeat is refused at each individual phase. Neither
        /// drives a *lost* Level 1 attempt and a *replacement* attempt end to
        /// end, which is the pairing this asserts: losing writes nothing at all,
        /// and the retry that follows commits exactly once — not twice, and not
        /// on top of a half-written first attempt.
        /// </summary>
        [UnityTest]
        public IEnumerator LoseLevelOne_CommitsNothing_ThenTheRetryCompletesAndCommitsOnce()
        {
            // Two Defense entries, so the bare scene reports its missing wiring twice.
            LogAssert.Expect(LogType.Error, MissingOnboardingControllerError);
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            LogAssert.Expect(LogType.Error, MissingOnboardingControllerError);
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);

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
            GameObject defeatPanel = CreatePanel("DefeatPanel");
            SetPrivateField(defeat, "_panel", defeatPanel);

            LevelConfigSO config = BuildInaAmaConfig(
                out string[] symbolIds, out ChallengeSequenceSO _);

            // ---- Attempt 1: reach Defense, then lose. ----
            TestE2EFlowController lost = CreateComponent<TestE2EFlowController>("FlowController_Lost");
            SetPrivateField(lost, "_victoryScreen", victory);
            SetPrivateField(lost, "_defeatScreen", defeat);
            SetPrivateField(lost, "_dialogueController", dialogue);
            InvokePrivate(lost, "BootstrapRuntimeFlow", new object[] { config, null, null, null });
            yield return TraverseToDefense(lost, gameManager);

            EventBus.RaiseGameOver();
            yield return WaitFrames(10);

            Assert.IsTrue(defeatPanel.activeSelf, "A lost attempt must show Defeat.");
            Assert.IsFalse(victoryPanel.activeSelf, "A lost attempt must not show Results.");
            Assert.AreEqual(0, lost.CommitCalls,
                "AC: a defeat must not commit — no partial outcome may reach the save.");
            Assert.AreEqual(0, progressManager.GetStars(1),
                "A lost attempt must earn no stars.");
            Assert.IsFalse(progressManager.IsLevelUnlocked(2),
                "A lost attempt must not unlock the next level.");

            // Retry reloads the level in production, so the attempt-scoped flow is
            // replaced rather than reused. Destroying it also takes the runtime
            // surfaces it parented, so attempt 2 finds its own.
            Object.Destroy(lost.gameObject);
            yield return WaitFrames(5);
            defeatPanel.SetActive(false);

            // ---- Attempt 2: the retry completes. ----
            TestE2EFlowController won = CreateComponent<TestE2EFlowController>("FlowController_Won");
            SetPrivateField(won, "_victoryScreen", victory);
            SetPrivateField(won, "_defeatScreen", defeat);
            SetPrivateField(won, "_dialogueController", dialogue);
            InvokePrivate(won, "BootstrapRuntimeFlow", new object[] { config, null, null, null });
            yield return TraverseToDefense(won, gameManager);

            foreach (string symbolId in symbolIds)
            {
                progressManager.LevelEvidence.RecordAttempt(
                    symbolId, LearningContentKind.Symbol, MasteryDimension.Form,
                    success: true, answerWasVisible: false);
            }

            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);

            Assert.AreEqual(LevelPhase.ContextChallenge, MachineOf(won).Phase);
            ChallengeFlowController challenge =
                GetPrivateField<ChallengeFlowController>(won, "_challengeFlowController");
            Assert.IsNotNull(challenge);
            challenge.SubmitPlacement("e2e-ina");
            yield return WaitFrames(5);
            challenge.SubmitPlacement("e2e-ama");
            yield return WaitFrames(15);

            Assert.AreEqual(LevelPhase.Completed, MachineOf(won).Phase);
            Assert.AreEqual(1, won.CommitCalls,
                "AC: the retry must commit exactly once.");
            Assert.AreEqual(0, lost.CommitCalls,
                "The discarded attempt must still have committed nothing after the retry.");
            Assert.IsTrue(victoryPanel.activeSelf, "The retry must reach Results.");
            Assert.IsTrue(progressManager.IsLevelUnlocked(2),
                "Only the completed attempt may unlock Level 2.");
        }

        /// <summary>
        /// Story → focus-word preview → the four authored learning cards → Defense.
        /// Shared by the traversals above so a retry drives the same path a first
        /// attempt does.
        /// </summary>
        private IEnumerator TraverseToDefense(
            TestE2EFlowController controller, GameManager gameManager)
        {
            yield return WaitFrames(5);
            Assert.AreEqual(LevelPhase.Story, MachineOf(controller).Phase);
            EventBus.RaiseDialogueComplete();
            yield return WaitFrames(10);

            Assert.AreEqual(LevelPhase.FocusWords, MachineOf(controller).Phase);
            FocusWordPreviewController preview =
                Object.FindFirstObjectByType<FocusWordPreviewController>();
            Assert.IsNotNull(preview, "The flow must provide the focus-word preview.");
            preview.Continue();

            yield return WaitFrames(10);
            Assert.AreEqual(LevelPhase.SymbolLearning, MachineOf(controller).Phase);
            SymbolLearningCardController learningCards =
                Object.FindFirstObjectByType<SymbolLearningCardController>();
            Assert.IsNotNull(learningCards, "The flow must provide the learning cards.");
            for (int card = 0; card < learningCards.CardCount; card++)
            {
                learningCards.Continue();
                yield return WaitFrames(3);
            }

            yield return WaitFrames(15);
            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase);
            Assert.IsTrue(gameManager.AcceptsDrawingInput,
                "Defense opens drawing input for the attempt.");
        }

        // ---------------------------------------------------------------------
        // Config
        // ---------------------------------------------------------------------

        /// <summary>
        /// The shipped Level1_Config shape with synthetic ids: INA/AMA focus words over
        /// the four Level 1 symbols, tier-1 challenge policy, glyph plus Latin-text clue
        /// channels, and the prototype off so the challenge plays as phase 6.
        /// </summary>
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
                    requiredSuccesses = 2,
                    symbolValue = Reference(symbol),
                });
                config.masteryRequirements.Add(new ContentRequirement
                {
                    kind = ContentRequirementKind.Mastery,
                    requiredSuccesses = 1,
                    symbolValue = Reference(symbol),
                });
            }

            config.allowedCharacters = new List<BaybayinCharacterSO> { ei, na, a, ma };
            config.finalRestorationValue = Reference(na);
            config.rewardIds.Add("memory.e2e.ugat01");
            config.activeClueCombatEnabled = true;
            config.clueChannels = ClueChannels.Glyph | ClueChannels.LatinText;
            config.audioVisualFallback = ClueChannels.LatinText;

            // Off: the sequence is phase 6 of the plan, not a pre-wave replacement
            // inside the Defense executor.
            config.challengePrototypeEnabled = false;
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

        /// <summary>The Assembly entry a word-placement unit records for one focus word.</summary>
        private static LearningEvidenceEntry PlacementEvidence(
            LearningEvidenceBatch evidence, string focusWordId)
        {
            LearningEvidenceEntry entry = evidence.entries.SingleOrDefault(
                candidate => candidate.contentId == focusWordId
                    && candidate.dimension == MasteryDimension.Assembly);
            Assert.IsNotNull(entry, $"The challenge recorded no placement evidence for {focusWordId}.");
            return entry;
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

        /// <summary>
        /// Destroys every <typeparamref name="T"/> an earlier fixture left in the scene
        /// and clears the static field, so this fixture's own manager takes the
        /// "I am the instance" branch of Singleton&lt;T&gt;.Awake instead of the
        /// duplicate branch that schedules its destruction.
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
