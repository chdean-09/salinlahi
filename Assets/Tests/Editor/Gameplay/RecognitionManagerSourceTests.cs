using System.IO;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class RecognitionManagerSourceTests
    {
        [Test]
        public void RecognitionManager_UsesDegenerateValidationInsteadOfMinimumPointGate()
        {
            string source = File.ReadAllText("Assets/Scripts/Core/RecognitionManager.cs");

            StringAssert.Contains("StrokeValidation.IsRecognitionDegenerate(strokes)", source);
            StringAssert.DoesNotContain("pointCount < _config.minimumPointCount", source);
        }
    }
}
