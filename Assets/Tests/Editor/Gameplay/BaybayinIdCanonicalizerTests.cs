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
        [TestCase("DA", "DA")]
        [TestCase("RA", "DA")]
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
        /// RA folds into DA, as classic Baybayin does. One glyph, two readings.
        ///
        /// This assertion was inverted on 2026-08-31 under a since-reverted reading of REQ-42 that
        /// made the set 18. REQ-42 is now resolved at **17 taught identities** (SALIN-212): the
        /// campaign catalog holds 17 and excludes Char_RA, and Char_DA carries both value.da and
        /// value.ra. The revert to 17 corrected the docs and the boss config but missed this test,
        /// which is why it is being changed here rather than in that revert.
        ///
        /// Folding matters at runtime, not just on paper. Every consumer of a recognition result
        /// compares raw ids — ActiveEnemyTracker.FindAllWithCharacter, the active-clue check in
        /// CombatResolver, BossController.TryRouteDraw — and nothing in the game carries RA. An
        /// unfolded "RA" therefore matched nothing and scored a correct draw as a miss.
        ///
        /// If this ever starts returning "RA" again, that bug is back.
        /// </summary>
        [Test]
        public void RA_FoldsIntoDA_BecauseTheyAreOneGlyph()
        {
            Assert.AreEqual("DA", BaybayinIdCanonicalizer.Canonicalize("RA"));
            Assert.AreEqual(BaybayinIdCanonicalizer.Canonicalize("DA"),
                            BaybayinIdCanonicalizer.Canonicalize("RA"),
                            "DA and RA are readings of one glyph and must canonicalize together.");
        }

        /// <summary>
        /// DA/RA is the only alias group whose members both have template files, so it is the only
        /// one that merges template sets: RA_template_01..05 load under "DA" beside
        /// DA_template_01..12. Guards the sprite-candidate path used when art is resolved by id.
        /// </summary>
        [Test]
        public void SpriteCandidates_ForRa_IncludeTheDaGlyphAndItsPairedName()
        {
            System.Collections.Generic.List<string> candidates =
                BaybayinIdCanonicalizer.GetSpriteResourceCandidates("RA");
            CollectionAssert.Contains(candidates, "DA");
            CollectionAssert.Contains(candidates, "DA-RA");
        }
    }
}
