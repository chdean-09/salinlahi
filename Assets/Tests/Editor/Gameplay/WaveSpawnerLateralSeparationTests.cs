using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// A wave mixes moveSpeeds (Level 6 spans 0.85-1.9), so a fast enemy catches a slow one and the
    /// pair stacks vertically. Spawns keep a minimum horizontal distance from the previous one so
    /// both stay readable. These guard the separation and, just as importantly, that it can never
    /// stall a spawn when the band is too narrow to satisfy it.
    /// </summary>
    public class WaveSpawnerLateralSeparationTests
    {
        private const float MinX = -2.14f;
        private const float MaxX = 2.14f;

        private static float PickSpawnX(WaveSpawner spawner, float minX, float maxX)
        {
            MethodInfo method = typeof(WaveSpawner).GetMethod(
                "PickSpawnX", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "PickSpawnX is missing; the separation guard has been renamed or removed.");
            return (float)method.Invoke(spawner, new object[] { minX, maxX });
        }

        private static void SetPrivate(WaveSpawner spawner, string field, object value)
        {
            FieldInfo info = typeof(WaveSpawner).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{field} is missing.");
            info.SetValue(spawner, value);
        }

        [Test]
        public void PickSpawnX_KeepsSeparationFromPreviousSpawn()
        {
            GameObject obj = new("WaveSpawner");
            WaveSpawner spawner = obj.AddComponent<WaveSpawner>();
            try
            {
                SetPrivate(spawner, "_minLateralSpawnSeparation", 1.8f);
                SetPrivate(spawner, "_lateralSeparationAttempts", 64);

                // Anchor at one edge so a satisfying X exists across most of the band.
                SetPrivate(spawner, "_lastSpawnX", (float?)MinX);

                for (int i = 0; i < 200; i++)
                {
                    Random.InitState(i);
                    SetPrivate(spawner, "_lastSpawnX", (float?)MinX);
                    float x = PickSpawnX(spawner, MinX, MaxX);
                    Assert.GreaterOrEqual(Mathf.Abs(x - MinX), 1.8f,
                        $"seed {i}: spawn landed {Mathf.Abs(x - MinX):F2} from the previous one");
                    Assert.That(x, Is.InRange(MinX, MaxX), $"seed {i}: spawn left the band");
                }
            }
            finally
            {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void PickSpawnX_FirstSpawnIsUnconstrained()
        {
            GameObject obj = new("WaveSpawner");
            WaveSpawner spawner = obj.AddComponent<WaveSpawner>();
            try
            {
                SetPrivate(spawner, "_minLateralSpawnSeparation", 1.8f);
                SetPrivate(spawner, "_lastSpawnX", null);

                float x = PickSpawnX(spawner, MinX, MaxX);
                Assert.That(x, Is.InRange(MinX, MaxX));
            }
            finally
            {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void PickSpawnX_BandTooNarrowForSeparation_StillReturnsInBandValue()
        {
            GameObject obj = new("WaveSpawner");
            WaveSpawner spawner = obj.AddComponent<WaveSpawner>();
            try
            {
                // Separation wider than the whole band: no X can ever satisfy it.
                SetPrivate(spawner, "_minLateralSpawnSeparation", 50f);
                SetPrivate(spawner, "_lateralSeparationAttempts", 8);
                SetPrivate(spawner, "_lastSpawnX", (float?)0f);

                float x = PickSpawnX(spawner, MinX, MaxX);
                Assert.That(x, Is.InRange(MinX, MaxX),
                    "An unsatisfiable separation must fall back to an in-band X, not stall or escape the band.");
            }
            finally
            {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void PickSpawnX_SeparationDisabled_SkipsRerolling()
        {
            GameObject obj = new("WaveSpawner");
            WaveSpawner spawner = obj.AddComponent<WaveSpawner>();
            try
            {
                SetPrivate(spawner, "_minLateralSpawnSeparation", 0f);
                SetPrivate(spawner, "_lastSpawnX", (float?)0f);

                Random.InitState(12345);
                float expected = Random.Range(MinX, MaxX);

                Random.InitState(12345);
                float actual = PickSpawnX(spawner, MinX, MaxX);

                Assert.AreEqual(expected, actual, 1e-6f,
                    "With separation disabled the first roll must be used as-is.");
            }
            finally
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
