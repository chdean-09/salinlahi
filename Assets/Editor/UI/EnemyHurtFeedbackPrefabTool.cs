using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-210. Authors <see cref="EnemyHurtFeedback"/> onto the enemy prefabs so it is not added
/// at spawn time.
///
/// `Enemy.Initialize` calls `gameObject.AddComponent&lt;EnemyHurtFeedback&gt;()` whenever the
/// component is missing, and no enemy prefab carried it — so every enemy allocated a component on
/// its first initialise and logged a warning. That quietly works against the pool's whole reason
/// for existing: no Instantiate and no AddComponent during the game loop (doc 01 §5).
///
/// Behaviour-neutral: the runtime path already guarantees the component exists. This only moves
/// the cost from spawn time to author time, and stops the warning.
///
/// Prefab VARIANTS are skipped. A variant inherits its base's components, so adding it to both
/// would duplicate the component on the variant.
/// </summary>
public static class EnemyHurtFeedbackPrefabTool
{
    private const string EnemyPrefabFolder = "Assets/Prefabs/Enemies";

    [MenuItem("Salinlahi/SALIN-210/Add EnemyHurtFeedback To Enemy Prefabs")]
    public static void Apply() => Run(apply: true);
    public static void Report() => Run(apply: false);

    private static void Run(bool apply)
    {
        var log = new StringBuilder($"=== EnemyHurtFeedback prefab pass (apply={apply}) ===\n");
        int added = 0, already = 0, variants = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponent<Enemy>() == null) continue;

            string name = Path.GetFileNameWithoutExtension(path);

            if (PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant)
            {
                // Verify the inheritance rather than assuming it: a variant whose base is not an
                // enemy prefab would silently keep allocating at runtime.
                bool inherited = asset.GetComponent<EnemyHurtFeedback>() != null;
                log.AppendLine($"  {name,-26} variant — inherited={inherited}" +
                               (inherited ? "" : "  *** BASE IS MISSING IT ***"));
                variants++;
                continue;
            }

            if (asset.GetComponent<EnemyHurtFeedback>() != null)
            {
                log.AppendLine($"  {name,-26} already present");
                already++;
                continue;
            }

            if (!apply) { log.AppendLine($"  {name,-26} WOULD ADD"); added++; continue; }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root.GetComponent<EnemyHurtFeedback>() == null)
                root.AddComponent<EnemyHurtFeedback>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);

            log.AppendLine($"  {name,-26} added");
            added++;
        }

        if (apply) AssetDatabase.SaveAssets();
        log.AppendLine($"  -- added={added} alreadyPresent={already} variantsSkipped={variants}");
        Debug.Log(log.ToString());
        File.WriteAllText("hurtfeedback-report.txt", log.ToString());
    }
}
