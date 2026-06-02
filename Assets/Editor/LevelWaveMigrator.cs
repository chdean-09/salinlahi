using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class LevelWaveMigrator
{
    [MenuItem("Salinlahi/Migration/Force Reserialize Level Assets")]
    public static void ReserializeLevels()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelConfigSO");
        var paths = new List<string>();
        foreach (string guid in guids)
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        AssetDatabase.ForceReserializeAssets(paths);
        Debug.Log($"Reserialized {paths.Count} level asset(s).");
    }
}
