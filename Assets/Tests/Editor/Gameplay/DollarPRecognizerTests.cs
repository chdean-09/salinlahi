using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class DollarPRecognizerTests
    {
        [Test]
        public void Recognize_UsesDifferentCharacterForSecondBest_WhenBestCharacterHasMultipleVariants()
        {
            var recognizer = new DollarPRecognizer(32);
            var templates = new Dictionary<string, List<List<List<Vector2>>>>
            {
                ["BA"] = new List<List<List<Vector2>>>
                {
                    new List<List<Vector2>> { CreateStroke(0f, 0f, 0f, 1f) },
                    new List<List<Vector2>> { CreateStroke(0.1f, 0f, 0.1f, 1f) }
                },
                ["KA"] = new List<List<List<Vector2>>>
                {
                    new List<List<Vector2>> { CreateStroke(0f, 0f, 1f, 0f) }
                }
            };

            recognizer.SetTemplateStrokeVariants(templates);

            RecognitionResult result = recognizer.Recognize(new List<List<Vector2>>
            {
                CreateStroke(0.1f, 0f, 0.1f, 1f)
            });

            Assert.AreEqual("BA", result.characterID);
            Assert.AreEqual(2, result.templateVariantIndex);
            Assert.AreEqual("KA", result.secondBestID);
            Assert.AreNotEqual(result.characterID, result.secondBestID);
        }

        [Test]
        public void Recognize_PrefersTemplateWithMatchingStrokeCount_WhenShapesAreEquivalent()
        {
            var recognizer = new DollarPRecognizer(32);
            var templates = new Dictionary<string, List<List<List<Vector2>>>>
            {
                // SINGLE: one continuous stroke along the same horizontal path.
                ["SINGLE"] = new List<List<List<Vector2>>>
                {
                    new List<List<Vector2>> { CreateStroke(0f, 0f, 1f, 0f) }
                },
                // DOUBLE: two strokes covering the same path with a lift in the middle.
                // After preprocessing the point cloud is essentially identical to SINGLE,
                // so without a stroke-count penalty $P treats them as a tie.
                ["DOUBLE"] = new List<List<List<Vector2>>>
                {
                    new List<List<Vector2>>
                    {
                        CreateStroke(0f, 0f, 0.5f, 0f),
                        CreateStroke(0.5f, 0f, 1f, 0f)
                    }
                }
            };

            recognizer.SetTemplateStrokeVariants(templates);

            RecognitionResult oneStrokeResult = recognizer.Recognize(new List<List<Vector2>>
            {
                CreateStroke(0f, 0f, 1f, 0f)
            });
            Assert.AreEqual("SINGLE", oneStrokeResult.characterID,
                "One-stroke gesture should match the one-stroke template, not the two-stroke template with the same shape.");

            RecognitionResult twoStrokeResult = recognizer.Recognize(new List<List<Vector2>>
            {
                CreateStroke(0f, 0f, 0.5f, 0f),
                CreateStroke(0.5f, 0f, 1f, 0f)
            });
            Assert.AreEqual("DOUBLE", twoStrokeResult.characterID,
                "Two-stroke gesture should match the two-stroke template when shapes are equivalent.");
        }

        [Test]
        public void Recognize_PrefersTemplateWithMatchingAspectRatio_WhenStrokeCountsTie()
        {
            var recognizer = new DollarPRecognizer(32);
            // Both templates are single-stroke. Anisotropic ScaleToSquare would erase
            // their aspect-ratio difference and leave it to greedy point matching alone.
            // The aspect-ratio penalty must rescue the right answer.
            var templates = new Dictionary<string, List<List<List<Vector2>>>>
            {
                // WIDE: long thin horizontal stroke (HA-like aspect ~10).
                ["WIDE"] = new List<List<List<Vector2>>>
                {
                    new List<List<Vector2>>
                    {
                        new List<Vector2>
                        {
                            new Vector2(0f, 0f),
                            new Vector2(2.5f, 0.1f),
                            new Vector2(5f, 0f),
                            new Vector2(7.5f, 0.1f),
                            new Vector2(10f, 0f)
                        }
                    }
                },
                // SQUARE: stroke that fills a moderate-aspect box (SA-like aspect ~2).
                ["SQUARE"] = new List<List<List<Vector2>>>
                {
                    new List<List<Vector2>>
                    {
                        new List<Vector2>
                        {
                            new Vector2(0f, 0f),
                            new Vector2(2.5f, 2.5f),
                            new Vector2(5f, 0f),
                            new Vector2(7.5f, 2.5f),
                            new Vector2(10f, 0f)
                        }
                    }
                }
            };

            recognizer.SetTemplateStrokeVariants(templates);

            // User draws a wide thin gesture. Without the aspect-ratio penalty,
            // anisotropic scaling normalizes both to a 1x1 square and SQUARE can win.
            RecognitionResult wideResult = recognizer.Recognize(new List<List<Vector2>>
            {
                new List<Vector2>
                {
                    new Vector2(0f, 0f),
                    new Vector2(2.5f, 0.05f),
                    new Vector2(5f, 0f),
                    new Vector2(7.5f, 0.05f),
                    new Vector2(10f, 0f)
                }
            });
            Assert.AreEqual("WIDE", wideResult.characterID,
                "A thin horizontal gesture should not match a square-aspect template even when stroke count agrees.");
        }

        [TestCase("KA_draw_01", "KA")]
        [TestCase("RA_draw_01", "RA")]
        [TestCase("RA_draw_02", "RA")]
        [TestCase("RA_draw_03", "RA")]
        [TestCase("HA_draw_01", "HA")]
        public void Recognize_ResourceDrawRegression_ReturnsExpectedCharacter(string drawAssetName, string expectedCharacter)
        {
            var recognizer = new DollarPRecognizer(32);
            var templates = new TemplateLoader().LoadAll();
            recognizer.SetTemplateStrokeVariants(templates);

            TextAsset drawAsset = Resources.Load<TextAsset>($"TestDraws/{drawAssetName}");
            Assert.IsNotNull(drawAsset, $"Missing Resources/TestDraws/{drawAssetName}.txt");

            List<List<Vector2>> strokes = StrokeTextParser.ParseStrokes(drawAsset.text);
            RecognitionResult result = recognizer.Recognize(strokes);

            Assert.AreEqual(expectedCharacter, result.characterID);
        }

        private static List<Vector2> CreateStroke(float x0, float y0, float x1, float y1)
        {
            return new List<Vector2>
            {
                new Vector2(x0, y0),
                new Vector2(x1, y1)
            };
        }
    }
}
