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
