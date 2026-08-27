using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
        // Scratch asset built fresh by each run. It is never committed: the
        // AssetDatabase needs a path under Assets/, and TearDown removes it.
        private const string TempAssetPath = "Assets/__TestBossPhaseLegacy.asset";

        // YAML blob using ONLY the old field names, shaped like a BossConfig
        // asset authored before the SALIN-98 rename. Every value here must
        // differ from the matching BossPhase field initializer — otherwise a
        // dropped [FormerlySerializedAs] leaves the field at its default and
        // the assert still passes, which is exactly the blind spot this
        // fixture exists to cover.
        private const string LegacyPhaseYaml =
            "summonDuration: 42\n" +
            "summonInterval: 7\n" +
            "summonBurstMin: 1\n" +
            "summonBurstMax: 2\n" +
            "requiredCharacterCount: 3\n" +
            "vulnerabilityTimer: 20\n";

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
            // Build a host ScriptableObject so we can drive Unity's serializer
            // (BossPhase itself isn't a SO — it's a [Serializable] class
            // embedded in BossConfigSO.phases).
            BossConfigSO host = ScriptableObject.CreateInstance<BossConfigSO>();
            host.phases = new List<BossPhase> { new BossPhase() };
            AssetDatabase.CreateAsset(host, TempAssetPath);

            // Rewrite the asset's on-disk YAML to use the old field names so
            // we exercise the FormerlySerializedAs migration on reload.
            string raw = File.ReadAllText(TempAssetPath);
            File.WriteAllText(TempAssetPath, ReplacePhasesWithLegacyKeys(raw));
            AssetDatabase.ImportAsset(TempAssetPath, ImportAssetOptions.ForceSynchronousImport);

            BossConfigSO reloaded = AssetDatabase.LoadAssetAtPath<BossConfigSO>(TempAssetPath);
            Assert.IsNotNull(reloaded, "Reloaded asset must not be null.");
            Assert.AreEqual(1, reloaded.phases.Count, "Phase count must round-trip.");

            BossPhase p = reloaded.phases[0];
            Assert.AreEqual(42f, p.summonPhaseDuration, "summonDuration must migrate to summonPhaseDuration.");
            Assert.AreEqual(7f, p.delayBetweenSummons, "summonInterval must migrate to delayBetweenSummons.");
            Assert.AreEqual(1, p.minionsPerSummonMin, "summonBurstMin must migrate to minionsPerSummonMin.");
            Assert.AreEqual(2, p.minionsPerSummonMax, "summonBurstMax must migrate to minionsPerSummonMax.");
        }

        // Swaps the entire serialized `phases:` list for one phase written with
        // only the pre-rename keys. Replacing the whole block is the point: a
        // post-rename key left alongside its legacy counterpart wins during
        // deserialization, so the migration would never actually be exercised.
        private static string ReplacePhasesWithLegacyKeys(string yaml)
        {
            int phasesIndex = yaml.IndexOf("\n  phases:", StringComparison.Ordinal);
            Assert.GreaterOrEqual(phasesIndex, 0, "Could not locate the phases list in the asset YAML.");

            int listStart = yaml.IndexOf('\n', phasesIndex + 1);
            Assert.GreaterOrEqual(listStart, 0, "Phases list is missing a following field.");

            // The list ends at the next sibling field: a two-space-indented line
            // that is neither a "  - " list item nor a "    " continuation line.
            Match sibling = Regex.Match(yaml.Substring(listStart), "\n  [A-Za-z_]");
            Assert.IsTrue(sibling.Success, "Could not locate the field following the phases list.");

            return yaml.Substring(0, phasesIndex)
                + "\n  phases:\n  - " + LegacyPhaseYaml.Replace("\n", "\n    ").TrimEnd()
                + yaml.Substring(listStart + sibling.Index);
        }
    }
}
