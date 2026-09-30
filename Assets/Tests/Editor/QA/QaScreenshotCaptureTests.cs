using System;
using System.IO;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.QA
{
    [TestFixture]
    public sealed class QaScreenshotCaptureTests
    {
        private string _directory;

        [SetUp]
        public void CreateDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "SalinlahiQaCapture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void DeleteDirectory()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        [Test]
        public void ResolveFileName_MatchesTheCommittedCaptureNames()
        {
            Assert.AreEqual("level01-baseline.png",
                QaScreenshotCapture.ResolveFileName(_directory, 1, "baseline"));
            Assert.AreEqual("level15-issue.png",
                QaScreenshotCapture.ResolveFileName(_directory, 15, "issue"));
        }

        [Test]
        public void ResolveFileName_NumbersARepeat_InsteadOfOverwritingEvidence()
        {
            File.WriteAllText(Path.Combine(_directory, "level06-baseline.png"), string.Empty);
            Assert.AreEqual("level06-baseline-2.png",
                QaScreenshotCapture.ResolveFileName(_directory, 6, "baseline"));

            File.WriteAllText(Path.Combine(_directory, "level06-baseline-2.png"), string.Empty);
            Assert.AreEqual("level06-baseline-3.png",
                QaScreenshotCapture.ResolveFileName(_directory, 6, "baseline"));
        }

        [TestCase("gapos-intro-overlap", "gapos-intro-overlap")]
        [TestCase("Gapos Intro / Overlap!", "gapos-intro-overlap")]
        [TestCase("  --story panel--  ", "story-panel")]
        [TestCase("../../escape", "escape")]
        [TestCase("", "capture")]
        [TestCase("   ", "capture")]
        [TestCase(null, "capture")]
        public void SanitizeSlug_AlwaysYieldsASafeFileName(string slug, string expected)
        {
            Assert.AreEqual(expected, QaScreenshotCapture.SanitizeSlug(slug));
        }
    }
}
