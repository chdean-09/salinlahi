using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
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

        // SALIN-217 (ruling Q2 / OQ-6): the RA draws expect RA again. RA_template_01..05 no longer
        // load under "DA", so an RA-shaped stroke resolves to the symbol.ra identity that now sits
        // in the campaign catalog.
        //
        // DA_draw_01 is asserted alongside them deliberately. Removing the fold reverses SALIN-212
        // on evidence measured in one direction only: commit 935f2392 recorded RA templates scoring
        // 0.756-0.839 against DA with the RA key absent, but nobody measured whether DA draws leak
        // into RA once 12 DA templates compete against 5 RA templates. This case is that
        // measurement. If it starts returning "RA", the unfold has cost DA recognition and the fix
        // is template curation in its own ticket, not a tweak here.
        [TestCase("KA_draw_01", "KA")]
        [TestCase("DA_draw_01", "DA")]
        [TestCase("RA_draw_01", "RA")]
        [TestCase("RA_draw_02", "RA")]
        [TestCase("RA_draw_03", "RA")]
        [TestCase("HA_draw_01", "HA")]
        public void Recognize_ResourceDrawRegression_ReturnsExpectedCharacter(string drawAssetName, string expectedCharacter)
        {
            var recognizer = new DollarPRecognizer(32);
            var templates = new TemplateLoader().LoadAll();
            recognizer.SetTemplateStrokeVariants(templates);

            string fixturePath = $"Assets/Tests/Fixtures/TestDraws/{drawAssetName}.txt";
            TextAsset drawAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(fixturePath);
            Assert.IsNotNull(drawAsset, $"Missing {fixturePath}");

            List<List<Vector2>> strokes = StrokeTextParser.ParseStrokes(drawAsset.text);
            RecognitionResult result = recognizer.Recognize(strokes);

            Assert.AreEqual(expectedCharacter, result.characterID);
        }

        // ScaleToSquare normalises per-axis below ONE_D_ASPECT_THRESHOLD (4.5) and uniformly
        // above it. The branch used to be chosen from the candidate alone, which made the
        // threshold a cliff: EI's five templates sit at aspect 3.09-3.94 - the highest of any
        // 2D glyph, only 1.14x under 4.5 - so an EI drawn ~20% wider than the reference was
        // normalised uniformly while every EI template stayed per-axis, and the two clouds were
        // no longer in the same space. EI's own score collapsed 0.92 -> 0.58 and HA, the only
        // uniformly-scaled class, took the lead. EI_draw_01 is a real recorded draw at aspect
        // 3.92; widening it 1.25x puts it at 4.90, just past the threshold. Before the fix this
        // returned HA at 0.599.
        //
        // The 1.15x case is the control: it stays on the per-axis side (aspect 4.51 by raw
        // bounds, and the resampled cloud can land either side), so it passed before too and
        // must keep passing. The pair together assert the cliff is gone, not merely moved.
        [TestCase(1.15f)]
        [TestCase(1.25f)]
        [TestCase(1.40f)]
        public void Recognize_ReturnsEI_WhenDrawnWideEnoughToCrossTheOneDScalingThreshold(float widthScale)
        {
            var recognizer = new DollarPRecognizer(32);
            recognizer.SetTemplateStrokeVariants(new TemplateLoader().LoadAll());

            const string fixturePath = "Assets/Tests/Fixtures/TestDraws/EI_draw_01.txt";
            TextAsset drawAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(fixturePath);
            Assert.IsNotNull(drawAsset, $"Missing {fixturePath}");

            List<List<Vector2>> strokes = ScaleWidthAboutCentre(
                StrokeTextParser.ParseStrokes(drawAsset.text), widthScale);

            RecognitionResult result = recognizer.Recognize(strokes);

            Assert.AreEqual("EI", result.characterID,
                $"An EI drawn {widthScale:0.00}x wider than the reference should still be EI, "
                + $"not {result.characterID} at {result.score:F3}.");
        }

        // Stretches horizontally about the drawing's own centre, leaving height alone, so only
        // the bounding-box aspect ratio changes.
        private static List<List<Vector2>> ScaleWidthAboutCentre(
            List<List<Vector2>> strokes, float widthScale)
        {
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            foreach (List<Vector2> stroke in strokes)
            {
                foreach (Vector2 point in stroke)
                {
                    if (point.x < minX) minX = point.x;
                    if (point.x > maxX) maxX = point.x;
                }
            }

            float centreX = (minX + maxX) * 0.5f;
            var scaled = new List<List<Vector2>>(strokes.Count);
            foreach (List<Vector2> stroke in strokes)
            {
                var scaledStroke = new List<Vector2>(stroke.Count);
                foreach (Vector2 point in stroke)
                    scaledStroke.Add(new Vector2((point.x - centreX) * widthScale + centreX, point.y));
                scaled.Add(scaledStroke);
            }

            return scaled;
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
