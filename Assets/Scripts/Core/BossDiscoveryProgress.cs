using System.Collections.Generic;
using UnityEngine;

public static class BossDiscoveryProgress
{
    private const string Key = "salinlahi.almanac.boss_ids";

    public static bool HasDiscovered(BossConfigSO config)
    {
        string id = Normalize(config);
        if (string.IsNullOrEmpty(id)) return false;
        if (UsesRevisedProgress())
            return SaveManager.Instance.Repository.IsBossDiscovered(config.bossID);
        return Load().Contains(id);
    }

    public static bool TryMarkDiscovered(BossConfigSO config, out string bossID)
    {
        bossID = Normalize(config);
        if (string.IsNullOrEmpty(bossID)) return false;

        if (UsesRevisedProgress())
            return SaveManager.Instance.Repository.TryDiscoverBoss(config.bossID);
        if (SaveManager.Instance != null && SaveManager.Instance.Mode == SaveManagerMode.RevisedBlocked)
            return false;

        HashSet<string> set = Load();
        if (!set.Add(bossID)) return false;

        Save(set);
        return true;
    }

    public static void ClearAllDiscovered()
    {
        if (UsesRevisedProgress())
            return;
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
    public static void ResetForTests() => ClearAllDiscovered();
#endif

    private static string Normalize(BossConfigSO config)
    {
        if (config == null || string.IsNullOrWhiteSpace(config.bossID)) return null;
        return config.bossID.Trim().ToLowerInvariant();
    }

    private static bool UsesRevisedProgress()
    {
        return SaveManager.Instance != null && SaveManager.Instance.Mode == SaveManagerMode.RevisedReady &&
            SaveManager.Instance.Repository != null;
    }

    private static HashSet<string> Load()
    {
        var set = new HashSet<string>();
        string raw = PlayerPrefs.GetString(Key, string.Empty);
        if (string.IsNullOrEmpty(raw)) return set;
        foreach (string line in raw.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0) set.Add(trimmed);
        }
        return set;
    }

    private static void Save(HashSet<string> set)
    {
        PlayerPrefs.SetString(Key, string.Join("\n", set));
        PlayerPrefs.Save();
    }
}
