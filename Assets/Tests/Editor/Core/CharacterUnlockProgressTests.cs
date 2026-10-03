using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Core
{
    [TestFixture]
    public class CharacterUnlockProgressTests
    {
        private BaybayinCharacterSO _ba;

        private static BaybayinCharacterSO MakeChar(string id)
        {
            BaybayinCharacterSO c = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            c.characterID = id;
            return c;
        }

        [SetUp]
        public void SetUp()
        {
            CharacterUnlockProgress.ClearAllUnlocked();
            _ba = MakeChar("BA");
        }

        [TearDown]
        public void TearDown()
        {
            CharacterUnlockProgress.ClearAllUnlocked();
            if (_ba != null) Object.DestroyImmediate(_ba);
        }

        [Test]
        public void TryMarkUnlocked_NewId_ReturnsTrueAndPersists()
        {
            bool added = CharacterUnlockProgress.TryMarkUnlocked(_ba, out string id);
            Assert.IsTrue(added);
            Assert.AreEqual("ba", id);
            Assert.IsTrue(CharacterUnlockProgress.HasUnlocked(_ba));
        }

        [Test]
        public void TryMarkUnlocked_Repeated_ReturnsFalse()
        {
            CharacterUnlockProgress.TryMarkUnlocked(_ba, out _);
            bool addedAgain = CharacterUnlockProgress.TryMarkUnlocked(_ba, out _);
            Assert.IsFalse(addedAgain, "second add of the same id must return false");
        }

        [Test]
        public void HasUnlocked_IsCaseInsensitive()
        {
            CharacterUnlockProgress.TryMarkUnlocked(_ba, out _); // stored as "ba"
            BaybayinCharacterSO lower = MakeChar("ba");
            try
            {
                Assert.IsTrue(CharacterUnlockProgress.HasUnlocked(lower));
                bool addedAgain = CharacterUnlockProgress.TryMarkUnlocked(lower, out _);
                Assert.IsFalse(addedAgain, "different casing must not be treated as a new id");
            }
            finally { Object.DestroyImmediate(lower); }
        }

        [Test]
        public void ClearAllUnlocked_RemovesEverything()
        {
            CharacterUnlockProgress.TryMarkUnlocked(_ba, out _);
            CharacterUnlockProgress.ClearAllUnlocked();
            Assert.IsFalse(CharacterUnlockProgress.HasUnlocked(_ba));
        }

        [Test]
        public void NullAndBlankIds_AreSafeNoOps()
        {
            BaybayinCharacterSO blank = MakeChar("");
            BaybayinCharacterSO whitespace = MakeChar("   ");
            try
            {
                Assert.IsFalse(CharacterUnlockProgress.TryMarkUnlocked(null, out _));
                Assert.IsFalse(CharacterUnlockProgress.TryMarkUnlocked(blank, out _));
                Assert.IsFalse(CharacterUnlockProgress.TryMarkUnlocked(whitespace, out _));
                Assert.IsFalse(CharacterUnlockProgress.HasUnlocked(null));
                Assert.IsFalse(CharacterUnlockProgress.HasUnlocked(blank));
            }
            finally
            {
                Object.DestroyImmediate(blank);
                Object.DestroyImmediate(whitespace);
            }
        }
    }
}
