using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-182 completion criteria 1-4, at the level of the contract itself. Pure static mapping
    /// with no MonoBehaviour lifecycle, so Edit Mode is correct here; the grant and spend behaviour
    /// lives in ComboPowerGrantTests, and the shield's effect on hearts in the Play Mode fixture.
    /// </summary>
    [TestFixture]
    public class ComboPowerResolverTests
    {
        [Test]
        public void ForTier_TierOne_GrantsNoPower()
        {
            Assert.That(ComboPowerResolver.ForTier(1), Is.EqualTo(ComboPower.None),
                "Criterion 1: tier 1 grants no combo power after five correct traces.");
        }

        [Test]
        public void ForTier_TierTwo_GrantsRapidShot()
        {
            Assert.That(ComboPowerResolver.ForTier(2), Is.EqualTo(ComboPower.RapidShot));
        }

        [Test]
        public void ForTier_TiersThreeAndFour_GrantPiercingArrow()
        {
            Assert.That(ComboPowerResolver.ForTier(3), Is.EqualTo(ComboPower.PiercingArrow));
            Assert.That(ComboPowerResolver.ForTier(4), Is.EqualTo(ComboPower.PiercingArrow),
                "Criterion 3 covers tiers 3 and 4 together.");
        }

        [Test]
        public void ForTier_TierFive_GrantsShield()
        {
            Assert.That(ComboPowerResolver.ForTier(5), Is.EqualTo(ComboPower.Shield));
        }

        // The mapping has to be total. An unauthored tier reaching this mid-combat must degrade to
        // "no power" rather than throw, because the alternative is an exception during a wave.
        [Test]
        public void ForTier_TiersOutsideTheAuthoredRange_DegradeToNone()
        {
            foreach (int tier in new[] { int.MinValue, -1, 0, 6, 99, int.MaxValue })
                Assert.That(ComboPowerResolver.ForTier(tier), Is.EqualTo(ComboPower.None),
                    $"Tier {tier} is outside the authored range and must grant nothing.");
        }

        [Test]
        public void EveryAuthoredTier_ResolvesToADefinedPower()
        {
            for (int tier = ChallengeTierPolicy.MinTier; tier <= ChallengeTierPolicy.MaxTier; tier++)
            {
                Assert.That(ChallengeTierPolicy.IsDefinedTier(tier), Is.True, "Setup: authored range.");
                Assert.That(System.Enum.IsDefined(typeof(ComboPower), ComboPowerResolver.ForTier(tier)),
                    Is.True, $"Tier {tier} must map to a defined ComboPower.");
            }
        }

        // Only tier 1 is specified as granting nothing. If a later edit made another authored tier
        // fall through to None, that would silently remove a reward the criteria promise.
        [Test]
        public void TierOne_IsTheOnlyAuthoredTierGrantingNothing()
        {
            for (int tier = ChallengeTierPolicy.MinTier; tier <= ChallengeTierPolicy.MaxTier; tier++)
            {
                ComboPower power = ComboPowerResolver.ForTier(tier);
                if (tier == 1)
                    Assert.That(power, Is.EqualTo(ComboPower.None));
                else
                    Assert.That(power, Is.Not.EqualTo(ComboPower.None),
                        $"Authored tier {tier} silently grants nothing.");
            }
        }
    }
}
