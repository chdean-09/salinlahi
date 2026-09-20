using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.QA
{
    [TestFixture]
    public sealed class QaStrokeSampleLibraryTests
    {
        private static readonly string[] ExpectedIds = { "A", "EI", "BA", "MA", "NA", "TA" };

        [Test]
        public void ResolvePath_IsCaseInsensitive_AndUsesRecordedHumanFixtures()
        {
            for (int i = 0; i < ExpectedIds.Length; i++)
            {
                string requested = ExpectedIds[i].ToLowerInvariant();
                Assert.IsTrue(QaStrokeSampleLibrary.TryResolveAssetPath(requested, out string path),
                    $"Expected a replay fixture for {ExpectedIds[i]}.");
                Assert.That(path, Does.StartWith("Assets/Tests/Fixtures/TestDraws/"));
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(path),
                    $"Replay fixture path should resolve to a TextAsset: {path}");
            }
        }

        [Test]
        public void LoadStrokes_ParsesEverySupportedFixture()
        {
            for (int i = 0; i < ExpectedIds.Length; i++)
            {
                Assert.IsTrue(QaStrokeSampleLibrary.TryLoadStrokes(
                    ExpectedIds[i], out List<List<UnityEngine.Vector2>> strokes, out string error),
                    error);
                Assert.IsNotEmpty(strokes);
                Assert.GreaterOrEqual(StrokeTextParser.FlattenStrokes(strokes).Count, 8,
                    $"{ExpectedIds[i]} fixture should contain a recorded draw, not an empty sample.");
            }
        }

        [Test]
        public void RecordedFixtures_RecognizeAsTheirMappedSymbols()
        {
            var recognizer = new DollarPRecognizer(32);
            recognizer.SetTemplateStrokeVariants(new TemplateLoader().LoadAll());

            for (int i = 0; i < ExpectedIds.Length; i++)
            {
                string expected = ExpectedIds[i];
                Assert.IsTrue(QaStrokeSampleLibrary.TryLoadStrokes(
                    expected, out List<List<UnityEngine.Vector2>> strokes, out string error), error);

                RecognitionResult result = recognizer.Recognize(strokes);
                Assert.AreEqual(expected, BaybayinIdCanonicalizer.Canonicalize(result.characterID),
                    $"Recorded QA fixture {expected} should resolve to {expected}.");
            }
        }

        [Test]
        public void ResolvePath_RejectsUnsupportedSymbolsClearly()
        {
            Assert.IsFalse(QaStrokeSampleLibrary.TryResolveAssetPath("MISSING", out string path));
            Assert.IsNull(path);
        }

        [Test]
        public void BuildMissSample_UsesNormalRecognitionFailurePath()
        {
            List<List<UnityEngine.Vector2>> strokes = QaStrokeSampleLibrary.BuildMissSample();
            Assert.IsFalse(StrokeValidation.IsRecognitionDegenerate(strokes));

            var recognizer = new DollarPRecognizer(32);
            recognizer.SetTemplateStrokeVariants(new TemplateLoader().LoadAll());
            RecognitionResult result = recognizer.Recognize(strokes);

            Assert.Less(result.score, 0.45f,
                "The replay miss must stay below the campaign acceptance threshold so it "
                + "exercises RecognitionManager's normal failed-recognition path.");
        }
    }
}
