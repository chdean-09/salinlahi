using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-208. Locks the ID aliasing that decides which filename a recording must use.
    ///
    /// This matters because pronunciation clips are assigned by filename:
    /// BaybayinPronunciationAudioSync canonicalizes each file's name and matches it to a
    /// character. If the alias table changes, a recording session's output silently stops
    /// assigning — and the failure looks like "audio just doesn't play" rather than a mapping bug.
    /// </summary>
    public sealed class BaybayinIdCanonicalizerTests
    {
        [TestCase("EI", "EI")]
        [TestCase("E",  "EI")]
        [TestCase("I",  "EI")]
        [TestCase("OU", "OU")]
        [TestCase("O",  "OU")]
        [TestCase("U",  "OU")]
        [TestCase("PA", "PA")]
        [TestCase("FA", "PA")]
        [TestCase("BA", "BA")]
        [TestCase("VA", "BA")]
        [TestCase("SA", "SA")]
        [TestCase("ZA", "SA")]
        public void AliasSpellings_ResolveToTheCanonicalCharacter(string raw, string expected)
        {
            Assert.AreEqual(expected, BaybayinIdCanonicalizer.Canonicalize(raw));
        }

        [TestCase("A")]
        [TestCase("GA")]
        [TestCase("LA")]
        [TestCase("MA")]
        [TestCase("NA")]
        [TestCase("NGA")]
        [TestCase("RA")]
        [TestCase("TA")]
        [TestCase("YA")]
        public void CharactersWithoutAliases_CanonicalizeToThemselves(string id)
        {
            Assert.AreEqual(id, BaybayinIdCanonicalizer.Canonicalize(id));
        }

        [Test]
        public void Canonicalize_IsCaseInsensitive()
        {
            // Recordings arrive named however the person exported them.
            Assert.AreEqual("EI", BaybayinIdCanonicalizer.Canonicalize("i"));
            Assert.AreEqual("BA", BaybayinIdCanonicalizer.Canonicalize("va"));
            Assert.AreEqual("NGA", BaybayinIdCanonicalizer.Canonicalize("nga"));
        }

        [Test]
        public void EmptyAndUnknownInput_DoNotThrow()
        {
            Assert.AreEqual(string.Empty, BaybayinIdCanonicalizer.Canonicalize(null));
            Assert.AreEqual(string.Empty, BaybayinIdCanonicalizer.Canonicalize("   "));
            // An unrecognised id passes through rather than being mapped to something arbitrary.
            Assert.AreEqual("ZZ", BaybayinIdCanonicalizer.Canonicalize("ZZ"));
        }

        /// <summary>
        /// RA canonicalizes to itself, NOT to DA. Classic Baybayin folds RA into DA, and this
        /// build deliberately does not (REQ-42, ruled 2026-08-31). If this ever starts returning
        /// "DA", the 18-character set has been silently collapsed back to 17.
        /// </summary>
        [Test]
        public void RA_IsItsOwnCharacter_NotFoldedIntoDA()
        {
            Assert.AreEqual("RA", BaybayinIdCanonicalizer.Canonicalize("RA"));
            Assert.AreNotEqual(BaybayinIdCanonicalizer.Canonicalize("DA"),
                               BaybayinIdCanonicalizer.Canonicalize("RA"));
        }
    }
}
