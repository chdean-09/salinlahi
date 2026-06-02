using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelConfigSO))]
public class LevelConfigEditor : Editor
{
    private SerializedProperty _waves;

    private void OnEnable()
    {
        _waves = serializedObject.FindProperty("waves");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        LevelConfigSO level = (LevelConfigSO)target;

        // Default fields except the embedded waves list (drawn custom below).
        DrawPropertiesExcluding(serializedObject, "m_Script", "waves");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Waves", EditorStyles.boldLabel);

        for (int i = 0; i < _waves.arraySize; i++)
        {
            SerializedProperty wave = _waves.GetArrayElementAtIndex(i);
            DrawWave(level, wave, i);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Wave"))
            _waves.arraySize++;
        using (new EditorGUI.DisabledScope(_waves.arraySize == 0))
        {
            if (GUILayout.Button("Remove Last"))
                _waves.arraySize--;
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawWave(LevelConfigSO level, SerializedProperty wave, int index)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Wave {index + 1}", EditorStyles.boldLabel);
        if (GUILayout.Button("Up", GUILayout.Width(34)) && index > 0)
            _waves.MoveArrayElement(index, index - 1);
        if (GUILayout.Button("Down", GUILayout.Width(44)) && index < _waves.arraySize - 1)
            _waves.MoveArrayElement(index, index + 1);
        if (GUILayout.Button("X", GUILayout.Width(22)))
        {
            _waves.DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(wave.FindPropertyRelative("isIntermissionWave"));
        EditorGUILayout.PropertyField(wave.FindPropertyRelative("enemyCount"));
        EditorGUILayout.PropertyField(wave.FindPropertyRelative("spawnInterval"));
        EditorGUILayout.PropertyField(wave.FindPropertyRelative("waveStartDelay"));

        DrawToggleGrid("Characters", wave.FindPropertyRelative("characters"),
            ToObjects(level.allowedCharacters), o => ((BaybayinCharacterSO)o).name);
        DrawToggleGrid("Enemy Types", wave.FindPropertyRelative("enemyTypes"),
            ToObjects(level.allowedEnemyTypes), o => ((EnemyDataSO)o).name);

        EditorGUILayout.EndVertical();
    }

    // Renders the roster as checkboxes; checked = present in this wave's subset list.
    private static void DrawToggleGrid(string label, SerializedProperty subset,
        Object[] roster, System.Func<Object, string> nameOf)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        if (roster.Length == 0)
        {
            EditorGUILayout.HelpBox($"Level roster has no {label.ToLower()} yet.", MessageType.None);
            return;
        }

        foreach (Object entry in roster)
        {
            if (entry == null) continue;
            int existingIndex = IndexOf(subset, entry);
            bool wasOn = existingIndex >= 0;
            bool isOn = EditorGUILayout.ToggleLeft(nameOf(entry), wasOn);
            if (isOn && !wasOn)
            {
                subset.arraySize++;
                subset.GetArrayElementAtIndex(subset.arraySize - 1).objectReferenceValue = entry;
            }
            else if (!isOn && wasOn)
            {
                subset.DeleteArrayElementAtIndex(existingIndex);
            }
        }
    }

    private static int IndexOf(SerializedProperty list, Object value)
    {
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
                return i;
        return -1;
    }

    private static Object[] ToObjects<T>(System.Collections.Generic.List<T> list) where T : Object
    {
        if (list == null) return System.Array.Empty<Object>();
        Object[] result = new Object[list.Count];
        for (int i = 0; i < list.Count; i++) result[i] = list[i];
        return result;
    }
}
