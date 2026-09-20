using System.Reflection;
using NUnit.Framework;
using Salinlahi.Debug.Sandbox;
using UnityEngine;

namespace Salinlahi.Tests.Editor.QA
{
    public sealed class QaProtectionTests
    {
        [TearDown]
        public void TearDown()
        {
            SandboxMode.Deactivate();
            SandboxMode.SetAvailabilityOverrideForTests(null);
        }

        [Test]
        public void QaProtectionFreezesMovementWithoutActivatingSandboxFlow()
        {
            SandboxMode.SetAvailabilityOverrideForTests(true);
            MethodInfo setProtection = typeof(SandboxMode).GetMethod(
                "SetQaProtectionEnabled",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(setProtection,
                "The editor-only QA protection seam must be available without entering sandbox flow.");

            setProtection.Invoke(null, new object[] { true });

            Assert.IsFalse(SandboxMode.IsActive,
                "QA protection must not switch the level into sandbox mode.");

            GameObject enemyObject = new GameObject("QA Enemy");
            try
            {
                enemyObject.AddComponent<BoxCollider2D>();
                EnemyMover mover = enemyObject.AddComponent<EnemyMover>();
                mover.SetSpeed(2f);

                Assert.AreEqual(0f, mover.GetFinalSpeedForTests(), 0.0001f,
                    "QA protection must hold enemy movement while recognition replay is in progress.");
            }
            finally
            {
                Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void QaProtectionBlocksHeartLossWithoutChangingNormalHeartCount()
        {
            SandboxMode.SetAvailabilityOverrideForTests(true);
            MethodInfo setProtection = typeof(SandboxMode).GetMethod(
                "SetQaProtectionEnabled",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(setProtection,
                "The editor-only QA protection seam must be available to the heart guard.");

            GameObject heartObject = new GameObject("QA Hearts");
            try
            {
                HeartSystem hearts = heartObject.AddComponent<HeartSystem>();
                InvokePrivate(hearts, "Awake");
                setProtection.Invoke(null, new object[] { true });

                hearts.LoseHeart();

                Assert.AreEqual(3, hearts.GetCurrentHearts(),
                    "QA protection must prevent defeat while the replay panel is active.");
            }
            finally
            {
                Object.DestroyImmediate(heartObject);
            }
        }

        private static void InvokePrivate(object instance, string methodName)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing private method {methodName}.");
            method.Invoke(instance, null);
        }
    }
}
