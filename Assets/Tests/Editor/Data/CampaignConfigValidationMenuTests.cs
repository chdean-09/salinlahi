using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    public class CampaignConfigValidationMenuTests
    {
        [Test]
        public void Validate_ReturnsIssuesFromPureValidator()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            fixture.Campaign.eras.Clear();

            IReadOnlyList<ContentValidationIssue> issues =
                CampaignConfigValidationMenu.Validate(fixture.Campaign);

            Assert.That(issues, Has.Some.Matches<ContentValidationIssue>(
                issue => issue.Code == ContentValidationCode.EraCountInvalid));
        }

        [Test]
        public void Validate_DoesNotChangeSelectionOrCampaign()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            UnityEngine.Object selectedBefore = Selection.activeObject;
            string campaignBefore = UnityEngine.JsonUtility.ToJson(fixture.Campaign);

            CampaignConfigValidationMenu.Validate(fixture.Campaign);

            Assert.AreSame(selectedBefore, Selection.activeObject);
            Assert.AreEqual(campaignBefore, UnityEngine.JsonUtility.ToJson(fixture.Campaign));
        }
    }
}
