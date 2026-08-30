using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Boss
{
    [TestFixture]
    public class BossTutorialControllerTests
    {
        [Test]
        public void Scroll_ShowWithEmptyPages_StaysHidden()
        {
            GameObject host = new("BossTutorialScrollHost");
            // The scroll starts hidden in the scene; Show must not activate it
            // for an empty page list. A fresh GameObject defaults to active, so
            // mirror the production starting state before calling Show.
            host.SetActive(false);
            try
            {
                BossTutorialScroll scroll = host.AddComponent<BossTutorialScroll>();
                scroll.Show(new List<BossTutorialPage>());
                Assert.IsFalse(host.activeSelf, "Empty page list must not activate the scroll.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Play_WhenConfigNull_ReturnsImmediately()
        {
            GameObject host = new("BossTutorialControllerHost");
            try
            {
                BossTutorialController controller = host.AddComponent<BossTutorialController>();
                IEnumerator routine = controller.Play(null);
                Assert.IsFalse(routine.MoveNext(), "Null config must return immediately.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Play_WhenTutorialHasNoPages_ReturnsImmediately()
        {
            GameObject host = new("BossTutorialControllerHost");
            BossConfigSO config = ScriptableObject.CreateInstance<BossConfigSO>();
            config.tutorial = ScriptableObject.CreateInstance<BossTutorialSO>();
            try
            {
                BossTutorialController controller = host.AddComponent<BossTutorialController>();
                IEnumerator routine = controller.Play(config);
                Assert.IsFalse(routine.MoveNext(), "Empty tutorial must return immediately without showing.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(config.tutorial);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Play_WhenTutorialNull_ReturnsImmediately()
        {
            GameObject host = new("BossTutorialControllerHost");
            BossConfigSO config = ScriptableObject.CreateInstance<BossConfigSO>();
            // config.tutorial intentionally left null
            try
            {
                BossTutorialController controller = host.AddComponent<BossTutorialController>();
                IEnumerator routine = controller.Play(config);
                Assert.IsFalse(routine.MoveNext(), "Config with null tutorial must return immediately.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Play_WhenScrollUnwired_ReturnsImmediately()
        {
            GameObject host = new("BossTutorialControllerHost");
            BossConfigSO config = ScriptableObject.CreateInstance<BossConfigSO>();
            config.tutorial = ScriptableObject.CreateInstance<BossTutorialSO>();
            config.tutorial.pages = new List<BossTutorialPage> { new BossTutorialPage { title = "Boss", body = "Lore" } };
            try
            {
                BossTutorialController controller = host.AddComponent<BossTutorialController>();
                // No _scroll assigned.
                IEnumerator routine = controller.Play(config);
                Assert.IsFalse(routine.MoveNext(), "Unwired scroll must skip gracefully.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(config.tutorial);
                Object.DestroyImmediate(config);
            }
        }
    }
}
