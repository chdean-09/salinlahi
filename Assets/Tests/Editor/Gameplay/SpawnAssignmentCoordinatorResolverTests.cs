using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// Ugat QA 2026-09-16: ResolveEnemyData used to read only the wave's own enemyTypes and return
    /// null on a miss, and WaveSpawner then kept its random type roll. On a level whose waves narrow
    /// the roster that put a needed symbol on a body that contradicts its badge - Level 5 wave 1
    /// needed MA but listed no Mantsa, so MA spawned on a Bakod. ResolveCharacter already fell back to
    /// the level roster; these tests pin the same fallback onto the enemy side so the bijection the
    /// glyph badge asserts holds for any wave.
    /// </summary>
    public class SpawnAssignmentCoordinatorResolverTests
    {
        private static BaybayinCharacterSO Symbol(string characterId, string stableId)
        {
            var symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.characterID = characterId;
            symbol.stableId = stableId;
            return symbol;
        }

        private static EnemyDataSO Enemy(string id, BaybayinCharacterSO assigned)
        {
            var data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = id;
            data.displayName = id;
            data.assignedCharacter = assigned;
            return data;
        }

        [Test]
        public void ResolveEnemyData_FallsBackToTheLevelRosterWhenTheWaveNarrowsIt()
        {
            var go = new GameObject("SpawnAssignmentCoordinator");
            try
            {
                BaybayinCharacterSO ei = Symbol("EI", "symbol.ei");
                BaybayinCharacterSO ma = Symbol("MA", "symbol.ma");
                EnemyDataSO iligaw = Enemy("iligaw", ei);
                EnemyDataSO mantsa = Enemy("mantsa", ma);

                var config = ScriptableObject.CreateInstance<LevelConfigSO>();
                config.activeClueCombatEnabled = true;
                config.allowedCharacters = new List<BaybayinCharacterSO> { ei, ma };
                config.allowedEnemyTypes = new List<EnemyDataSO> { iligaw, mantsa };

                // The narrowed wave: it carries EI only, exactly Level 5 wave 1's old shape.
                var wave = new WaveDefinition { enemyTypes = new List<EnemyDataSO> { iligaw } };

                var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
                coordinator.ApplyLevel(config, null);

                Assert.AreSame(mantsa, coordinator.ResolveEnemyData("symbol.ma", wave),
                    "a symbol the wave does not carry must still resolve to the enemy that owns it on "
                    + "the level roster, or the spawner keeps its random roll and the enemy body "
                    + "contradicts the glyph badge it wears");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveEnemyData_PrefersTheWavesOwnEnemyOverTheLevelRoster()
        {
            var go = new GameObject("SpawnAssignmentCoordinator");
            try
            {
                BaybayinCharacterSO ma = Symbol("MA", "symbol.ma");
                EnemyDataSO waveMantsa = Enemy("mantsa-wave", ma);
                EnemyDataSO rosterMantsa = Enemy("mantsa-roster", ma);

                var config = ScriptableObject.CreateInstance<LevelConfigSO>();
                config.activeClueCombatEnabled = true;
                config.allowedEnemyTypes = new List<EnemyDataSO> { rosterMantsa };

                var wave = new WaveDefinition { enemyTypes = new List<EnemyDataSO> { waveMantsa } };

                var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
                coordinator.ApplyLevel(config, null);

                Assert.AreSame(waveMantsa, coordinator.ResolveEnemyData("symbol.ma", wave),
                    "the fallback must not override a wave that already carries the symbol");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Level13_NeededSaHasARealCarrier_WhileSalungatRemainsADecoy()
        {
            var level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                "Assets/ScriptableObjects/Levels/Level13_Config.asset");
            var sa = UnityEditor.AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(
                "Assets/ScriptableObjects/Characters/Char_SA.asset");
            var go = new GameObject("NeededSalungatCarrierTest");
            try
            {
                var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
                coordinator.ApplyLevel(level, null);
                var assignment = new SpawnAssignment { SymbolStableId = sa.stableId, Role = SpawnAssignmentRole.Needed };
                var carrier = coordinator.ResolveEnemyData(assignment, level.waves[0]);
                Assert.IsNotNull(carrier);
                Assert.IsFalse(carrier.isDecoy,
                    "A required SA slot cannot be offered only as Salungat: its penalty never restores a slot.");
                var filler = coordinator.ResolveEnemyData(sa.stableId, level.waves[0]);
                Assert.IsTrue(filler.isDecoy, "Salungat filler must retain the decoy penalty.");
                var state = new RestorationObjectiveState();
                state.ConfigureFromFocusWords(level.focusWords);
                Assert.IsTrue(state.TryRestore(sa.stableId).Applied);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NeededCarrier_PrefersARealMatchingType_AndRejectsAnAllDecoyRoster(bool hasRealCarrier)
        {
            var go = new GameObject("NeededCarrierOwnershipTest");
            var sa = Symbol("SA", "symbol.sa");
            var decoy = Enemy("salungat", sa);
            decoy.isDecoy = true;
            var real = Enemy("real-sa", sa);
            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            try
            {
                level.allowedEnemyTypes = hasRealCarrier
                    ? new List<EnemyDataSO> { decoy, real } : new List<EnemyDataSO> { decoy };
                var wave = new WaveDefinition { enemyTypes = new List<EnemyDataSO> { decoy } };
                var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
                coordinator.ApplyLevel(level, null);
                var needed = new SpawnAssignment { SymbolStableId = sa.stableId, Role = SpawnAssignmentRole.Needed };
                if (!hasRealCarrier)
                {
                    LogAssert.Expect(LogType.Error,
                        "[Salinlahi] SpawnAssignmentCoordinator: no real enemy can carry required symbol "
                        + "'symbol.sa'. Check the wave and level enemy rosters.");
                }
                Assert.AreSame(hasRealCarrier ? real : null, coordinator.ResolveEnemyData(needed, wave));
                var filler = new SpawnAssignment { SymbolStableId = sa.stableId, Role = SpawnAssignmentRole.Filler };
                Assert.AreSame(decoy, coordinator.ResolveEnemyData(filler, wave));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(level);
                Object.DestroyImmediate(decoy);
                Object.DestroyImmediate(real);
                Object.DestroyImmediate(sa);
            }
        }

        [Test]
        public void ResolveEnemyData_ReturnsNullWhenNoEnemyOnTheLevelOwnsTheSymbol()
        {
            var go = new GameObject("SpawnAssignmentCoordinator");
            try
            {
                BaybayinCharacterSO ei = Symbol("EI", "symbol.ei");
                EnemyDataSO iligaw = Enemy("iligaw", ei);

                var config = ScriptableObject.CreateInstance<LevelConfigSO>();
                config.activeClueCombatEnabled = true;
                config.allowedEnemyTypes = new List<EnemyDataSO> { iligaw };

                var wave = new WaveDefinition { enemyTypes = new List<EnemyDataSO> { iligaw } };

                var coordinator = go.AddComponent<SpawnAssignmentCoordinator>();
                coordinator.ApplyLevel(config, null);

                Assert.IsNull(coordinator.ResolveEnemyData("symbol.wa", wave),
                    "an unowned symbol still returns null so the caller keeps its own type roll");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
