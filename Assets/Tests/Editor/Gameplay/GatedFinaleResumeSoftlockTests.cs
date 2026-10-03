using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// Covers the resume softlock: a run that was paused or quit during the FINAL wave, after that
    /// wave's last enemy had already spawned, came back unwinnable AND unloseable.
    ///
    /// <para>
    /// The chain: <c>WaveManager.ResolveResumeWaveIndex</c> returns <c>waves.Count</c> for that
    /// save (the saved wave is complete, so it advances past it and clamps to the end). The resumed
    /// <c>RunAllWavesRoutine</c> loop body then never executes, so the per-wave
    /// <c>IsFinalWaveIndex</c> block that opens <see cref="SpawnGateRegistry.FinalWaveReached"/> is
    /// never reached. Control falls straight through to <c>RunRestorationOverflow</c>, where
    /// Levels 2-4 ship <c>maxOverflowBatches = 0</c>, so <c>ShouldContinueOverflow</c> is
    /// permanently true. The gated slot is still closed, so the director's eligible set is empty,
    /// it returns <c>HoldForGate</c> forever and <c>WantsOverflow</c> never goes false. The player
    /// can neither restore the last slot nor run out of waves. The old hardcoded
    /// <c>MaxOverflowBatches = 12</c> used to bound this by accident.
    /// </para>
    ///
    /// <para>
    /// WHY THESE ARE PURE TESTS OF THE DECISION rather than a driven coroutine: reaching
    /// <c>RunRestorationOverflow</c> for real needs <c>WaveManager.ValidateRunDependencies</c> to
    /// pass, which needs live <c>EnemyPool.Instance</c> and <c>ActiveEnemyTracker.Instance</c>
    /// singletons. Those register in Awake/OnEnable, which EditMode never runs, so the scaffolding
    /// would be a PlayMode scene rather than a test. Each link of the chain is asserted here
    /// instead, plus the fix's own effect on a real coordinator.
    /// </para>
    /// </summary>
    public sealed class GatedFinaleResumeSoftlockTests
    {
        private static List<WaveDefinition> ThreeWaves(int enemiesPerWave)
        {
            var waves = new List<WaveDefinition>();
            for (int index = 0; index < 3; index++)
                waves.Add(new WaveDefinition { enemyCount = enemiesPerWave });

            return waves;
        }

        [Test]
        public void PausingAfterTheFinalWavesLastSpawn_ResumesPastTheEndOfTheWaveList()
        {
            List<WaveDefinition> waves = ThreeWaves(enemiesPerWave: 5);

            int resumeIndex = WaveManager.ResolveResumeWaveIndex(
                waves,
                hasSavedWaveProgress: true,
                savedWaveIndex: 2,
                savedWaveSpawnedCount: 5,
                out int spawnOffset);

            Assert.AreEqual(waves.Count, resumeIndex,
                "a save taken on the last wave after its last enemy spawned resumes one past the "
                + "end of the wave list. This is the precondition for the softlock, so it is "
                + "asserted rather than assumed.");
            Assert.AreEqual(0, spawnOffset,
                "resuming past the list starts no wave, so there is no partial-wave offset.");
        }

        [Test]
        public void ResumingPastTheEnd_NeverReachesThePerWaveGateOpening()
        {
            List<WaveDefinition> waves = ThreeWaves(enemiesPerWave: 5);

            Assert.IsFalse(WaveManager.IsFinalWaveIndex(waves.Count, waves.Count),
                "the loop body never runs for a start index at the exclusive bound, so the "
                + "per-wave finale-gate opening inside RunAllWavesRoutine cannot fire. The gate "
                + "must therefore be opened somewhere that does not depend on the loop.");
        }

        [Test]
        public void UnboundedOverflow_NeverEndsOnItsOwn()
        {
            var policy = new SpawnAssignmentPolicy { maxOverflowBatches = 0 };
            Assert.IsTrue(policy.OverflowIsUnbounded,
                "test setup: Levels 2-4 ship maxOverflowBatches = 0.");

            Assert.IsTrue(
                WaveManager.ShouldContinueOverflow(
                    batch: 10_000, policy.OverflowIsUnbounded, policy.maxOverflowBatches),
                "with an unbounded escort budget the overflow loop has no batch ceiling to stop "
                + "it, so a still-closed finale gate here is a permanent softlock rather than an "
                + "eventual defeat. This is why the old hardcoded 12-batch cap hid the bug.");
        }

        /// <summary>
        /// The fix itself: <c>WaveManager.OpenFinaleGate</c> releases the withheld slot on the
        /// scene's coordinator, and <c>RunAllWavesRoutine</c> calls it unconditionally immediately
        /// before <c>RunRestorationOverflow</c>. Reaching overflow means the wave list is exhausted
        /// by definition, so the gate is safe to open there no matter how the resume index was
        /// computed.
        /// </summary>
        [Test]
        public void OpenFinaleGate_ReleasesTheWithheldSlotWithoutRunningAWave()
        {
            var coordinatorObject = new GameObject(nameof(SpawnAssignmentCoordinator));
            var waveManagerObject = new GameObject(nameof(WaveManager));
            try
            {
                var coordinator = coordinatorObject.AddComponent<SpawnAssignmentCoordinator>();
                coordinator.ApplyLevel(GatedTwoWordLevel(), null);

                Assert.IsFalse(coordinator.Gates.IsOpen(SpawnGateRegistry.FinalWaveReached),
                    "test setup: the finale gate starts closed.");

                var waveManager = waveManagerObject.AddComponent<WaveManager>();
                waveManager.OpenFinaleGate();

                Assert.IsTrue(coordinator.Gates.IsOpen(SpawnGateRegistry.FinalWaveReached),
                    "the pre-overflow opening must release the finale gate without any wave having "
                    + "started. Without it, a run resumed past the end of the wave list holds the "
                    + "gated slot closed forever: unwinnable, and unloseable too, because "
                    + "unbounded overflow keeps the run alive.");

                waveManager.OpenFinaleGate();
                Assert.IsTrue(coordinator.Gates.IsOpen(SpawnGateRegistry.FinalWaveReached),
                    "opening is idempotent, so keeping the per-wave opening as well costs nothing "
                    + "on the ordinary path.");
            }
            finally
            {
                Object.DestroyImmediate(waveManagerObject);
                Object.DestroyImmediate(coordinatorObject);
            }
        }

        private static LevelConfigSO GatedTwoWordLevel()
        {
            var config = ScriptableObject.CreateInstance<LevelConfigSO>();
            config.activeClueCombatEnabled = true;
            config.spawnAssignmentPolicy = new SpawnAssignmentPolicy
            {
                gateFinalSlotToFinalWave = true,
                maxOverflowBatches = 0,
            };
            config.focusWords = new List<FocusWordDefinition>
            {
                new FocusWordDefinition
                {
                    stableId = "word.bata",
                    decomposition = new List<SymbolValueReference>
                    {
                        new SymbolValueReference { symbol = Symbol("ba") },
                        new SymbolValueReference { symbol = Symbol("ta") },
                    },
                },
                new FocusWordDefinition
                {
                    stableId = "word.mata",
                    decomposition = new List<SymbolValueReference>
                    {
                        new SymbolValueReference { symbol = Symbol("ma") },
                        new SymbolValueReference { symbol = Symbol("ta") },
                    },
                },
            };
            return config;
        }

        private static BaybayinCharacterSO Symbol(string id)
        {
            var symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.characterID = id.ToUpperInvariant();
            symbol.stableId = "symbol." + id;
            return symbol;
        }
    }
}
