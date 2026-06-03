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
            try
            {
                BossTutorialScroll scroll = host.AddComponent<BossTutorialScroll>();
                scroll.Show(new List<BossTutorialPage>());
                Assert.IsFalse(host.activeSelf, "Empty page list must not activate the scroll.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Play_WhenTutorialNull_ReturnsImmediately()
        {
            GameObject host = new("BossTutorialControllerHost");
            try
            {
                BossTutorialController controller = host.AddComponent<BossTutorialController>();
                IEnumerator routine = controller.Play(null);
                Assert.IsFalse(routine.MoveNext(), "Null tutorial must return immediately.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Play_WhenTutorialHasNoPages_ReturnsImmediately()
        {
            GameObject host = new("BossTutorialControllerHost");
            BossTutorialSO so = ScriptableObject.CreateInstance<BossTutorialSO>();
            try
            {
                BossTutorialController controller = host.AddComponent<BossTutorialController>();
                IEnumerator routine = controller.Play(so);
                Assert.IsFalse(routine.MoveNext(), "Empty tutorial must return immediately without showing.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void Play_WhenScrollUnwired_ReturnsImmediately()
        {
            GameObject host = new("BossTutorialControllerHost");
            BossTutorialSO so = ScriptableObject.CreateInstance<BossTutorialSO>();
            so.pages = new List<BossTutorialPage> { new BossTutorialPage { title = "Boss", body = "Lore" } };
            try
            {
                BossTutorialController controller = host.AddComponent<BossTutorialController>();
                // No _scroll assigned.
                IEnumerator routine = controller.Play(so);
                Assert.IsFalse(routine.MoveNext(), "Unwired scroll must skip gracefully.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(so);
            }
        }
    }
}
