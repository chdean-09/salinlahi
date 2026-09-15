using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Onboarding
{
    [TestFixture]
    public class Level1OnboardingControllerTests
    {
        [TearDown]
        public void TearDown()
        {
            DestroyRuntimeObject("TutorialCanvas");
            DestroyRuntimeObject("TutorialSpotlightOverlay");
        }

        [Test]
        public void Awake_WhenSceneObjectHasNoBeatComponents_AddsBasicLevelOneBeatComponents()
        {
            GameObject host = new("Level1OnboardingControllerHost");
            try
            {
                Level1OnboardingController controller = host.AddComponent<Level1OnboardingController>();
                // EditMode never runs Awake on AddComponent. Awake would also
                // spawn the runtime guide UI, so drive only the unit under
                // test: the default-beat attachment step.
                InvokePrivate<object>(controller, "EnsureDefaultBeatComponents");

                Assert.NotNull(host.GetComponent<ProtagonistIntroBeat>());
                Assert.NotNull(host.GetComponent<BaseIntroBeat>());
                Assert.NotNull(host.GetComponent<HeartLossDemoBeat>());
                Assert.NotNull(host.GetComponent<ReleaseBeat>());
                // SALIN-241 added MassClearTeachBeat to the default attachment set. The eight-beat
                // lesson then removed SoloTeachBeat: drawing/glyph teaching moved into
                // EnemyIntroductionBeat, triggered by a spawn rather than attached here. Net count
                // moved 6 -> 5. A beat only runs when a sequence's beatOrder schedules it, so
                // attaching Level 2's beat here does not change what Level 1 plays.
                Assert.NotNull(host.GetComponent<MassClearTeachBeat>());
                Assert.AreEqual(5, host.GetComponents<OnboardingBeat>().Length);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// The eight-beat lesson removed SoloTeach: basicTeachSteps/soloTeachStep are gone, so the
        /// legacy Level1TutorialSequenceSO converter no longer builds a teach beat or its beatOrder
        /// entry. What survives is the heart-loss demo enemy/character derivation, which this pins:
        /// it still reads the legacy step list's index 2 (the old demo step), and the resulting
        /// order still runs ProtagonistIntro, BaseIntro, HeartLossDemo, Release with no SoloTeach
        /// beat to schedule.
        /// </summary>
        [Test]
        public void LegacyLevelOneSequence_DerivesHeartLossDemoFromThirdStep()
        {
            GameObject host = new("Level1OnboardingControllerHost");
            LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            Level1TutorialSequenceSO legacySequence = ScriptableObject.CreateInstance<Level1TutorialSequenceSO>();
            Level1TutorialStepSO ba = ScriptableObject.CreateInstance<Level1TutorialStepSO>();
            Level1TutorialStepSO ou = ScriptableObject.CreateInstance<Level1TutorialStepSO>();
            Level1TutorialStepSO ha = ScriptableObject.CreateInstance<Level1TutorialStepSO>();
            BaybayinCharacterSO haCharacter = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            EnemyDataSO haEnemy = ScriptableObject.CreateInstance<EnemyDataSO>();

            try
            {
                ha.targetCharacter = haCharacter;
                ha.enemyData = haEnemy;
                legacySequence.steps = new[] { ba, ou, ha };
                levelConfig.levelNumber = LevelTutorialProgress.Level1TutorialLevelNumber;
                levelConfig.tutorialSequence = legacySequence;

                Level1OnboardingController controller = host.AddComponent<Level1OnboardingController>();
                OnboardingSequenceSO sequence = InvokePrivate<OnboardingSequenceSO>(
                    controller,
                    "ResolveSequence",
                    levelConfig);

                Assert.IsNotNull(sequence);
                Assert.AreSame(haCharacter, sequence.heartLossDemoCharacter);
                Assert.AreSame(haEnemy, sequence.heartLossDemoEnemyData);
                Assert.AreEqual(
                    new[]
                    {
                        OnboardingBeatType.ProtagonistIntro,
                        OnboardingBeatType.BaseIntro,
                        OnboardingBeatType.HeartLossDemo,
                        OnboardingBeatType.Release,
                    },
                    sequence.beatOrder);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(levelConfig);
                Object.DestroyImmediate(legacySequence);
                Object.DestroyImmediate(ba);
                Object.DestroyImmediate(ou);
                Object.DestroyImmediate(ha);
                Object.DestroyImmediate(haCharacter);
                Object.DestroyImmediate(haEnemy);
            }
        }

        /// <summary>
        /// SALIN-241 guard, first half. The anti-fallthrough protection SALIN-225 added must
        /// survive: an empty level-2 order still resolves to Release alone rather than falling
        /// through to the SO's five-beat default and re-teaching Level 1's basics.
        /// </summary>
        [Test]
        public void NormalizeSequenceForLevel_WhenOrderIsEmpty_FallsBackToReleaseOnly()
        {
            OnboardingSequenceSO sequence = ScriptableObject.CreateInstance<OnboardingSequenceSO>();

            try
            {
                sequence.beatOrder = System.Array.Empty<OnboardingBeatType>();

                Level1OnboardingController.NormalizeSequenceForLevel(
                    sequence,
                    LevelTutorialProgress.Level2TutorialLevelNumber);

                Assert.AreEqual(
                    new[] { OnboardingBeatType.Release },
                    sequence.beatOrder,
                    "An unauthored level-2 order must still end at Release rather than inheriting "
                    + "Level 1's five-beat default.");
            }
            finally
            {
                Object.DestroyImmediate(sequence);
            }
        }

        /// <summary>
        /// SALIN-241 guard, second half, and the load-bearing one. The level-2 arm used to
        /// overwrite the authored order UNCONDITIONALLY, so a beat authored into
        /// Level2AdvancedOnboardingSequence.asset was discarded at runtime with no compile error,
        /// no warning and no failing test. This pins the fix: a non-empty authored order survives
        /// normalization verbatim, which is what makes the asset the source of truth.
        /// </summary>
        [Test]
        public void NormalizeSequenceForLevel_WhenOrderIsAuthored_PreservesIt()
        {
            OnboardingSequenceSO sequence = ScriptableObject.CreateInstance<OnboardingSequenceSO>();

            try
            {
                OnboardingBeatType[] authored =
                {
                    OnboardingBeatType.MassClearTeach,
                    OnboardingBeatType.Release,
                };
                sequence.beatOrder = authored;

                Level1OnboardingController.NormalizeSequenceForLevel(
                    sequence,
                    LevelTutorialProgress.Level2TutorialLevelNumber);

                Assert.AreEqual(
                    authored,
                    sequence.beatOrder,
                    "Level 2's authored beat order must reach the run loop untouched. If this "
                    + "fails, the normalizer is silently stripping the asset again and any beat "
                    + "authored for Level 2 will never play.");
            }
            finally
            {
                Object.DestroyImmediate(sequence);
            }
        }

        [Test]
        public void HeartLossDemo_RevealsInactiveHeartHudBeforeDemoDamage()
        {
            GameObject hudCanvas = new("HUDCanvas");
            GameObject heartsPanel = new("HeartsPanel");

            try
            {
                heartsPanel.transform.SetParent(hudCanvas.transform, false);
                heartsPanel.AddComponent<HeartDisplay>();
                heartsPanel.SetActive(false);

                Assert.IsTrue(HeartLossDemoBeat.RevealHeartHudForDemo());
                Assert.IsTrue(heartsPanel.activeInHierarchy,
                    "The Level 1 heart-loss beat must reactivate HeartsPanel before raising the tutorial damage event.");
            }
            finally
            {
                Object.DestroyImmediate(hudCanvas);
            }
        }

        [Test]
        public void BuildContext_ForLevelTwo_PersistsCompletedBeatUnderLevelTwoKey()
        {
            OnboardingPersistence.Clear();
            GameObject host = new("Level1OnboardingControllerHost");
            OnboardingSequenceSO sequence = ScriptableObject.CreateInstance<OnboardingSequenceSO>();

            try
            {
                Level1OnboardingController controller = host.AddComponent<Level1OnboardingController>();

                OnboardingContext ctx = InvokePrivate<OnboardingContext>(
                    controller,
                    "BuildContext",
                    sequence,
                    LevelTutorialProgress.Level2TutorialLevelNumber);

                ctx.SetBeatCompleted(1);

                Assert.AreEqual(
                    1,
                    OnboardingPersistence.GetLastCompletedBeatIndex(LevelTutorialProgress.Level2TutorialLevelNumber),
                    "Level 2 beat completion must persist under the Level 2 key.");
                Assert.AreEqual(
                    OnboardingPersistence.NoBeatCompleted,
                    OnboardingPersistence.GetLastCompletedBeatIndex(LevelTutorialProgress.Level1TutorialLevelNumber),
                    "Level 2 beat completion must not write the Level 1 key.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(sequence);
                OnboardingPersistence.Clear();
            }
        }

        private static void DestroyRuntimeObject(string objectName)
        {
            GameObject[] objects = Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < objects.Length; i++)
            {
                GameObject obj = objects[i];
                if (obj != null && obj.name == objectName)
                    Object.DestroyImmediate(obj);
            }
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} method not found.");
            return (T)method.Invoke(target, args);
        }
    }
}
