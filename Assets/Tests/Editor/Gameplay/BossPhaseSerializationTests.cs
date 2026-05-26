using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    // Locks the [FormerlySerializedAs] migration on BossPhase. If any of the
    // four renamed fields ever loses its FormerlySerializedAs attribute, an
    // existing serialized BossConfig asset using the old names will silently
    // zero-default — this test catches that regression.
    [TestFixture]
    public class BossPhaseSerializationTests
    {
        // YAML blob using ONLY the old field names. Mirrors how the live
        // BossConfig_ElInquisidor.asset stores a single phase today.
        private const string LegacyPhaseYaml =
            "summonDuration: 30\n" +
            "summonInterval: 7\n" +
            "summonBurstMin: 1\n" +
            "summonBurstMax: 2\n" +
            "requiredCharacterCount: 3\n" +
            "vulnerabilityTimer: 20\n";

        [Test]
        public void BossPhase_LoadedFromLegacyYaml_MigratesRenamedFields()
        {
            // Build a host ScriptableObject so we can drive Unity's serializer
            // (BossPhase itself isn't a SO — it's a [Serializable] class
            // embedded in BossConfigSO.phases).
            BossConfigSO host = ScriptableObject.CreateInstance<BossConfigSO>();
            host.phases = new System.Collections.Generic.List<BossPhase>
            {
                new BossPhase()
            };

            string assetPath = "Assets/__TestBossPhaseLegacy.asset";
            AssetDatabase.CreateAsset(host, assetPath);

            // Rewrite the asset's on-disk YAML to use the old field names so
            // we exercise the FormerlySerializedAs migration on reload.
            string raw = System.IO.File.ReadAllText(assetPath);
            string patched = raw.Replace(
                "  phases:\n  - summonPhaseDuration: 30",  // post-rename default written by CreateAsset
                "  phases:\n  - " + LegacyPhaseYaml.Replace("\n", "\n    ").TrimEnd());
            // If the post-rename serializer wrote something different, fall
            // back to a direct injection so the test still exercises legacy keys.
            if (patched == raw)
            {
                int idx = raw.IndexOf("  phases:\n  - ", System.StringComparison.Ordinal);
                Assert.GreaterOrEqual(idx, 0, "Could not locate phases list in asset YAML.");
                int insertAt = idx + "  phases:\n  - ".Length;
                patched = raw.Substring(0, insertAt)
                    + LegacyPhaseYaml.Replace("\n", "\n    ").TrimEnd() + "\n"
                    + raw.Substring(insertAt);
            }
            System.IO.File.WriteAllText(assetPath, patched);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            BossConfigSO reloaded = AssetDatabase.LoadAssetAtPath<BossConfigSO>(assetPath);
            Assert.IsNotNull(reloaded, "Reloaded asset must not be null.");
            Assert.AreEqual(1, reloaded.phases.Count, "Phase count must round-trip.");

            BossPhase p = reloaded.phases[0];
            Assert.AreEqual(30f, p.summonPhaseDuration, "summonDuration must migrate to summonPhaseDuration.");
            Assert.AreEqual(7f, p.delayBetweenSummons, "summonInterval must migrate to delayBetweenSummons.");
            Assert.AreEqual(1, p.minionsPerSummonMin, "summonBurstMin must migrate to minionsPerSummonMin.");
            Assert.AreEqual(2, p.minionsPerSummonMax, "summonBurstMax must migrate to minionsPerSummonMax.");

            AssetDatabase.DeleteAsset(assetPath);
        }
    }
}
