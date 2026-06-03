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
