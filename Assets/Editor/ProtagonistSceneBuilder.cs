using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Salinlahi.Runtime.Gameplay;

public static class ProtagonistSceneBuilder
{
    private const string GameplayScenePath = "Assets/_Scenes/Gameplay.unity";
    private const string ProtagonistPrefabPath = "Assets/Prefabs/Protagonist/Protagonist.prefab";
    private const string SlashVfxPrefabPath = "Assets/Prefabs/Protagonist/ProtagonistSlashVfx.prefab";
    private const string ProtagonistManagerPrefabPath = "Assets/Prefabs/Managers/[Manager] ProtagonistManager.prefab";

    [MenuItem("Salinlahi/Protagonist/Configure Protagonist in Gameplay")]
    public static void ConfigureProtagonistInGameplay()
    {
        if (!System.IO.File.Exists(GameplayScenePath))
        {
            EditorUtility.DisplayDialog(
                "Protagonist Scene Builder",
                $"Missing gameplay scene:\n{GameplayScenePath}",
                "OK");
            return;
        }

        bool proceed = EditorUtility.DisplayDialog(
            "Protagonist Scene Builder",
            "This will open Gameplay.unity and add/configure the protagonist system (ProtagonistManager, attack controller, and prefabs).",
            "Configure Gameplay",
            "Cancel");

        if (!proceed)
            return;

        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        ConfigureScene(scene);
    }

    [MenuItem("Salinlahi/Protagonist/Setup Protagonist in Current Scene")]
    public static void SetupProtagonistInCurrentScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        
        bool proceed = EditorUtility.DisplayDialog(
            "Setup Protagonist",
            $"This will add/configure the protagonist system in the current scene '{scene.name}'.",
            "Proceed",
            "Cancel");
        
        if (!proceed)
            return;
        
