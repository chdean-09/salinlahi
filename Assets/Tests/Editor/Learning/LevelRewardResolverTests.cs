using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Learning
{
    /// <summary>
    /// SALIN-202: reward derivation from a level config — symbols first introduced
    /// by the level unlock, memory-prefixed reward ids become memories, and every
    /// reward id is claimed. Replay duplication is prevented downstream by the
    /// outcome coordinator's applied-receipt union.
    /// </summary>
    [TestFixture]
    public sealed class LevelRewardResolverTests
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

        private BaybayinCharacterSO Symbol(string stableId, string introLevelId)
        {
            var symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.stableId = stableId;
            symbol.firstIntroductionLevelId = introLevelId;
            _objectsToDestroy.Add(symbol);
            return symbol;
        }

        private LevelConfigSO Level(string stableId, params BaybayinCharacterSO[] pool)
        {
            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.stableId = stableId;
            foreach (BaybayinCharacterSO symbol in pool)
            {
                level.cumulativeSymbolPool.Add(new SymbolValueReference
                {
                    symbol = symbol,
                    spokenValueId = "value." + symbol.stableId.Substring("symbol.".Length),
                });
            }

            _objectsToDestroy.Add(level);
            return level;
        }

        [Test]
        public void UnlockedSymbols_AreThoseFirstIntroducedByThisLevel()
        {
            LevelConfigSO level = Level(
                "level.ugat.01",
                Symbol("symbol.ei", "level.ugat.01"),
                Symbol("symbol.na", "level.ugat.01"),
                Symbol("symbol.ba", "level.ugat.02"));

            RewardGrant grant = LevelRewardResolver.Resolve(level);

            CollectionAssert.AreEquivalent(
                new[] { "symbol.ei", "symbol.na" }, grant.UnlockedSymbolIds.ToList());
        }

        [Test]
        public void MemoryIds_AreTheMemoryPrefixedRewards_AndAllRewardsAreClaimed()
        {
            LevelConfigSO level = Level("level.ugat.01");
            level.rewardIds.Add("memory.ugat.01");
            level.rewardIds.Add("title.unang-alaala");

            RewardGrant grant = LevelRewardResolver.Resolve(level);

            CollectionAssert.AreEqual(new[] { "memory.ugat.01" }, grant.UnlockedMemoryIds.ToList());
            CollectionAssert.AreEquivalent(
                new[] { "memory.ugat.01", "title.unang-alaala" }, grant.ClaimedRewardIds.ToList());
        }

        [Test]
        public void EmptyLevel_GrantsNothingButNeverNull()
        {
            RewardGrant grant = LevelRewardResolver.Resolve(Level("level.ugat.02"));

            Assert.IsNotNull(grant.UnlockedSymbolIds);
            Assert.IsNotNull(grant.UnlockedMemoryIds);
            Assert.IsNotNull(grant.ClaimedRewardIds);
            Assert.IsEmpty(grant.UnlockedSymbolIds);
            Assert.IsEmpty(grant.UnlockedMemoryIds);
            Assert.IsEmpty(grant.ClaimedRewardIds);
        }

        [Test]
        public void NullPoolEntriesAndBlankRewards_AreIgnored()
        {
            LevelConfigSO level = Level("level.ugat.01", Symbol("symbol.ei", "level.ugat.01"));
            level.cumulativeSymbolPool.Add(null);
            level.cumulativeSymbolPool.Add(new SymbolValueReference());
            level.rewardIds.Add("");
            level.rewardIds.Add(null);

            RewardGrant grant = LevelRewardResolver.Resolve(level);

            CollectionAssert.AreEqual(new[] { "symbol.ei" }, grant.UnlockedSymbolIds.ToList());
            Assert.IsEmpty(grant.ClaimedRewardIds);
        }
    }
}
