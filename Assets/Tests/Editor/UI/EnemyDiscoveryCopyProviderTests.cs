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
            ApplyDiscoveryCopyFields(data);
            return data;
        }

        private static void ApplyDiscoveryCopyFields(EnemyDataSO data)
        {
            switch (data.enemyID?.Trim().ToLowerInvariant())
            {
                case "soldado":
                    data.displayName = "Soldado";
                    data.discoverySubtitle = "The Conscripted Shadows";
                    data.description = "During the Spanish occupation, many natives were forced into military service under colonial command. They became symbols of obedience to foreign rule.\n\nPower: Marches forward.";
                    break;
                case "fraile":
                    data.displayName = "Fraile";
                    data.discoverySubtitle = "The Word Keeper";
                    data.description = "Frailes controlled education, religion, and writing, helping replace Baybayin with the Latin alphabet. Their influence caused generations to forget the old script.\n\nPower: Fades in and out.";
                    break;
                case "guardia":
                    data.displayName = "Guardia";
                    data.discoverySubtitle = "The Patrol of Control";
                    data.description = "The Guardia Civil enforced Spanish authority across towns and villages. Their presence discouraged resistance and protected colonial rule.\n\nPower: Moves faster.";
                    break;
                case "capitan":
                    data.displayName = "Capitan";
                    data.discoverySubtitle = "The Armored Authority";
                    data.description = "Captains held positions of power and commanded colonial forces. Their rank and protection made them difficult to challenge.\n\nPower: Requires 2 hits.";
                    break;
            }
        }
    }
}
