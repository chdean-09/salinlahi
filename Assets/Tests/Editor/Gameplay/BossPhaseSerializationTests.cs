using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Gameplay
{
    // Locks the [FormerlySerializedAs] migration on BossPhase. If any of the
    // four renamed fields ever loses its FormerlySerializedAs attribute, an
    // existing serialized BossConfig asset using the old names will silently
    // zero-default — this test catches that regression.
    [TestFixture]
    public class BossPhaseSerializationTests
    {
        // Scratch asset built fresh by each run. It is never committed: the
        // AssetDatabase needs a path under Assets/, and TearDown removes it.
        private const string TempAssetPath = "Assets/__TestBossPhaseLegacy.asset";

        // Use a real imported BossConfig asset as the fixture source instead of creating a
        // ScriptableObject and then mutating its still-loaded YAML. Unity may serialize that
        // live object back over the mutation during ImportAsset, which makes a migration test
        // read today's defaults rather than a historical pre-rename asset.
        private const string SourceBossConfigPath =
            "Assets/ScriptableObjects/Enemies/Boss Configs/BossConfig_Superintendent.asset";

        [SetUp]
        public void DeleteStaleTempAssetBeforeImport()
        {
            AssetDatabase.DeleteAsset(TempAssetPath);
        }

        // Runs even when an assert fails, so a red test cannot leave the
        // scratch asset behind for someone to commit by accident.
        [TearDown]
        public void DeleteTempAsset()
        {
            AssetDatabase.DeleteAsset(TempAssetPath);
        }

        [Test]
        public void BossPhase_LoadedFromLegacyYaml_MigratesRenamedFields()
        {
            string source = File.ReadAllText(SourceBossConfigPath);
            string historical = RewriteAsPreRenameFixture(source);
            File.WriteAllText(TempAssetPath, historical);
            AssetDatabase.ImportAsset(TempAssetPath, ImportAssetOptions.ForceSynchronousImport);

            BossConfigSO reloaded = AssetDatabase.LoadAssetAtPath<BossConfigSO>(TempAssetPath);
            Assert.IsNotNull(reloaded, "Reloaded asset must not be null.");
            Assert.AreEqual(1, reloaded.phases.Count, "The one-phase historical fixture must round-trip.");

            BossPhase p = reloaded.phases[0];
            Assert.AreEqual(42f, p.summonPhaseDuration, "summonDuration must migrate to summonPhaseDuration.");
            Assert.AreEqual(7f, p.delayBetweenSummons, "summonInterval must migrate to delayBetweenSummons.");
            Assert.AreEqual(1, p.minionsPerSummonMin, "summonBurstMin must migrate to minionsPerSummonMin.");
            Assert.AreEqual(2, p.minionsPerSummonMax, "summonBurstMax must migrate to minionsPerSummonMax.");
        }

        // Converts the checked-in current asset into a freshly imported historical fixture. All
        // four renamed fields are replaced everywhere, so no post-rename key remains alongside a
        // legacy key. The first phase values are deliberately different from BossPhase defaults;
        // a removed FormerlySerializedAs then fails loudly instead of passing by coincidence.
        private static string RewriteAsPreRenameFixture(string yaml)
        {
            Assert.IsNotEmpty(yaml, "The checked-in BossConfig fixture could not be read.");

            string historical = yaml
                .Replace("summonPhaseDuration:", "summonDuration:")
                .Replace("delayBetweenSummons:", "summonInterval:")
                .Replace("minionsPerSummonMin:", "summonBurstMin:")
                .Replace("minionsPerSummonMax:", "summonBurstMax:")
                .Replace("m_Name: BossConfig_Superintendent", "m_Name: __TestBossPhaseLegacy");

            historical = ReplaceFirst(historical, "summonDuration: 30", "summonDuration: 42");
            historical = ReplaceFirst(historical, "summonInterval: 5", "summonInterval: 7");
            historical = ReplaceFirst(historical, "summonBurstMin: 2", "summonBurstMin: 1");
            historical = ReplaceFirst(historical, "summonBurstMax: 3", "summonBurstMax: 2");

            return historical;
        }

        private static string ReplaceFirst(string value, string oldValue, string newValue)
        {
            int index = value.IndexOf(oldValue, StringComparison.Ordinal);
            Assert.GreaterOrEqual(index, 0, "Fixture did not contain expected text: " + oldValue);
            return value.Substring(0, index) + newValue
                + value.Substring(index + oldValue.Length);
        }
    }
}
