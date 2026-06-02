using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public class AlmanacControllerTests
    {
        // ---- AlmanacCell decision logic ----

        [Test]
        public void ShouldShowBossBorder_OnlyWhenBossAndRevealed()
        {
            Assert.IsTrue(AlmanacCell.ShouldShowBossBorder(isBoss: true, isRevealed: true));
            Assert.IsFalse(AlmanacCell.ShouldShowBossBorder(isBoss: true, isRevealed: false),
                "a locked boss must read as a plain '?', no red border");
            Assert.IsFalse(AlmanacCell.ShouldShowBossBorder(isBoss: false, isRevealed: true));
            Assert.IsFalse(AlmanacCell.ShouldShowBossBorder(isBoss: false, isRevealed: false));
        }

        [Test]
        public void ShouldBeInteractable_OnlyWhenRevealed()
        {
            Assert.IsTrue(AlmanacCell.ShouldBeInteractable(isRevealed: true));
            Assert.IsFalse(AlmanacCell.ShouldBeInteractable(isRevealed: false));
        }

        // ---- AlmanacController counter math ----

        [Test]
        public void CountUnlockedCharacters_CountsOnlyUnlockedNonNull()
        {
            BaybayinCharacterSO a = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            BaybayinCharacterSO b = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            var all = new List<BaybayinCharacterSO> { a, null, b };
            try
            {
                int count = AlmanacController.CountUnlockedCharacters(all, c => c == a);
                Assert.AreEqual(1, count);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void CountDiscoveredEnemies_CountsOnlyDiscoveredNonNullEntries()
        {
            EnemyDataSO seen = ScriptableObject.CreateInstance<EnemyDataSO>();
            EnemyDataSO unseen = ScriptableObject.CreateInstance<EnemyDataSO>();
            var entries = new List<AlmanacEnemyEntry>
            {
                new AlmanacEnemyEntry { enemyData = seen },
                null,
                new AlmanacEnemyEntry { enemyData = unseen },
            };
            try
            {
                int count = AlmanacController.CountDiscoveredEnemies(entries, e => e == seen);
                Assert.AreEqual(1, count);
            }
            finally
            {
                Object.DestroyImmediate(seen);
                Object.DestroyImmediate(unseen);
            }
        }

        [Test]
        public void CountHelpers_NullArgs_ReturnZero()
        {
            Assert.AreEqual(0, AlmanacController.CountUnlockedCharacters(null, c => true));
            Assert.AreEqual(0, AlmanacController.CountDiscoveredEnemies(null, e => true));
        }

        [Test]
        public void FormatCounter_RendersLabelAndFraction()
        {
            Assert.AreEqual("Learned 2/17", AlmanacController.FormatCounter("Learned", 2, 17));
            Assert.AreEqual("Discovered 0/9", AlmanacController.FormatCounter("Discovered", 0, 9));
        }
    }
}
