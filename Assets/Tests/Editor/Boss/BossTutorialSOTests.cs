using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Boss
{
    [TestFixture]
    public class BossTutorialSOTests
    {
        [Test]
        public void NewInstance_HasNoPages()
        {
            BossTutorialSO so = ScriptableObject.CreateInstance<BossTutorialSO>();
            try
            {
                Assert.AreEqual(0, so.PageCount);
                Assert.IsFalse(so.HasPages);
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void WithPages_ReportsCountAndHasPages()
        {
            BossTutorialSO so = ScriptableObject.CreateInstance<BossTutorialSO>();
            try
            {
                so.pages = new List<BossTutorialPage>
                {
                    new BossTutorialPage { title = "El Inquisidor", body = "Lore" },
                    new BossTutorialPage { title = "Summoning", body = "It calls minions." },
                };
                Assert.AreEqual(2, so.PageCount);
                Assert.IsTrue(so.HasPages);
            }
            finally { Object.DestroyImmediate(so); }
        }
    }
}
