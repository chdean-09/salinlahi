using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class BossDamageFeedbackTests
    {
        private float _originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            _originalTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _originalTimeScale;
        }

        [Test]
        public void PlayHitFeedback_RestoresPreviousTimeScale_AfterScreenDip()
        {
            var go = new GameObject("BossDamageFeedback_Test");
            try
            {
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<BossController>();
                BossDamageFeedback feedback = go.AddComponent<BossDamageFeedback>();

                Time.timeScale = 0.5f;
                IEnumerator routine = InvokePrivate<IEnumerator>(
                    feedback,
                    "PlayHitFeedback",
                    0.01f,
                    0f,
                    0f,
                    0.3f);

                Assert.IsTrue(routine.MoveNext());
                Assert.AreEqual(0.7f, Time.timeScale, 0.0001f);

                Assert.IsTrue(routine.MoveNext());
                Assert.AreEqual(0.5f, Time.timeScale, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            return (T)method.Invoke(target, args);
        }
    }
}
