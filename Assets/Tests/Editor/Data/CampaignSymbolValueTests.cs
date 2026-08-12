using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    public class CampaignSymbolValueTests
    {
        [Test]
        public void TryGetSpokenValue_ResolvesDaAndRaFromOneVisualSymbol()
        {
            BaybayinCharacterSO symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            try
            {
                symbol.stableId = "symbol.dara";
                symbol.legacyAliases = new List<string> { "DA", "RA" };
                symbol.spokenValues = new List<SpokenValueDefinition>
                {
                    new() { stableId = "value.da", displayValue = "DA" },
                    new() { stableId = "value.ra", displayValue = "RA" },
                };

                Assert.IsTrue(symbol.TryGetSpokenValue("value.da", out SpokenValueDefinition da));
                Assert.AreEqual("DA", da.displayValue);
                Assert.IsTrue(symbol.TryGetSpokenValue("value.ra", out SpokenValueDefinition ra));
                Assert.AreEqual("RA", ra.displayValue);
                Assert.IsFalse(symbol.TryGetSpokenValue("value.unknown", out _));
            }
            finally
            {
                Object.DestroyImmediate(symbol);
            }
        }

        [Test]
        public void TryGetSpokenValue_RejectsDuplicateValueIds()
        {
            BaybayinCharacterSO symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            try
            {
                symbol.spokenValues = new List<SpokenValueDefinition>
                {
                    new() { stableId = "value.da" },
                    new() { stableId = "value.da" },
                };

                Assert.IsFalse(symbol.TryGetSpokenValue("value.da", out _));
            }
            finally
            {
                Object.DestroyImmediate(symbol);
            }
        }
    }
}
