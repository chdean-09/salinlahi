using System.IO;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class StrokeCaptureSourceTests
    {
        [Test]
        public void StrokeCapture_UsesTouchHistoryAndImmediateBeginPoint()
        {
            string source = File.ReadAllText("Assets/Scripts/Gameplay/Recognition/StrokeCapture.cs");

            StringAssert.Contains("finger.touchHistory", source);
            StringAssert.Contains("_canvas.AddPoint(startPosition)", source);
            StringAssert.Contains("ProcessTouchHistory", source);
            StringAssert.DoesNotContain("finger.index != 0", source);
        }
    }
}
