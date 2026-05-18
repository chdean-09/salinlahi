using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Salin93OnboardingSceneBuilder
{
    private const string GameplayScenePath = "Assets/_Scenes/Gameplay.unity";
    private const string GlyphPresenterName = "BaybayinGlyphPresenter";

    private static readonly string[] EnemyPrefabPaths =
    {
        "Assets/Prefabs/Enemies/[Enemy] Soldado.prefab",
        "Assets/Prefabs/Enemies/[Enemy] Soldier.prefab",
        "Assets/Prefabs/Enemies/[Enemy] Sprinter.prefab",
        "Assets/Prefabs/Enemies/[Enemy] Shielded.prefab",
        "Assets/Prefabs/Enemies/[Enemy] Kisha.prefab",
        "Assets/Prefabs/Enemies/[Enemy] Maestro.prefab",
        "Assets/Prefabs/Enemies/[Enemy] Pensionado.prefab",
        "Assets/Prefabs/Enemies/[Enemy] General.prefab",
        "Assets/Prefabs/Enemies/[Enemy] Kempei.prefab",
        "Assets/Prefabs/Enemies/[Enemy] Heitai.prefab",
        "Assets/Prefabs/Enemies/[Enemy] Shokan.prefab"
    };

    [MenuItem("Salinlahi/Scene Builders/SALIN-93 Apply Onboarding Flow")]
    public static void Apply()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

        int changes = 0;
        changes += EnsureExternalWaveStart();
        changes += ConfigureWorldIntro();
        changes += HidePrototypeLabels(scene);
        changes += EnsureEnemyGlyphPresenters();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Salinlahi] SALIN-93 onboarding scene builder complete. Applied {changes} changes.");
    }

    private static int EnsureExternalWaveStart()
    {
        WaveManager waveManager = Object.FindFirstObjectByType<WaveManager>();
        if (waveManager == null)
        {
            Debug.LogError("[Salinlahi] SALIN-93 builder: Gameplay scene is missing WaveManager.");
            return 0;
        }

        SerializedObject serialized = new(waveManager);
        SerializedProperty waitForExternalStart = serialized.FindProperty("_waitForExternalStart");
        if (waitForExternalStart == null || waitForExternalStart.boolValue)
            return 0;

        waitForExternalStart.boolValue = true;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(waveManager);
        return 1;
    }

    private static int ConfigureWorldIntro()
    {
        Level1WorldIntroController intro = Object.FindFirstObjectByType<Level1WorldIntroController>(FindObjectsInactive.Include);
        if (intro == null)
        {
            Debug.LogError("[Salinlahi] SALIN-93 builder: Gameplay scene is missing Level1WorldIntroController.");
            return 0;
        }

        int changes = 0;
        SerializedObject serialized = new(intro);
        changes += SetString(serialized, "_objectiveLine", "Defend the Shrine.");
        changes += SetString(serialized, "_threatCueLine", "Enemies incoming.");
        changes += SetBool(serialized, "_hidePlaceholderLabels", true);
        changes += SetFloat(serialized, "_threatCueHoldSeconds", 0.75f);
        serialized.ApplyModifiedProperties();

        if (!intro.IsConfigured)
        {
            Debug.LogError(
                "[Salinlahi] SALIN-93 builder: Level1WorldIntroController is not fully configured. "
                + "Assign intro group, objective text, protagonist, and shrine references in Gameplay.");
        }

        EditorUtility.SetDirty(intro);
        return changes;
    }

    private static int HidePrototypeLabels(Scene scene)
    {
        int changes = 0;
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            TextMeshProUGUI[] labels = roots[i].GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int j = 0; j < labels.Length; j++)
            {
                TextMeshProUGUI label = labels[j];
                if (label == null || !IsPrototypeLabel(label.text) || !label.gameObject.activeSelf)
                    continue;

                label.gameObject.SetActive(false);
                EditorUtility.SetDirty(label.gameObject);
                changes++;
            }
        }

        return changes;
    }

    private static int EnsureEnemyGlyphPresenters()
    {
        int changes = 0;

        for (int i = 0; i < EnemyPrefabPaths.Length; i++)
        {
            string path = EnemyPrefabPaths[i];
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
                continue;

            try
            {
                Enemy enemy = root.GetComponent<Enemy>();
                if (enemy == null)
                {
                    Debug.LogWarning($"[Salinlahi] SALIN-93 builder: Prefab '{path}' has no Enemy component.");
                    continue;
                }

                EnemyGlyphPresenter presenter = root.GetComponentInChildren<EnemyGlyphPresenter>(true);
                if (presenter == null)
                {
                    GameObject presenterObject = new(GlyphPresenterName);
                    presenterObject.transform.SetParent(root.transform, false);
                    presenterObject.transform.localPosition = new Vector3(0f, 0.85f, -0.05f);
                    presenterObject.transform.localScale = Vector3.one;
                    presenter = presenterObject.AddComponent<EnemyGlyphPresenter>();
                    changes++;
                }

                SerializedObject serializedEnemy = new(enemy);
                SerializedProperty glyphPresenter = serializedEnemy.FindProperty("_glyphPresenter");
                if (glyphPresenter != null && glyphPresenter.objectReferenceValue != presenter)
                {
                    glyphPresenter.objectReferenceValue = presenter;
                    serializedEnemy.ApplyModifiedProperties();
                    changes++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return changes;
    }

    private static int SetString(SerializedObject serialized, string propertyName, string value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.stringValue == value)
            return 0;

        property.stringValue = value;
        return 1;
    }

    private static int SetBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.boolValue == value)
            return 0;

        property.boolValue = value;
        return 1;
    }

    private static int SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || Mathf.Approximately(property.floatValue, value))
            return 0;

        property.floatValue = value;
        return 1;
    }

    private static bool IsPrototypeLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim().ToUpperInvariant();
        return normalized == "PROTAGONIST"
            || normalized == "SHRINE"
            || normalized == "ENEMY CROSSING LINE";
    }
}
