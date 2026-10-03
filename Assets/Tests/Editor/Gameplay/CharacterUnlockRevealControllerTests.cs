using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class CharacterUnlockRevealControllerTests
    {
        private static BaybayinCharacterSO MakeChar(string id)
        {
            BaybayinCharacterSO c = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            c.characterID = id;
            return c;
        }

        [Test]
        public void BuildRevealQueue_PreservesOrder_AndKeepsOnlyNotUnlocked()
        {
            BaybayinCharacterSO ba = MakeChar("BA");
            BaybayinCharacterSO sa = MakeChar("SA");
            BaybayinCharacterSO la = MakeChar("LA");
            try
            {
                var allowed = new List<BaybayinCharacterSO> { ba, sa, la };
                // SA is already unlocked → it must be filtered out; order otherwise preserved.
                List<BaybayinCharacterSO> queue =
                    CharacterUnlockRevealController.BuildRevealQueue(allowed, c => c == sa);

                Assert.AreEqual(2, queue.Count);
                Assert.AreSame(ba, queue[0]);
                Assert.AreSame(la, queue[1]);
            }
            finally
            {
                Object.DestroyImmediate(ba);
                Object.DestroyImmediate(sa);
                Object.DestroyImmediate(la);
            }
        }

        [Test]
        public void BuildRevealQueue_SkipsNullEntries()
        {
            BaybayinCharacterSO ba = MakeChar("BA");
            try
            {
                var allowed = new List<BaybayinCharacterSO> { null, ba, null };
                List<BaybayinCharacterSO> queue =
                    CharacterUnlockRevealController.BuildRevealQueue(allowed, _ => false);

                Assert.AreEqual(1, queue.Count);
                Assert.AreSame(ba, queue[0]);
            }
            finally { Object.DestroyImmediate(ba); }
        }

        [Test]
        public void BuildRevealQueue_AllUnlocked_ReturnsEmpty()
        {
            BaybayinCharacterSO ba = MakeChar("BA");
            try
            {
                var allowed = new List<BaybayinCharacterSO> { ba };
                Assert.IsEmpty(CharacterUnlockRevealController.BuildRevealQueue(allowed, _ => true));
            }
            finally { Object.DestroyImmediate(ba); }
        }

        // With the scroll interstitial switched off in LevelFlowController, this is the path that
        // keeps the unlock DATA alive: the Almanac reads CharacterUnlockProgress, so the characters
        // must still be marked, and marking them is also what stops a re-enabled reveal from
        // replaying every character earned in the meantime.
        [Test]
        public void RegisterUnlocksWithoutReveal_MarksEachCharacterOnce()
        {
            CharacterUnlockProgress.ClearAllUnlocked();
            BaybayinCharacterSO ba = MakeChar("BA");
            BaybayinCharacterSO la = MakeChar("LA");
            try
            {
                var queue = new List<BaybayinCharacterSO> { ba, null, la };

                Assert.AreEqual(2, CharacterUnlockRevealController.RegisterUnlocksWithoutReveal(queue));
                Assert.IsTrue(CharacterUnlockProgress.HasUnlocked(ba));
                Assert.IsTrue(CharacterUnlockProgress.HasUnlocked(la));

                // Already unlocked → nothing new to mark, and BuildRevealQueue now filters them out.
                Assert.AreEqual(0, CharacterUnlockRevealController.RegisterUnlocksWithoutReveal(queue));
                Assert.IsEmpty(CharacterUnlockRevealController.BuildRevealQueue(
                    queue, CharacterUnlockProgress.HasUnlocked));
            }
            finally
            {
                CharacterUnlockProgress.ClearAllUnlocked();
                Object.DestroyImmediate(ba);
                Object.DestroyImmediate(la);
            }
        }

        [Test]
        public void RegisterUnlocksWithoutReveal_NullList_ReturnsZero()
        {
            Assert.AreEqual(0, CharacterUnlockRevealController.RegisterUnlocksWithoutReveal(null));
        }

        [Test]
        public void BuildRevealQueue_NullArgs_ReturnsEmpty()
        {
            Assert.IsEmpty(CharacterUnlockRevealController.BuildRevealQueue(null, _ => false));

            BaybayinCharacterSO ba = MakeChar("BA");
            try
            {
                Assert.IsEmpty(CharacterUnlockRevealController.BuildRevealQueue(
                    new List<BaybayinCharacterSO> { ba }, null));
            }
            finally { Object.DestroyImmediate(ba); }
        }
    }
}
