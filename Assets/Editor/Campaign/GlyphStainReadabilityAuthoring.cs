using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Re-authors the stained-badge dwell times on every <see cref="EnemyDataSO"/> that stains nearby
/// glyphs, so the Inspector shows the values the game actually uses.
///
/// <para>Mantsa shipped with <c>scrambleMinGlitchInterval 0.18</c> / <c>scrambleMaxGlitchInterval
/// 0.36</c> and nothing else. Those numbers were never wrong as a churn speed — what was missing
/// was anywhere for the badge to rest. The timing is now asymmetric: wrong faces scroll past
/// quickly, then the true face holds for a long, readable beat. In Level 1 the enemy that sits
/// inside Mantsa's 3-unit radius longest is Nawalang Mukha, which is where this reads worst.</para>
///
/// <para>The runtime floors both bands regardless
/// (<see cref="GlyphStainCycle.MinimumFalseGlyphInterval"/> and
/// <see cref="GlyphStainCycle.MinimumReadableInterval"/>), so the game reads correctly with or
/// without this command. This exists so the authored numbers stop contradicting the runtime, and
/// is written as an Editor command rather than hand-edited asset YAML so Unity owns the
/// serialization and the GUIDs stay put.</para>
///
/// <para>Run from Salinlahi/Campaign/Author Glyph Stain Readability.</para>
/// </summary>
public static class GlyphStainReadabilityAuthoring
{
    [MenuItem("Salinlahi/Campaign/Author Glyph Stain Readability")]
    public static void Author()
    {
        string[] guids = AssetDatabase.FindAssets("t:EnemyDataSO");
        var report = new List<string>();
        int updated = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
            if (data == null || !data.stainsNearbyGlyphs)
                continue;

            float oldMin = data.scrambleMinGlitchInterval;
            float oldMax = data.scrambleMaxGlitchInterval;
            float oldTrueMin = data.scrambleTrueGlyphMinDwell;
            int oldBurst = data.scrambleFalseBurstCount;

            bool churnOk = oldMin >= GlyphStainCycle.MinimumFalseGlyphInterval
                           && oldMax >= oldMin;
            bool restOk = oldTrueMin >= GlyphStainCycle.MinimumReadableInterval;
            bool burstOk = oldBurst >= 2;

            if (churnOk && restOk && burstOk)
            {
                report.Add($"  {data.name}: already tuned (churn {oldMin:0.##}-{oldMax:0.##}s, "
                           + $"rest {oldTrueMin:0.##}s, burst {oldBurst}), untouched.");
                continue;
            }

            // Mutate the existing asset rather than recreating it: CreateAsset would reissue the
            // GUID and silently unwire every level and prefab that references this enemy.
            data.scrambleMinGlitchInterval = GlyphStainCycle.DefaultFalseMinInterval;
            data.scrambleMaxGlitchInterval = GlyphStainCycle.DefaultFalseMaxInterval;
            data.scrambleTrueGlyphMinDwell = GlyphStainCycle.DefaultTrueMinInterval;
            data.scrambleTrueGlyphMaxDwell = GlyphStainCycle.DefaultTrueMaxInterval;
            data.scrambleFalseBurstCount = GlyphStainCycle.DefaultFalseBurstCount;
            EditorUtility.SetDirty(data);
            updated++;
            report.Add($"  {data.name}: churn {oldMin:0.##}-{oldMax:0.##}s -> "
                       + $"{GlyphStainCycle.DefaultFalseMinInterval:0.##}-{GlyphStainCycle.DefaultFalseMaxInterval:0.##}s, "
                       + $"true-face rest -> {GlyphStainCycle.DefaultTrueMinInterval:0.##}-{GlyphStainCycle.DefaultTrueMaxInterval:0.##}s, "
                       + $"burst -> {GlyphStainCycle.DefaultFalseBurstCount}");
        }

        if (updated > 0)
            AssetDatabase.SaveAssets();

        Debug.Log($"[GlyphStainReadabilityAuthoring] Updated {updated} enemy data asset(s).\n"
                  + string.Join("\n", report));
    }
}
