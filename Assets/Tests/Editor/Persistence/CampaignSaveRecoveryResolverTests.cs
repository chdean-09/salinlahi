using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignSaveRecoveryResolverTests
    {
        [TestCase(5, 4, RecoveryDecisionKind.UsePrimary)]
        [TestCase(4, 5, RecoveryDecisionKind.PromoteTemporary)]
        [TestCase(5, 5, RecoveryDecisionKind.UsePrimary)]
        public void Resolve_WhenPrimaryAndTemporaryAreValid_UsesRevisionThenPrimaryTieBreak(
            long primaryRevision, long temporaryRevision, RecoveryDecisionKind expected)
        {
            CandidateInspection primary = Valid(CampaignSaveFileRole.Primary, primaryRevision);
            CandidateInspection temporary = Valid(CampaignSaveFileRole.Temporary, temporaryRevision);

            RecoveryDecision decision = CampaignSaveRecoveryResolver.Resolve(
                primary, temporary, CandidateInspection.Missing(CampaignSaveFileRole.Backup));

            Assert.That(decision.Kind, Is.EqualTo(expected));
        }

        [Test]
        public void Resolve_WhenHigherSchemaExists_BlocksInsteadOfResetting()
        {
            RecoveryDecision decision = CampaignSaveRecoveryResolver.Resolve(
                new CandidateInspection
                {
                    Role = CampaignSaveFileRole.Primary,
                    Exists = true,
                    FailureCode = CampaignSaveFailureCode.UnsupportedSchema,
                },
                CandidateInspection.Missing(CampaignSaveFileRole.Temporary),
                CandidateInspection.Missing(CampaignSaveFileRole.Backup));

            Assert.That(decision.Kind, Is.EqualTo(RecoveryDecisionKind.Blocked));
        }

        private static CandidateInspection Valid(CampaignSaveFileRole role, long revision)
        {
            return new CandidateInspection
            {
                Role = role,
                Exists = true,
                FailureCode = CampaignSaveFailureCode.None,
                Document = new CampaignSaveDocument { revision = revision },
            };
        }
    }
}
