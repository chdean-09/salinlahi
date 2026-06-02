using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    public class WaveSpawnerSubsetTests
    {
        [Test]
        public void SelectCharacterForSpawn_ReturnsOnlyFromWaveSubset()
        {
            GameObject obj = new("WaveSpawner");
            WaveSpawner spawner = obj.AddComponent<WaveSpawner>();
            BaybayinCharacterSO inSubset = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            EnemyDataSO enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            WaveDefinition wave = new()
            {
                characters = new List<BaybayinCharacterSO> { inSubset },
                enemyTypes = new List<EnemyDataSO> { enemy },
            };

            try
            {
                BaybayinCharacterSO result =
                    InvokePrivate(spawner, "SelectCharacterForSpawn", wave, enemy) as BaybayinCharacterSO;
                Assert.AreSame(inSubset, result);
            }
            finally
            {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(inSubset);
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void SelectEnemyDataForSpawn_ReturnsOnlyFromWaveSubset()
        {
            GameObject obj = new("WaveSpawner");
            WaveSpawner spawner = obj.AddComponent<WaveSpawner>();
            EnemyDataSO inSubset = ScriptableObject.CreateInstance<EnemyDataSO>();
            WaveDefinition wave = new() { enemyTypes = new List<EnemyDataSO> { inSubset } };

            try
            {
                EnemyDataSO result =
                    InvokePrivate(spawner, "SelectEnemyDataForSpawn", wave) as EnemyDataSO;
                Assert.AreSame(inSubset, result);
            }
            finally
            {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(inSubset);
            }
        }

        [Test]
        public void SelectCharacterForSpawn_FallsBackToAssignedCharacter_WhenSubsetEmpty()
        {
            GameObject obj = new("WaveSpawner");
            WaveSpawner spawner = obj.AddComponent<WaveSpawner>();
            BaybayinCharacterSO assigned = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            EnemyDataSO enemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            enemy.assignedCharacter = assigned;
            WaveDefinition wave = new() { characters = new List<BaybayinCharacterSO>() };

            try
            {
                BaybayinCharacterSO result =
                    InvokePrivate(spawner, "SelectCharacterForSpawn", wave, enemy) as BaybayinCharacterSO;
                Assert.AreSame(assigned, result);
            }
            finally
            {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(assigned);
                Object.DestroyImmediate(enemy);
            }
        }

        private static object InvokePrivate(object target, string method, params object[] args)
        {
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, $"Missing method '{method}' on {target.GetType().Name}.");
            return m.Invoke(target, args);
        }
    }
}