        ConfigureScene(scene);
    }

    private static void ConfigureScene(Scene scene)
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Protagonist Scene Setup");
        
        try
        {
            ConfigureSceneCore(scene);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static void ConfigureSceneCore(Scene scene)
    {
        // Check for required prefabs
        if (!ValidatePrefabs())
            return;

        // Ensure ProtagonistManager exists
        ProtagonistManager manager = EnsureProtagonistManager();
        
        // Configure the manager with prefabs
        ConfigureProtagonistManager(manager);
        
        // Ensure ProtagonistAttackController exists
        ProtagonistAttackController attackController = EnsureAttackController(manager);
        
        // Configure attack controller
        ConfigureAttackController(attackController);

        // Mark scene dirty and save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = "Protagonist system configured successfully!\n\n" +
                        "Next steps:\n" +
                        "1. Assign protagonist sprite to Protagonist.prefab\n" +
                        "2. Assign slash animation frames to ProtagonistSlashVfx.prefab\n" +
                        "3. Test by playing Level 1";

        EditorUtility.DisplayDialog("Protagonist Scene Builder", message, "OK");
        Debug.Log("[Salinlahi] Protagonist system configured in scene.");
    }

    private static bool ValidatePrefabs()
    {
        bool allValid = true;
        string missing = "";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistPrefabPath) == null)
        {
            missing += $"\n- {ProtagonistPrefabPath}";
            allValid = false;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(SlashVfxPrefabPath) == null)
        {
            missing += $"\n- {SlashVfxPrefabPath}";
            allValid = false;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistManagerPrefabPath) == null)
        {
            missing += $"\n- {ProtagonistManagerPrefabPath}";
            allValid = false;
        }

        if (!allValid)
        {
            EditorUtility.DisplayDialog(
                "Protagonist Scene Builder - Error",
                $"Missing required prefabs:{missing}\n\nPlease ensure all prefabs exist.",
                "OK");
        }

        return allValid;
    }

    private static ProtagonistManager EnsureProtagonistManager()
    {
        ProtagonistManager existing = Object.FindFirstObjectByType<ProtagonistManager>();
        if (existing != null)
        {
            Debug.Log("[Salinlahi] Found existing ProtagonistManager.");
            return existing;
        }

        // Try to instantiate from prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistManagerPrefabPath);
        if (prefab != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "[Manager] ProtagonistManager";
            Undo.RegisterCreatedObjectUndo(instance, "Create ProtagonistManager");
            Debug.Log("[Salinlahi] Created ProtagonistManager from prefab.");
            return instance.GetComponent<ProtagonistManager>();
        }

        // Fallback: create manually
        GameObject go = new("[Manager] ProtagonistManager");
        Undo.RegisterCreatedObjectUndo(go, "Create ProtagonistManager");
        ProtagonistManager manager = go.AddComponent<ProtagonistManager>();
        go.AddComponent<ProtagonistAttackController>();
        Debug.Log("[Salinlahi] Created ProtagonistManager manually.");
        return manager;
    }

    private static void ConfigureProtagonistManager(ProtagonistManager manager)
    {
        SerializedObject serialized = new(manager);
        
        // Load and assign protagonist prefab
        GameObject protagonistPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistPrefabPath);
        if (protagonistPrefab != null)
        {
            serialized.FindProperty("_protagonistPrefab").objectReferenceValue = protagonistPrefab;
            Debug.Log("[Salinlahi] Assigned protagonist prefab.");
        }
        else
        {
            Debug.LogWarning("[Salinlahi] Could not load protagonist prefab.");
        }

        // Ensure walk duration is reasonable
        SerializedProperty walkDuration = serialized.FindProperty("_walkInDuration");
        if (walkDuration.floatValue <= 0)
        {
            walkDuration.floatValue = 1.5f;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
    }

    private static ProtagonistAttackController EnsureAttackController(ProtagonistManager manager)
    {
        ProtagonistAttackController existing = manager.GetComponent<ProtagonistAttackController>();
        if (existing != null)
        {
            return existing;
        }

        existing = Object.FindFirstObjectByType<ProtagonistAttackController>();
        if (existing != null)
        {
            return existing;
        }

        // Add to manager GameObject
        existing = manager.gameObject.AddComponent<ProtagonistAttackController>();
        Undo.RegisterCreatedObjectUndo(existing, "Create ProtagonistAttackController");
        Debug.Log("[Salinlahi] Added ProtagonistAttackController.");
        return existing;
    }

    private static void ConfigureAttackController(ProtagonistAttackController controller)
    {
        SerializedObject serialized = new(controller);
        
        // Load and assign slash VFX prefab
        GameObject slashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlashVfxPrefabPath);
        if (slashPrefab != null)
        {
            serialized.FindProperty("_slashVfxPrefab").objectReferenceValue = slashPrefab;
            Debug.Log("[Salinlahi] Assigned slash VFX prefab.");
        }
        else
        {
            Debug.LogWarning("[Salinlahi] Could not load slash VFX prefab.");
        }

        // Ensure pool size is reasonable
        SerializedProperty poolSize = serialized.FindProperty("_poolSize");
        if (poolSize.intValue <= 0)
        {
            poolSize.intValue = 3;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    [MenuItem("Salinlahi/Protagonist/Validate Protagonist Setup")]
    public static void ValidateSetup()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("=== Protagonist System Validation ===\n");

        // Check ProtagonistManager
        ProtagonistManager manager = Object.FindFirstObjectByType<ProtagonistManager>();
        if (manager == null)
        {
            report.AppendLine("❌ ProtagonistManager: NOT FOUND");
        }
        else
        {
            report.AppendLine("✅ ProtagonistManager: Found");
            if (manager.ProtagonistTransform != null)
            {
                report.AppendLine($"   Current protagonist: {manager.ProtagonistTransform.name}");
            }
            else
            {
                report.AppendLine("   Current protagonist: None (will create on play)");
            }
        }

        // Check AttackController
        ProtagonistAttackController attackController = Object.FindFirstObjectByType<ProtagonistAttackController>();
        if (attackController == null)
        {
            report.AppendLine("❌ ProtagonistAttackController: NOT FOUND");
        }
        else
        {
            report.AppendLine("✅ ProtagonistAttackController: Found");
        }

        // Check Prefabs
        report.AppendLine("\n=== Prefab Status ===");
        
        GameObject protagonistPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistPrefabPath);
        report.AppendLine(protagonistPrefab != null 
            ? "✅ Protagonist.prefab: Found" 
            : "❌ Protagonist.prefab: MISSING");

        GameObject slashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlashVfxPrefabPath);
        report.AppendLine(slashPrefab != null 
            ? "✅ ProtagonistSlashVfx.prefab: Found" 
            : "❌ ProtagonistSlashVfx.prefab: MISSING");

        GameObject managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistManagerPrefabPath);
        report.AppendLine(managerPrefab != null 
            ? "✅ [Manager] ProtagonistManager.prefab: Found" 
            : "❌ [Manager] ProtagonistManager.prefab: MISSING");

        // Check sprites
        report.AppendLine("\n=== Sprite Assets ===");
        string protagonistSpritePath = "Assets/Art/Characters/Protagonist";
        if (System.IO.Directory.Exists(protagonistSpritePath))
        {
            string[] sprites = System.IO.Directory.GetFiles(protagonistSpritePath, "*.png");
            report.AppendLine($"Found {sprites.Length} protagonist sprite(s)");
            foreach (string sprite in sprites)
            {
                report.AppendLine($"   - {System.IO.Path.GetFileName(sprite)}");
            }
        }
        else
        {
            report.AppendLine("❌ Protagonist sprite folder not found");
        }

        report.AppendLine("\n=== Recommendation ===");
        if (manager == null || attackController == null)
        {
            report.AppendLine("Run 'Configure Protagonist in Gameplay' to set up the system.");
        }
        else if (protagonistPrefab == null || slashPrefab == null)
        {
            report.AppendLine("Prefabs are missing. Check the prefab files exist.");
        }
        else
        {
            report.AppendLine("System looks good! Test by playing Level 1.");
        }

        EditorUtility.DisplayDialog("Protagonist Validation", report.ToString(), "OK");
        Debug.Log("[Salinlahi] Protagonist validation complete.");
    }
}
