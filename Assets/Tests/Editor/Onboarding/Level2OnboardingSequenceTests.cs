using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Salinlahi.Tests.Editor.Onboarding
{
    /// <summary>
    /// SALIN-241. Level 2 shipped with beatOrder [Release] and a single line advertising the chain
    /// attacks SALIN-225 deleted, so the level onboarded nothing while naming a removed mechanic.
    /// These tests pin the replacement and would have caught the original defect.
    /// </summary>
    [TestFixture]
    public class Level2OnboardingSequenceTests
    {
        private const string Level2SequencePath =
            "Assets/ScriptableObjects/Tutorial/Level2AdvancedOnboardingSequence.asset";
        private const string Level1ConfigPath =
            "Assets/ScriptableObjects/Levels/Level1_Config.asset";
        private const string Level2ConfigPath =
            "Assets/ScriptableObjects/Levels/Level2_Config.asset";

        private const string ApprovedMassClearCopy =
            "Three or more enemies can share one mark. Draw it once to clear them all.";

        private static readonly string[] RemovedMechanicPhrases =
        {
            "chain attack",
            "combo",
            "focus mode",
        };

#if UNITY_EDITOR
        private static OnboardingSequenceSO LoadLevel2Sequence()
        {
            OnboardingSequenceSO sequence =
                AssetDatabase.LoadAssetAtPath<OnboardingSequenceSO>(Level2SequencePath);
            Assert.That(sequence, Is.Not.Null,
                $"Level 2's onboarding asset must stay at {Level2SequencePath}; Level2_Config "
                + "references it by GUID and nothing else does.");
            return sequence;
        }

        private static LevelConfigSO LoadLevelConfig(string path)
        {
            LevelConfigSO config = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(path);
            Assert.That(config, Is.Not.Null, $"Expected a LevelConfigSO at {path}.");
            return config;
        }
#endif

        /// <summary>
        /// The asset, not the controller, decides what Level 2 teaches. NormalizeSequenceForLevel
        /// used to overwrite this order at runtime, which is why authoring alone was not enough.
        /// </summary>
        [Test]
        public void Level2Sequence_AuthorsTheMassClearTeachBeatBeforeRelease()
        {
#if UNITY_EDITOR
            OnboardingSequenceSO sequence = LoadLevel2Sequence();

            Assert.That(sequence.beatOrder, Is.EqualTo(new[]
            {
                OnboardingBeatType.MassClearTeach,
                OnboardingBeatType.Release,
            }), "Level 2 must explain the mass-clear and then release the player into combat.");
#else
            Assert.Ignore("Reads the asset through AssetDatabase; EditMode only.");
#endif
        }

        /// <summary>
        /// The regression test for the original defect: Level 2's only player-facing line named
        /// chain attacks, a mechanic SALIN-225 had already deleted. This fails on dev as it stood.
        /// </summary>
        [Test]
        public void Level2Sequence_HasNoCopyReferencingRemovedMechanics()
        {
#if UNITY_EDITOR
            OnboardingSequenceSO sequence = LoadLevel2Sequence();

            AssertCopyIsClean(sequence.protagonistIntro.fallbackText, nameof(sequence.protagonistIntro));
            AssertCopyIsClean(sequence.baseIntro.fallbackText, nameof(sequence.baseIntro));
            AssertCopyIsClean(sequence.soloTeachPreVideo.fallbackText, nameof(sequence.soloTeachPreVideo));
            AssertCopyIsClean(sequence.soloTeachPostSuccess.fallbackText, nameof(sequence.soloTeachPostSuccess));
            AssertCopyIsClean(sequence.heartLossDialogue.fallbackText, nameof(sequence.heartLossDialogue));
            AssertCopyIsClean(sequence.release.fallbackText, nameof(sequence.release));
            AssertCopyIsClean(sequence.massClearTeach.fallbackText, nameof(sequence.massClearTeach));
#else
            Assert.Ignore("Reads the asset through AssetDatabase; EditMode only.");
#endif
        }

        private static void AssertCopyIsClean(string copy, string fieldName)
        {
            if (string.IsNullOrEmpty(copy)) return;

            foreach (string phrase in RemovedMechanicPhrases)
            {
                Assert.That(copy.ToLowerInvariant(), Does.Not.Contain(phrase),
                    $"Level 2's {fieldName} copy names \"{phrase}\", a mechanic SALIN-225 removed. "
                    + "Level 2 sits in the Ugat demo slice, so a player-facing string for a "
                    + "deleted system is demo-blocking.");
            }
        }

        /// <summary>
        /// Pins the approved copy exactly. The asset stores it as a folded YAML scalar, so this
        /// also proves the folding round-trips to the intended sentence rather than a mangled one.
        /// </summary>
        [Test]
        public void Level2Sequence_CarriesTheApprovedMassClearCopy()
        {
#if UNITY_EDITOR
            OnboardingSequenceSO sequence = LoadLevel2Sequence();

            Assert.That(sequence.massClearTeach.fallbackText, Is.EqualTo(ApprovedMassClearCopy),
                "The mass-clear line is owner-approved copy (2026-09-13). The threshold it states "
                + "is CombatResolver's _aoeThreshold = 3; changing one without the other makes the "
                + "tutorial lie.");
#else
            Assert.Ignore("Reads the asset through AssetDatabase; EditMode only.");
#endif
        }

        /// <summary>
        /// A beat resolves by BeatType alone, so a wrong or duplicated BeatType would silently bind
        /// the schedule to some other beat.
        /// </summary>
        [Test]
        public void MassClearTeachBeat_DeclaresItsOwnBeatType()
        {
            GameObject host = new("MassClearTeachBeatHost");
            try
            {
                // EditMode never runs Awake on AddComponent, so this is just the type declaration.
                MassClearTeachBeat beat = host.AddComponent<MassClearTeachBeat>();

                Assert.That(beat.BeatType, Is.EqualTo(OnboardingBeatType.MassClearTeach),
                    "Level1OnboardingController.FindBeat matches on BeatType only, so the beat must "
                    + "claim the type its schedule entry names.");
                Assert.That((int)OnboardingBeatType.MassClearTeach, Is.EqualTo(7),
                    "MassClearTeach must stay 7. Reusing the 3 or 6 that SALIN-225 freed would let "
                    + "a stale serialized beatOrder blob bind silently to this beat.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// Ties the beat to the flag that justifies it. Level 1 is the only level with the AOE
        /// mass-clear off, so Level 2 is the first moment a player can ever trigger it — which is
        /// why Level 2's onboarding is the right place to explain it.
        /// </summary>
        [Test]
        public void Level2Sequence_TeachesAMechanicThatIsEnabledAtLevel2()
        {
#if UNITY_EDITOR
            LevelConfigSO level1 = LoadLevelConfig(Level1ConfigPath);
            LevelConfigSO level2 = LoadLevelConfig(Level2ConfigPath);

            Assert.That(level1.multiKillChainEnabled, Is.False,
                "Level 1 keeps the AOE mass-clear off, so it cannot be what Level 1 teaches.");
            Assert.That(level2.multiKillChainEnabled, Is.True,
                "Level 2 switches the AOE mass-clear on. If this ever flips, the mass-clear teach "
                + "beat is explaining a mechanic the level cannot run.");

            OnboardingSequenceSO sequence = LoadLevel2Sequence();
            Assert.That(sequence.beatOrder, Contains.Item(OnboardingBeatType.MassClearTeach),
                "The level that switches the mechanic on is the level that must explain it.");
#else
            Assert.Ignore("Reads assets through AssetDatabase; EditMode only.");
#endif
        }
    }
}
