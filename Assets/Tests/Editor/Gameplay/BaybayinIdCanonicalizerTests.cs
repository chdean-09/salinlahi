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
        // SALIN-217: DARA is retained as a legacy alias of DA, but RA is no longer one — it is its
        // own identity and is asserted in CharactersWithoutAliases_CanonicalizeToThemselves.
        [TestCase("DARA", "DA")]
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
        /// RA stands on its own. It is not a reading of DA.
        ///
        /// This file has flip-flopped twice, so the current reason matters more than the history:
        /// ruling Q2 (2026-09-11), reaffirmed by OQ-6 (2026-09-12), sets the taught set at **18
        /// identities with DA and RA separate** (SALIN-217). Char_RA now holds stableId symbol.ra
        /// and value.ra, sits in the campaign catalog, and enters the taught pool at Level 13.
        ///
        /// The fold it replaces (SALIN-212) existed because every consumer of a recognition result
        /// compares raw ids — ActiveEnemyTracker.FindAllWithCharacter, the active-clue check in
        /// CombatResolver, BossController.TryRouteDraw — and nothing in the game carried RA, so an
        /// unfolded "RA" matched nothing and scored a correct draw as a miss. That premise is what
        /// SALIN-217 removed; the fold is not safe to reinstate without putting RA back out of the
        /// catalog first.
        ///
        /// If this starts returning "DA" again, the 17-symbol model has crept back in.
        /// </summary>
        [Test]
        public void RA_CanonicalizesToItself_BecauseDaAndRaAreSeparateIdentities()
        {
            Assert.AreEqual("RA", BaybayinIdCanonicalizer.Canonicalize("RA"));
            Assert.AreNotEqual(BaybayinIdCanonicalizer.Canonicalize("DA"),
                               BaybayinIdCanonicalizer.Canonicalize("RA"),
                               "DA and RA are separate taught identities under ruling Q2.");
        }

        /// <summary>
        /// "DARA" stays mapped to DA so ids written while the SALIN-212 fold was in force still
        /// resolve rather than falling through Canonicalize's pass-through as an unknown id.
        /// </summary>
        [Test]
        public void LegacyDaraAlias_StillResolvesToDa()
        {
            Assert.AreEqual("DA", BaybayinIdCanonicalizer.Canonicalize("DARA"));
        }

        /// <summary>
        /// The shipped glyph art is still filed under the paired name DA-RA, so RA offers both its
        /// own key and that paired name. Guards the sprite-candidate path used when art is resolved
        /// by id — Char_RA has no almanac or badge sprite of its own yet (content-blocked under
        /// SALIN-217), so this fallback is what renders RA today.
        /// </summary>
        [Test]
        public void SpriteCandidates_ForRa_IncludeItsOwnKeyAndThePairedName()
        {
            System.Collections.Generic.List<string> candidates =
                BaybayinIdCanonicalizer.GetSpriteResourceCandidates("RA");
            CollectionAssert.Contains(candidates, "RA");
            CollectionAssert.Contains(candidates, "DA-RA");
        }
    }
}
