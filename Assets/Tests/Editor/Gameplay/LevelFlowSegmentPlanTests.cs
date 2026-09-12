using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-226. The two primitives the alternating flow is built on, tested away from the
    /// machine: resolving a segment's wave range out of the flat wave list, and cutting a
    /// challenge sequence down to one segment's units.
    ///
    /// The subset test exists because the plan ASSUMED a strict subset of a valid sequence
    /// stays valid. That assumption is load-bearing — a subset that failed validation would
    /// make every segmented level refuse its own restoration leg — and the validator's
    /// uniqueness sets are sequence-global, so it is confirmed here rather than reasoned about.
    /// </summary>
    [TestFixture]
    public sealed class LevelFlowSegmentPlanTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void WaveRange_ForEachSegment_PartitionsTheFlatWaveListInOrder_SALIN226()
        {
            LevelConfigSO config = CreateSegmentedConfig(
                waveCount: 5,
                (2, new[] { "unit-a" }),
                (1, new[] { "unit-b" }),
                (2, new string[0]));

            LevelPhasePlan plan = LevelPhasePlan.FromConfig(config);
            Assert.AreEqual(3, plan.SegmentCount);
            Assert.IsFalse(plan.SegmentPlanInvalid);

            Assert.IsTrue(plan.TryGetSegmentWaveRange(0, out int start0, out int end0));
            Assert.AreEqual(0, start0);
            Assert.AreEqual(2, end0, "Segment 0 takes waves [0,2).");

            Assert.IsTrue(plan.TryGetSegmentWaveRange(1, out int start1, out int end1));
            Assert.AreEqual(2, start1, "Segment 1 starts where segment 0 stopped.");
            Assert.AreEqual(3, end1);

            Assert.IsTrue(plan.TryGetSegmentWaveRange(2, out int start2, out int end2));
            Assert.AreEqual(3, start2);
            Assert.AreEqual(5, end2, "The segments cover the whole list with no gap.");

            Assert.IsFalse(plan.TryGetSegmentWaveRange(3, out _, out _),
                "An out-of-range segment resolves no wave range.");
        }

        [Test]
        public void WaveRange_OnAnUnsegmentedLevel_ResolvesNothingSoTheWholeListRuns_SALIN226()
        {
            LevelConfigSO config = CreateLegacyConfig();
            LevelPhasePlan plan = LevelPhasePlan.FromConfig(config);

            Assert.AreEqual(1, plan.SegmentCount);
            Assert.IsFalse(plan.TryGetSegmentWaveRange(0, out _, out _),
                "An unsegmented level must fall through to the unbounded StartLevel path, "
                + "not to a synthesised range covering the same waves.");
        }

        [Test]
        public void UnitSubset_KeepsOnlyTheNamedUnitsInOrder_AndStaysValid_SALIN226()
        {
            ChallengeSequenceSO source = CreateSequence("unit-a", "unit-b", "unit-c");

            ChallengeSequenceSO subset =
                ChallengeFlowController.BuildUnitSubset(source, new[] { "unit-c", "unit-a" });
            Assert.IsNotNull(subset);
            _objectsToDestroy.Add(subset);

            Assert.AreEqual(2, subset.units.Length);
            Assert.AreEqual("unit-c", subset.units[0].unitId, "Units play in the order named.");
            Assert.AreEqual("unit-a", subset.units[1].unitId);
            Assert.AreEqual(source.sequenceId, subset.sequenceId);

            // The plan's load-bearing assumption, confirmed rather than assumed.
            Assert.IsTrue(ChallengeSequenceValidator.Validate(source).IsValid,
                "Fixture: the source sequence must itself be valid.");
            ChallengeValidationResult validation = ChallengeSequenceValidator.Validate(subset);
            Assert.IsTrue(validation.IsValid,
                "A strict subset of a valid sequence must stay valid, or every segmented "
                + "level would refuse its own restoration leg. Errors: "
                + string.Join("; ", validation.Errors));
        }

        [Test]
        public void UnitSubset_NamingAnAbsentUnit_ReturnsNullRatherThanAPartialPlay_SALIN226()
        {
            ChallengeSequenceSO source = CreateSequence("unit-a", "unit-b");

            Assert.IsNull(
                ChallengeFlowController.BuildUnitSubset(source, new[] { "unit-a", "unit-missing" }),
                "A partial subset would silently play less restoration than the level "
                + "authored. The caller must refuse instead.");
        }

        [Test]
        public void Plan_TrailingSegmentWithNoUnits_IsAcceptedAsARestorationlessWaveGroup_SALIN226()
        {
            // The AC-5/AC-6 shape: clear waves 1-2, restore line 1, then clear wave 3.
            LevelConfigSO config = CreateSegmentedConfig(
                waveCount: 3,
                (2, new[] { "unit-a" }),
                (1, new string[0]));

            LevelPhasePlan plan = LevelPhasePlan.FromConfig(config);

            Assert.AreEqual(2, plan.SegmentCount);
            Assert.IsFalse(plan.SegmentPlanInvalid,
                "A trailing wave group with no restoration leg is authored intent.");
            Assert.AreEqual(0, plan.SegmentChallengeUnitIds(1).Count);
        }

        [Test]
        public void Plan_NoSegmentPlayingAnyUnit_IsRejected_SALIN226()
        {
            LevelConfigSO config = CreateSegmentedConfig(
                waveCount: 3,
                (2, new string[0]),
                (1, new string[0]));

            LevelPhasePlan plan = LevelPhasePlan.FromConfig(config);

            Assert.IsTrue(plan.SegmentPlanInvalid,
                "If no segment plays a unit the level completes with its challenge never "
                + "played, which is exactly the defect SALIN-223 closed.");
            Assert.AreEqual(1, plan.SegmentCount);
        }

        // -------------------------------------------------------------------------
        // Fixtures
        // -------------------------------------------------------------------------

        private LevelConfigSO CreateLegacyConfig()
        {
            LevelConfigSO config = ScriptableObject.CreateInstance<LevelConfigSO>();
            _objectsToDestroy.Add(config);
            return config;
        }

        private LevelConfigSO CreateSegmentedConfig(
            int waveCount, params (int waves, string[] unitIds)[] segments)
        {
            LevelConfigSO config = CreateLegacyConfig();
            for (int i = 0; i < waveCount; i++)
                config.waves.Add(new WaveDefinition());

            var unitIds = new List<string>();
            foreach ((int _, string[] ids) in segments)
                unitIds.AddRange(ids);
            config.challengeSequence = CreateSequence(unitIds.ToArray());

            foreach ((int waves, string[] ids) in segments)
            {
                config.flowSegments.Add(new LevelFlowSegment
                {
                    waveCount = waves,
                    challengeUnitIds = ids,
                });
            }

            return config;
        }

        private ChallengeSequenceSO CreateSequence(params string[] unitIds)
        {
            ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            _objectsToDestroy.Add(sequence);
            sequence.sequenceId = "salin226-fixture";
            sequence.displayName = "SALIN-226 fixture";

            var units = new List<ChallengeUnitDefinition>();
            for (int i = 0; i < unitIds.Length; i++)
            {
                string unitId = unitIds[i];
                units.Add(new ChallengeUnitDefinition
                {
                    unitId = unitId,
                    mode = ChallengeMode.WordPlacement,
                    tokens = new[]
                    {
                        new ChallengeTokenDefinition
                        {
                            tokenId = unitId + "-t1",
                            displayText = "t1",
                            occurrenceId = unitId + "-w1",
                        },
                    },
                    slots = new[]
                    {
                        new ChallengeSlotDefinition
                        {
                            slotId = unitId + "-s1",
                            expectedOccurrenceId = unitId + "-w1",
                        },
                    },
                    candidateOccurrenceIds = new[] { unitId + "-w1" },
                    maxErrors = 3,
                    heartPenalty = 1,
                });
            }

            sequence.units = units.ToArray();
            return sequence;
        }
    }
}
