using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Data
{
    public class CampaignConfigValidatorTests
    {
        [Test]
        public void ValidFixture_HasNoErrorIssues()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();

            IReadOnlyList<ContentValidationIssue> issues = CampaignConfigValidator.Validate(fixture.Campaign);

            Assert.IsNotNull(issues);
            Assert.IsEmpty(issues, Describe(issues));
        }

        [Test]
        public void Validate_ReportsManifestAndTopologyIssues()
        {
            AssertMutation("MANIFEST_MISSING", fixture => fixture.Campaign.manifest = null);
            AssertMutation("MANIFEST_UNSUPPORTED", fixture => fixture.Campaign.manifest.contentSchemaVersion = 2);
            AssertMutation("WORKBOOK_HASH_MISMATCH", fixture => fixture.Campaign.manifest.sourceWorkbookSha256 = "wrong");
            AssertMutation("CAMPAIGN_ID_INVALID", fixture => fixture.Campaign.manifest.campaignId = "Campaign.Revised");
            AssertMutation("ERA_COUNT_INVALID", fixture => fixture.Campaign.eras.RemoveAt(0));
            AssertMutation("ERA_ID_INVALID", fixture => fixture.Campaign.eras[0].stableId = "era.invalid");
            AssertMutation("ERA_ORDER_INVALID", fixture => fixture.Campaign.eras[0].order = 2);
            AssertMutation("LEVEL_COUNT_INVALID", fixture => fixture.Campaign.eras[0].levels.RemoveAt(0));
            AssertMutation("LEVEL_ID_INVALID", fixture => fixture.Campaign.eras[0].levels[0].stableId = "level.invalid.01");
            AssertMutation("LEVEL_ORDER_INVALID", fixture => fixture.Campaign.eras[0].levels[0].levelNumber = 2);
            AssertMutation("FOCUS_SLOT_COUNT_INVALID", fixture => fixture.Campaign.eras[0].levels[0].focusWords.RemoveAt(0));
            AssertMutation("DUPLICATE_ID", fixture => fixture.Campaign.eras[1].stableId = "era.ugat");
        }

        [Test]
        public void Validate_ReportsSymbolAndContentIssues()
        {
            AssertMutation("SYMBOL_COUNT_INVALID", fixture => fixture.Campaign.symbols.RemoveAt(0));
            AssertMutation("SYMBOL_ID_INVALID", fixture => fixture.Campaign.symbols[0].stableId = "symbol.invalid");
            AssertMutation("SPOKEN_VALUE_COUNT_INVALID", fixture => fixture.Campaign.symbols[0].spokenValues.Clear());
            AssertMutation("SPOKEN_VALUE_UNKNOWN", fixture =>
                fixture.Campaign.eras[0].levels[0].focusWords[0].decomposition[0].spokenValueId = "value.unknown");
            AssertMutation("DARA_VISUAL_IDENTITY_INVALID", fixture =>
                FindSymbol(fixture, "symbol.dara").spokenValues.RemoveAt(1));
            AssertMutation("FOCUS_DECOMPOSITION_EMPTY", fixture =>
                fixture.Campaign.eras[0].levels[0].focusWords[0].decomposition.Clear());
            AssertMutation("FOCUS_DECOMPOSITION_INVALID", fixture =>
                fixture.Campaign.eras[0].levels[0].focusWords[0].decomposition[0].symbol = null);
            AssertMutation("KUDLIT_UNSUPPORTED", fixture =>
                fixture.Campaign.eras[0].levels[0].focusWords[0].decomposition[0].spokenValueId = "value.kudlit.modified");
            AssertMutation("CUMULATIVE_POOL_INVALID", fixture =>
                fixture.Campaign.eras[0].levels[0].cumulativeSymbolPool.RemoveAt(0));
            AssertMutation("FINAL_RESTORATION_INVALID", fixture =>
                fixture.Campaign.eras[0].levels[0].finalRestorationValue = null);
            AssertMutation("PA_INSTRUCTION_ORDER_INVALID", fixture =>
                FindLevel(fixture, "level.pamana.05").learningRequirements.RemoveAt(1));
            AssertMutation("REQUIRED_MEDIA_MISSING", fixture =>
                fixture.Campaign.eras[0].levels[0].focusWords[0].media.contextImage = null);
            AssertMutation("REQUIRED_REFERENCE_MISSING", fixture =>
                fixture.Campaign.eras[0].storyReference = null);
            AssertMutation("LEGACY_ERA_IDENTITY_ACTIVE", fixture => fixture.Campaign.eras[0].stableId = "Spanish");
        }

        [Test]
        public void Validate_ReportsSymbolIntroductionAndPoolMembershipIssues()
        {
            AssertMutation(ContentValidationCode.SymbolIntroductionLevelInvalid, fixture =>
                FindSymbol(fixture, "symbol.ba").firstIntroductionLevelId = "level.invalid.01");
            AssertMutation(ContentValidationCode.CumulativePoolInvalid, fixture =>
                FindSymbol(fixture, "symbol.ba").firstIntroductionLevelId = "level.ugat.01");
            AssertMutation(ContentValidationCode.SymbolNotIntroduced, fixture =>
                fixture.Campaign.eras[0].levels[0].focusWords[0].decomposition[0] =
                    ReferenceTo(fixture, "symbol.pa", "value.pa"));
            AssertMutation(ContentValidationCode.SymbolNotIntroduced, fixture =>
                fixture.Campaign.eras[0].levels[0].learningRequirements[0].symbolValue =
                    ReferenceTo(fixture, "symbol.pa", "value.pa"));
        }

        [Test]
        public void Validate_RejectsReferenceToOrphanAssetWithCatalogStableId()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            BaybayinCharacterSO orphan = UnityEngine.ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            try
            {
                orphan.stableId = "symbol.a";
                orphan.firstIntroductionLevelId = "level.ugat.01";
                orphan.spokenValues = new List<SpokenValueDefinition>
                {
                    new SpokenValueDefinition { stableId = "value.a", displayValue = "A" },
                };
                fixture.Campaign.eras[0].levels[0].focusWords[0].decomposition[0] =
                    new SymbolValueReference { symbol = orphan, spokenValueId = "value.a" };

                IReadOnlyList<ContentValidationIssue> issues =
                    CampaignConfigValidator.Validate(fixture.Campaign);

                Assert.That(issues, Has.Some.Matches<ContentValidationIssue>(
                    issue => issue.Code == ContentValidationCode.FocusDecompositionInvalid),
                    Describe(issues));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(orphan);
            }
        }

        [Test]
        public void Validate_ReportsPaInstructionAfterOrderedLearningExposure()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO finale = FindLevel(fixture, "level.pamana.05");
            finale.learningRequirements.Insert(0, new ContentRequirement
            {
                kind = ContentRequirementKind.Practice,
                requiredSuccesses = 1,
                symbolValue = ReferenceTo(fixture, "symbol.pa", "value.pa"),
            });

            IReadOnlyList<ContentValidationIssue> issues = CampaignConfigValidator.Validate(fixture.Campaign);

            Assert.That(issues, Has.Some.Matches<ContentValidationIssue>(
                issue => issue.Code == ContentValidationCode.PaInstructionOrderInvalid),
                Describe(issues));
        }

        [Test]
        public void Validate_NullMediaContainerReportsOneRootIssue()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            fixture.Campaign.eras[0].levels[0].focusWords[0].media = null;

            IReadOnlyList<ContentValidationIssue> issues = CampaignConfigValidator.Validate(fixture.Campaign);
            ContentValidationIssue[] mediaIssues = issues
                .Where(issue => issue.Path.EndsWith(".focusWords[0].media", StringComparison.Ordinal))
                .ToArray();

            Assert.AreEqual(1, mediaIssues.Length, Describe(mediaIssues));
            Assert.AreEqual(ContentValidationCode.RequiredMediaMissing, mediaIssues[0].Code);
        }

        [Test]
        public void Validate_EmptyRequirementListReportsRequirementInvalid()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            fixture.Campaign.eras[0].levels[0].learningRequirements.Clear();

            IReadOnlyList<ContentValidationIssue> issues = CampaignConfigValidator.Validate(fixture.Campaign);
            ContentValidationIssue[] requirementIssues = issues
                .Where(issue => issue.Path.EndsWith(".learningRequirements", StringComparison.Ordinal))
                .ToArray();

            Assert.That(requirementIssues, Has.Some.Matches<ContentValidationIssue>(
                issue => issue.Code == ContentValidationCode.RequirementInvalid), Describe(requirementIssues));
            Assert.That(requirementIssues, Has.None.Matches<ContentValidationIssue>(
                issue => issue.Code == ContentValidationCode.RequiredReferenceMissing), Describe(requirementIssues));
        }

        [Test]
        public void Validate_DisabledChallengeWithoutSequence_HasNoChallengeIssue()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO level = fixture.Campaign.eras[0].levels[0];
            level.challengePrototypeEnabled = false;
            level.challengeSequence = null;

            IReadOnlyList<ContentValidationIssue> issues = CampaignConfigValidator.Validate(fixture.Campaign);

            Assert.That(ChallengeIssues(issues), Is.Empty, Describe(issues));
        }

        [Test]
        public void Validate_DisabledChallengeDoesNotInspectDormantSequence()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO level = fixture.Campaign.eras[0].levels[0];
            ChallengeSequenceSO sequence = fixture.CreateValidChallengeSequence();
            sequence.units = Array.Empty<ChallengeUnitDefinition>();
            level.challengePrototypeEnabled = false;
            level.challengeSequence = sequence;

            IReadOnlyList<ContentValidationIssue> issues = CampaignConfigValidator.Validate(fixture.Campaign);

            Assert.That(ChallengeIssues(issues), Is.Empty, Describe(issues));
        }

        [Test]
        public void Validate_EnabledChallengeWithoutSequence_ReportsMissingReference()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO level = fixture.Campaign.eras[0].levels[0];
            level.challengePrototypeEnabled = true;
            level.challengeSequence = null;

            ContentValidationIssue[] issues = ChallengeIssues(
                CampaignConfigValidator.Validate(fixture.Campaign));

            Assert.AreEqual(1, issues.Length, Describe(issues));
            Assert.AreEqual(ContentValidationCode.ChallengeSequenceMissing, issues[0].Code);
            Assert.AreEqual("campaign.revised-v1.eras[0].levels[0].challengeSequence", issues[0].Path);
            Assert.AreSame(level, issues[0].Context);
        }

        [Test]
        public void Validate_EnabledValidChallenge_HasNoChallengeIssue()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO level = fixture.Campaign.eras[0].levels[0];
            level.challengePrototypeEnabled = true;
            level.challengeSequence = fixture.CreateValidChallengeSequence();

            IReadOnlyList<ContentValidationIssue> issues = CampaignConfigValidator.Validate(fixture.Campaign);

            Assert.That(ChallengeIssues(issues), Is.Empty, Describe(issues));
        }

        [Test]
        public void Validate_EnabledInvalidChallenge_AdaptsEveryDiagnosticInOrder()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO level = fixture.Campaign.eras[0].levels[0];
            ChallengeSequenceSO sequence = fixture.CreateValidChallengeSequence();
            sequence.units = new[]
            {
                new ChallengeUnitDefinition
                {
                    unitId = string.Empty,
                    mode = ChallengeMode.WordPlacement,
                    maxErrors = 0,
                    tokens = Array.Empty<ChallengeTokenDefinition>(),
                    slots = Array.Empty<ChallengeSlotDefinition>(),
                    candidateOccurrenceIds = Array.Empty<string>(),
                },
            };
            level.challengePrototypeEnabled = true;
            level.challengeSequence = sequence;
            ChallengeValidationResult source = ChallengeSequenceValidator.Validate(sequence);

            ContentValidationIssue[] issues = ChallengeIssues(
                CampaignConfigValidator.Validate(fixture.Campaign));

            Assert.That(source.Errors.Count, Is.GreaterThan(1));
            Assert.AreEqual(source.Errors.Count, issues.Length, Describe(issues));
            for (int index = 0; index < issues.Length; index++)
            {
                Assert.AreEqual(ContentValidationCode.ChallengeSequenceInvalid, issues[index].Code);
                Assert.AreEqual("campaign.revised-v1.eras[0].levels[0].challengeSequence", issues[index].Path);
                Assert.AreSame(sequence, issues[index].Context);
                StringAssert.Contains(source.Errors[index], issues[index].Message);
            }
        }

        [Test]
        public void Validate_ChallengeTraversalDoesNotMutateSequence()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            LevelConfigSO level = fixture.Campaign.eras[0].levels[0];
            ChallengeSequenceSO sequence = fixture.CreateValidChallengeSequence();
            level.challengePrototypeEnabled = true;
            level.challengeSequence = sequence;
            string beforeLevel = UnityEngine.JsonUtility.ToJson(level);
            string beforeSequence = UnityEngine.JsonUtility.ToJson(sequence);

            CampaignConfigValidator.Validate(fixture.Campaign);

            Assert.AreEqual(beforeLevel, UnityEngine.JsonUtility.ToJson(level));
            Assert.AreEqual(beforeSequence, UnityEngine.JsonUtility.ToJson(sequence));
        }

        [Test]
        public void Validate_DoesNotMutateCampaignOrReferencedObjects()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            string before = UnityEngine.JsonUtility.ToJson(fixture.Campaign);
            string[] beforeEras = fixture.Campaign.eras.Select(UnityEngine.JsonUtility.ToJson).ToArray();
            string[] beforeLevels = fixture.Campaign.eras
                .SelectMany(era => era.levels)
                .Select(UnityEngine.JsonUtility.ToJson)
                .ToArray();
            string[] beforeSymbols = fixture.Campaign.symbols
                .Select(UnityEngine.JsonUtility.ToJson)
                .ToArray();

            CampaignConfigValidator.Validate(fixture.Campaign);

            Assert.AreEqual(before, UnityEngine.JsonUtility.ToJson(fixture.Campaign));
            CollectionAssert.AreEqual(beforeEras, fixture.Campaign.eras.Select(UnityEngine.JsonUtility.ToJson));
            CollectionAssert.AreEqual(beforeLevels, fixture.Campaign.eras
                .SelectMany(era => era.levels)
                .Select(UnityEngine.JsonUtility.ToJson));
            CollectionAssert.AreEqual(beforeSymbols, fixture.Campaign.symbols
                .Select(UnityEngine.JsonUtility.ToJson));
        }

        [Test]
        public void Validate_DiagnosticsHaveCanonicalPathsMessagesAndContext()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            fixture.Campaign.eras[0].stableId = "era.invalid";

            IReadOnlyList<ContentValidationIssue> issues = CampaignConfigValidator.Validate(fixture.Campaign);

            Assert.IsNotEmpty(issues);
            foreach (ContentValidationIssue issue in issues)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(issue.Code));
                StringAssert.StartsWith("campaign.revised-v1", issue.Path);
                Assert.IsFalse(string.IsNullOrWhiteSpace(issue.Message));
                if (issue.Code == ContentValidationCode.EraIdInvalid)
                    Assert.IsNotNull(issue.Context);
            }
        }

        private static void AssertMutation(string expectedCode, Action<CampaignTestFixture> mutation)
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            mutation(fixture);

            IReadOnlyList<ContentValidationIssue> issues = CampaignConfigValidator.Validate(fixture.Campaign);

            Assert.That(issues, Has.Some.Matches<ContentValidationIssue>(
                issue => issue.Code == expectedCode),
                expectedCode + " was not emitted. Actual: " + Describe(issues));
        }

        private static BaybayinCharacterSO FindSymbol(CampaignTestFixture fixture, string stableId)
        {
            return fixture.Campaign.symbols.First(symbol => symbol.stableId == stableId);
        }

        private static LevelConfigSO FindLevel(CampaignTestFixture fixture, string stableId)
        {
            return fixture.Campaign.eras
                .SelectMany(era => era.levels)
                .First(level => level.stableId == stableId);
        }

        private static SymbolValueReference ReferenceTo(
            CampaignTestFixture fixture,
            string symbolId,
            string spokenValueId)
        {
            return new SymbolValueReference
            {
                symbol = FindSymbol(fixture, symbolId),
                spokenValueId = spokenValueId,
            };
        }

        private static string Describe(IEnumerable<ContentValidationIssue> issues)
        {
            return string.Join("; ", issues.Select(issue => issue.Code + " @ " + issue.Path));
        }

        private static ContentValidationIssue[] ChallengeIssues(
            IEnumerable<ContentValidationIssue> issues)
        {
            return issues.Where(issue =>
                issue.Code == ContentValidationCode.ChallengeSequenceMissing ||
                issue.Code == ContentValidationCode.ChallengeSequenceInvalid).ToArray();
        }
    }
}
