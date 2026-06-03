using System.Collections.Generic;
using UnityEngine;

public static class EnemyDiscoveryProgress
{
    public const string DiscoveredEnemyIDsKey = "salinlahi.discovery.enemy_ids";

    public static bool HasDiscovered(EnemyDataSO data)
    {
        string enemyID = NormalizeEnemyID(data);
        if (enemyID == null)
            return false;

        return LoadDiscoveredIDs().Contains(enemyID);
    }

    public static bool TryMarkDiscovered(EnemyDataSO data, out string enemyID)
    {
        enemyID = NormalizeEnemyID(data);
        if (enemyID == null)
            return false;

        HashSet<string> discovered = LoadDiscoveredIDs();
        if (!discovered.Add(enemyID))
            return false;

        SaveDiscoveredIDs(discovered);
        return true;
    }

    public static void ClearAllDiscovered()
    {
        PlayerPrefs.DeleteKey(DiscoveredEnemyIDsKey);
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
    public static void ResetForTests()
    {
        ClearAllDiscovered();
    }
#endif

    private static string NormalizeEnemyID(EnemyDataSO data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.enemyID))
            return null;

        return data.enemyID.Trim().ToLowerInvariant();
    }

    private static HashSet<string> LoadDiscoveredIDs()
    {
        HashSet<string> discovered = new HashSet<string>();
        string raw = PlayerPrefs.GetString(DiscoveredEnemyIDsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return discovered;

        string[] ids = raw.Split('\n');
        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i]?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(id))
                discovered.Add(id);
        }

        return discovered;
    }

    private static void SaveDiscoveredIDs(HashSet<string> discovered)
    {
        List<string> sorted = new List<string>(discovered);
        sorted.Sort(System.StringComparer.Ordinal);
        PlayerPrefs.SetString(DiscoveredEnemyIDsKey, string.Join("\n", sorted));
        PlayerPrefs.Save();
    }
}
