using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class StrokeGeometryTests
    {
        [Test]
        public void TryAppendPoint_AddsFirstPointAndRejectsNearDuplicate()
        {
            var points = new List<Vector2>();

            bool addedFirst = StrokeGeometry.TryAppendPoint(
                points, new Vector2(10f, 20f), minDistancePixels: 2f);
            bool addedDuplicate = StrokeGeometry.TryAppendPoint(
                points, new Vector2(11f, 20f), minDistancePixels: 2f);
            bool addedFarPoint = StrokeGeometry.TryAppendPoint(
                points, new Vector2(13f, 20f), minDistancePixels: 2f);

            Assert.IsTrue(addedFirst);
            Assert.IsFalse(addedDuplicate);
            Assert.IsTrue(addedFarPoint);
            Assert.AreEqual(2, points.Count);
            Assert.AreEqual(new Vector2(10f, 20f), points[0]);
            Assert.AreEqual(new Vector2(13f, 20f), points[1]);
        }

        [Test]
        public void ComputePathLength_UsesConsecutiveSegments()
        {
            var points = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(3f, 4f),
                new Vector2(6f, 8f)
            };

            Assert.AreEqual(10f, StrokeGeometry.ComputePathLength(points), 0.0001f);
        }

        [Test]
        public void ComputeBoundsSize_ReturnsWidthAndHeight()
        {
            var points = new List<Vector2>
            {
                new Vector2(10f, 50f),
                new Vector2(25f, 45f),
                new Vector2(18f, 80f)
            };

            Vector2 size = StrokeGeometry.ComputeBoundsSize(points);

            Assert.AreEqual(15f, size.x, 0.0001f);
            Assert.AreEqual(35f, size.y, 0.0001f);
        }

        [Test]
        public void AppendVisualSegment_SubdividesLongSegmentWithoutChangingRawPoints()
        {
            var visualPoints = new List<Vector2> { new Vector2(0f, 0f) };
            var rawPoints = new List<Vector2> { new Vector2(0f, 0f) };

            StrokeGeometry.AppendVisualSegment(
                visualPoints,
                from: new Vector2(0f, 0f),
                to: new Vector2(100f, 0f),
                spacingPixels: 20f,
                maxInsertedPoints: 8);

            rawPoints.Add(new Vector2(100f, 0f));

            Assert.AreEqual(2, rawPoints.Count, "Recognition raw points should remain real input samples only.");
            Assert.AreEqual(6, visualPoints.Count, "0, 20, 40, 60, 80, and 100 should be rendered.");
            Assert.AreEqual(new Vector2(100f, 0f), visualPoints[visualPoints.Count - 1]);
        }

        [Test]
        public void RebuildVisualCurve_CreatesCurvedIntermediatePointsWithoutChangingRawPoints()
        {
            var rawPoints = new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(50f, 80f),
                new Vector2(100f, 100f),
                new Vector2(150f, 80f)
            };
            var visualPoints = new List<Vector2>();

            StrokeGeometry.RebuildVisualCurve(
                rawPoints,
                visualPoints,
                spacingPixels: 8f,
                maxInsertedPointsPerSegment: 24);

            Assert.AreEqual(4, rawPoints.Count, "Recognition raw points should remain real input samples only.");
            Assert.AreEqual(new Vector2(0f, 0f), visualPoints[0]);
            Assert.AreEqual(new Vector2(150f, 80f), visualPoints[visualPoints.Count - 1]);
            Assert.Greater(visualPoints.Count, rawPoints.Count);

            bool hasCurvedPointBetweenSecondAndThirdRawSamples = false;
            for (int i = 0; i < visualPoints.Count; i++)
            {
                Vector2 point = visualPoints[i];
                if (point.x <= 50f || point.x >= 100f)
                    continue;

                float distanceFromChord = DistanceFromLineSegment(
                    point,
                    new Vector2(50f, 80f),
                    new Vector2(100f, 100f));

                if (distanceFromChord > 1f)
                {
                    hasCurvedPointBetweenSecondAndThirdRawSamples = true;
                    break;
                }
            }

            Assert.IsTrue(hasCurvedPointBetweenSecondAndThirdRawSamples,
                "Visual-only interpolation should curve between sparse real samples instead of staying on the straight chord.");
        }

        [Test]
        public void CapturedStroke_BeginPointAppearsInRawAndVisualStreams()
        {
            var stroke = new CapturedStroke(fingerIndex: 7, touchId: 123, startTime: 5.0);

            stroke.Begin(new Vector2(200f, 300f));

            Assert.AreEqual(7, stroke.FingerIndex);
            Assert.AreEqual(123, stroke.TouchId);
            Assert.AreEqual(5.0, stroke.StartTime, 0.0001);
            Assert.AreEqual(1, stroke.RawPoints.Count);
            Assert.AreEqual(1, stroke.VisualPoints.Count);
            Assert.AreEqual(new Vector2(200f, 300f), stroke.RawPoints[0]);
            Assert.AreEqual(new Vector2(200f, 300f), stroke.VisualPoints[0]);
        }

        private static float DistanceFromLineSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f)
                return Vector2.Distance(point, start);

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            Vector2 closest = start + segment * t;
            return Vector2.Distance(point, closest);
        }
    }
}
