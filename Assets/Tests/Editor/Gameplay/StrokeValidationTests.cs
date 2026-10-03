using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class StrokeValidationTests
    {
        [Test]
        public void IsTapLikeStroke_RejectsTinyTap()
        {
            var points = new List<Vector2>
            {
                new Vector2(100f, 100f),
                new Vector2(102f, 101f),
                new Vector2(101f, 102f)
            };

            bool result = StrokeValidation.IsTapLikeStroke(
                points,
                minimumPathLengthPixels: 40f,
                minimumBoundsPixels: 12f);

            Assert.IsTrue(result);
        }

        [Test]
        public void IsTapLikeStroke_AllowsFastSparseMeaningfulStroke()
        {
            var points = new List<Vector2>
            {
                new Vector2(100f, 100f),
                new Vector2(220f, 120f),
                new Vector2(320f, 90f)
            };

            bool result = StrokeValidation.IsTapLikeStroke(
                points,
                minimumPathLengthPixels: 40f,
                minimumBoundsPixels: 12f);

            Assert.IsFalse(result);
        }

        [Test]
        public void IsRecognitionDegenerate_RejectsEmptyAndSinglePointOnly()
        {
            Assert.IsTrue(StrokeValidation.IsRecognitionDegenerate(null));
            Assert.IsTrue(StrokeValidation.IsRecognitionDegenerate(new List<List<Vector2>>()));
            Assert.IsTrue(StrokeValidation.IsRecognitionDegenerate(new List<List<Vector2>>
            {
                new List<Vector2> { new Vector2(10f, 10f) }
            }));
            Assert.IsFalse(StrokeValidation.IsRecognitionDegenerate(new List<List<Vector2>>
            {
                new List<Vector2> { new Vector2(10f, 10f), new Vector2(100f, 100f) }
            }));
        }
    }
}
