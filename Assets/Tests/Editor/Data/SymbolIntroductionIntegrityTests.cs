using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// docs/design/gated-finale-levels-2-4.md Section 4: LevelConfigSO.learningRequirements
    /// Instruction entries are the authored fact of which level introduces a symbol;
    /// BaybayinCharacterSO.firstIntroductionLevelId restates the same fact from the symbol's side,
    /// and nothing enforced that the two agree until CampaignConfigValidator gained
    /// SYMBOL_INTRODUCTION_INTEGRITY_INVALID. Covers all three sub-rules: exactly one introducer
    /// per symbol, firstIntroductionLevelId naming that introducer, and no cumulativeSymbolPool
    /// entry appearing before its symbol's real introduction.
    /// </summary>
    [TestFixture]
    public sealed class SymbolIntroductionIntegrityTests
    {
        private const string CampaignAssetPath =
            "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

        private static List<string> Offenders(CampaignConfigSO campaign)
        {
            return CampaignConfigValidator.Validate(campaign)
                .Where(issue => issue.Code == ContentValidationCode.SymbolIntroductionIntegrityInvalid)
                .Select(issue => $"{issue.Code} @ {issue.Path}: {issue.Message}")
                .ToList();
        }

        private static LevelConfigSO FindLevel(CampaignConfigSO campaign, string stableId)
        {
            return campaign.eras
                .Where(era => era?.levels != null)
                .SelectMany(era => era.levels)
                .FirstOrDefault(level => level != null && level.stableId == stableId);
        }

        private static BaybayinCharacterSO FindSymbol(CampaignConfigSO campaign, string stableId)
        {
            return campaign.symbols.First(symbol => symbol.stableId == stableId);
        }

        [Test]
        public void SymbolWithNoInstructionAnywhere_IsReportedAsZeroIntroducers()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();

            // level.ugat.03 authors exactly one learning requirement: the Instruction that
            // introduces BA. Clearing it leaves BA with no introducer anywhere in the campaign,
            // while its firstIntroductionLevelId metadata and its place in every later pool are
            // untouched -- isolating the "zero introducers" branch from the others.
            LevelConfigSO introducer = FindLevel(fixture.Campaign, "level.ugat.03");
            introducer.learningRequirements.Clear();

            List<string> offenders = Offenders(fixture.Campaign);
            Assert.IsTrue(offenders.Any(o =>
                    o.Contains("is introduced by no level") && o.Contains("symbol.ba")),
                "a symbol whose Instruction requirement was removed everywhere must be reported "
                + "as having zero introducers.\n" + string.Join("\n", offenders));
        }

        [Test]
        public void SymbolWithTwoInstructionLevels_IsReportedAsDuplicateIntroducer()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();

            // EI is legitimately introduced once, at level.ugat.02. Authoring a second Instruction
            // requirement for it on level.ugat.04 -- a level whose pool already carries EI, so no
            // SYMBOL_NOT_INTRODUCED side effect -- leaves firstIntroductionLevelId agreeing with
            // one of the two introducers (ugat.02 is still in the introducer list), isolating the
            // "introduced by more than one level" branch from the disagreement branch.
            LevelConfigSO duplicateIntroducer = FindLevel(fixture.Campaign, "level.ugat.04");
            BaybayinCharacterSO ei = FindSymbol(fixture.Campaign, "symbol.ei");
            duplicateIntroducer.learningRequirements.Add(new ContentRequirement
            {
                kind = ContentRequirementKind.Instruction,
                requiredSuccesses = 1,
                symbolValue = new SymbolValueReference { symbol = ei, spokenValueId = "value.ei" },
            });

            List<string> offenders = Offenders(fixture.Campaign);
            Assert.IsTrue(offenders.Any(o =>
                    o.Contains("is introduced by more than one level") &&
                    o.Contains("symbol.ei") &&
                    o.Contains("level.ugat.02") &&
                    o.Contains("level.ugat.04")),
                "a symbol named by an Instruction requirement on two levels must be reported as "
                + "having more than one introducer, naming both levels.\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void FirstIntroductionLevelId_DisagreeingWithInstructionLevel_IsReported()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();

            // NA's sole Instruction requirement stays on level.ugat.05 (its rightful introducer,
            // per GetIntroducedSymbolIds/CreateSymbols); only the symbol's own metadata is mutated
            // to point somewhere else. The introducer count for NA stays exactly one, isolating the
            // disagreement branch from the introducer-count branches.
            BaybayinCharacterSO na = FindSymbol(fixture.Campaign, "symbol.na");
            na.firstIntroductionLevelId = "level.ugat.01";

            List<string> offenders = Offenders(fixture.Campaign);
            Assert.IsTrue(offenders.Any(o =>
                    o.Contains("declares firstIntroductionLevelId 'level.ugat.01'") &&
                    o.Contains("symbol.na")),
                "a symbol whose firstIntroductionLevelId names a level that does not carry its "
                + "Instruction requirement must be reported.\n" + string.Join("\n", offenders));
        }

        [Test]
        public void PoolSymbolIntroducedByALaterLevel_IsReported()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();

            // TA is authored as introduced at level.ugnayan.01, appearing in every pool from
            // there on (including level.ugnayan.02's, still one level short of ugnayan.03).
            // Moving its sole Instruction requirement to level.ugnayan.03 leaves ugnayan.01 and
            // ugnayan.02's cumulativeSymbolPool entries for TA pointing at a symbol that, per the
            // authored Instruction data, is not actually taught until after them.
            LevelConfigSO originalIntroducer = FindLevel(fixture.Campaign, "level.ugnayan.01");
            LevelConfigSO laterIntroducer = FindLevel(fixture.Campaign, "level.ugnayan.03");
            BaybayinCharacterSO ta = FindSymbol(fixture.Campaign, "symbol.ta");

            originalIntroducer.learningRequirements.RemoveAll(requirement =>
                requirement.kind == ContentRequirementKind.Instruction &&
                requirement.symbolValue?.symbol == ta);
            laterIntroducer.learningRequirements.Add(new ContentRequirement
            {
                kind = ContentRequirementKind.Instruction,
                requiredSuccesses = 1,
                symbolValue = new SymbolValueReference { symbol = ta, spokenValueId = "value.ta" },
            });

            List<string> offenders = Offenders(fixture.Campaign);
            Assert.IsTrue(offenders.Any(o =>
                    o.Contains("Cumulative symbol pool includes 'symbol.ta'") &&
                    o.Contains(".eras[1].levels[0]")),
                "level.ugnayan.01's pool still lists TA, but TA's Instruction requirement now "
                + "lives on a later level, so the pool entry must be reported.\n"
                + string.Join("\n", offenders));
            Assert.IsTrue(offenders.Any(o =>
                    o.Contains("Cumulative symbol pool includes 'symbol.ta'") &&
                    o.Contains(".eras[1].levels[1]")),
                "level.ugnayan.02's pool must be reported too, for the same reason.\n"
                + string.Join("\n", offenders));
        }

        /// <summary>
        /// The known live defect recorded in docs/design/gated-finale-levels-2-4.md Section 4:
        /// Char_RA declares firstIntroductionLevelId level.pamana.03, but no shipped level carries
        /// an Instruction requirement for RA anywhere, while RA sits in the cumulativeSymbolPool of
        /// Levels 13-15. Pinned to RA specifically -- an Assert.IsEmpty would be wrong here (RA is
        /// a real, known, un-fixed content gap) and an unpinned "some offenders exist" assertion
        /// would be too weak to catch a NEW drift landing somewhere else in the campaign.
        /// </summary>
        [Test]
        public void ShippedCampaign_OnlyOffenderIsRa()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignAssetPath);
            Assert.IsNotNull(campaign, $"Expected the authored campaign at {CampaignAssetPath}.");

            List<string> offenders = Offenders(campaign);
            Assert.IsNotEmpty(offenders,
                "the shipped campaign is expected to report the known RA introduction gap; an "
                + "empty result here means the gap was fixed (update this test) or the check "
                + "regressed silently.");

            var offendingSymbolIds = new SortedSet<string>(System.StringComparer.Ordinal);
            foreach (string offender in offenders)
            {
                foreach (Match match in Regex.Matches(offender, "symbol\\.[a-z]+"))
                    offendingSymbolIds.Add(match.Value);
            }

            CollectionAssert.AreEqual(new[] { "symbol.ra" }, offendingSymbolIds,
                "the shipped campaign's symbol-introduction-integrity offenders drifted from the "
                + "known RA-only gap. If this is a NEW, real content drift, that is exactly what "
                + "this check exists to catch -- do not widen this assertion without fixing the "
                + "content or confirming the new offender is understood. Actual:\n"
                + string.Join("\n", offenders));
        }
    }
}
