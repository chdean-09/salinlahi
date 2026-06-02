using System.IO;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class RecognitionConfigAssetTests
    {
        [Test]
        public void DefaultRecognitionConfig_SerializesMobileStrokeThresholds()
        {
            string yaml = File.ReadAllText("Assets/ScriptableObjects/RecognitionConfig_Default.asset");

            StringAssert.Contains("rawSampleMinDistancePixels: 2", yaml);
            StringAssert.Contains("visualSampleSpacingPixels: 8", yaml);
            StringAssert.Contains("maxVisualSamplesPerSegment: 24", yaml);
            StringAssert.Contains("minimumStrokePathLengthPixels: 40", yaml);
            StringAssert.Contains("minimumStrokeBoundsPixels: 12", yaml);
            StringAssert.DoesNotContain("minimumPointCount", yaml);
        }
    }
}
