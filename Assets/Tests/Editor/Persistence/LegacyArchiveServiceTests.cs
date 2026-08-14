using System.Collections.Generic;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class LegacyArchiveServiceTests
    {
        [Test]
        public void Catalog_ContainsExactly46UniqueKeys()
        {
            HashSet<string> keys = new HashSet<string>();
            for (int i = 0; i < LegacyProgressKeyCatalog.All.Count; i++)
                Assert.That(keys.Add(LegacyProgressKeyCatalog.All[i].Key), Is.True);
            Assert.That(keys.Count, Is.EqualTo(46));
        }
    }
}
