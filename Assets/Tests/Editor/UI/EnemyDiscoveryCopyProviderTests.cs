using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public class EnemyDiscoveryCopyProviderTests
    {
        [Test]
        public void Resolve_WithSoldado_ReturnsGddBackedCopy()
        {
            EnemyDataSO data = CreateEnemyData("  SOLDADO  ");

            EnemyDiscoveryCopy copy = EnemyDiscoveryCopyProvider.Resolve(data);

            Assert.AreEqual("Soldado - The Conscripted Shadows", copy.Title);
            StringAssert.Contains("forced into military service", copy.Description);
            Assert.AreEqual("Marches forward.", copy.Power);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Resolve_WithFraile_ReturnsWordKeeperCopy()
        {
            EnemyDataSO data = CreateEnemyData("fraile");

            EnemyDiscoveryCopy copy = EnemyDiscoveryCopyProvider.Resolve(data);

            Assert.AreEqual("Fraile - The Word Keeper", copy.Title);
            StringAssert.Contains("replace Baybayin with the Latin alphabet", copy.Description);
            Assert.AreEqual("Fades in and out.", copy.Power);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Resolve_WithGuardia_ReturnsPatrolCopy()
        {
            EnemyDataSO data = CreateEnemyData("guardia");

            EnemyDiscoveryCopy copy = EnemyDiscoveryCopyProvider.Resolve(data);

            Assert.AreEqual("Guardia - The Patrol of Control", copy.Title);
            StringAssert.Contains("enforced Spanish authority", copy.Description);
            Assert.AreEqual("Moves faster.", copy.Power);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Resolve_WithCapitan_ReturnsArmoredCopy()
        {
            EnemyDataSO data = CreateEnemyData("capitan");

            EnemyDiscoveryCopy copy = EnemyDiscoveryCopyProvider.Resolve(data);

            Assert.AreEqual("Capitan - The Armored Authority", copy.Title);
            StringAssert.Contains("commanded colonial forces", copy.Description);
            Assert.AreEqual("Requires 2 hits.", copy.Power);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Resolve_WithHighRankingFriarAlias_ReturnsFallbackBecauseAliasesWereRemoved()
        {
            EnemyDataSO data = CreateEnemyData("high_ranking_friar");

            EnemyDiscoveryCopy copy = EnemyDiscoveryCopyProvider.Resolve(data);

            Assert.AreEqual("High Ranking Friar", copy.Title);
            Assert.AreEqual("A new enemy has appeared.", copy.Description);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Resolve_WithUnknownEnemyID_ReturnsSafeFallback()
        {
            EnemyDataSO data = CreateEnemyData("unknown_variant");

            EnemyDiscoveryCopy copy = EnemyDiscoveryCopyProvider.Resolve(data);

            Assert.AreEqual("Unknown Variant", copy.Title);
            Assert.AreEqual("A new enemy has appeared.", copy.Description);
            Assert.AreEqual("Observe its movement and draw the matching Baybayin character.", copy.Power);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Resolve_WithElInquisidor_ReturnsFallbackBecauseBossesDoNotUseDiscoveryCopy()
        {
            EnemyDataSO data = CreateEnemyData("elinquisidor");

            EnemyDiscoveryCopy copy = EnemyDiscoveryCopyProvider.Resolve(data);

            Assert.AreEqual("Elinquisidor", copy.Title);
            Assert.AreEqual("A new enemy has appeared.", copy.Description);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Resolve_WithNullData_ReturnsUnknownFallback()
        {
            EnemyDiscoveryCopy copy = EnemyDiscoveryCopyProvider.Resolve(null);

            Assert.AreEqual("Unknown", copy.Title);
            Assert.AreEqual("A new enemy has appeared.", copy.Description);
        }

        private static EnemyDataSO CreateEnemyData(string enemyID)
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = enemyID;
            return data;
        }
    }
}
