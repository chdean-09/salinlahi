using System.Collections.Generic;
using UnityEngine;

public static class CharacterUnlockProgress
{
    private const string Key = "salinlahi.almanac.character_ids";

    public static bool HasUnlocked(BaybayinCharacterSO data)
    {
        if (data == null) return false;
        if (UsesRevisedProgress())
            return ContentIdentity.IsCanonical(data.stableId) &&
                SaveManager.Instance.Repository.IsSymbolUnlocked(data.stableId);

        string id = Normalize(data);
        return !string.IsNullOrEmpty(id) && Load().Contains(id);
    }

    public static bool TryMarkUnlocked(BaybayinCharacterSO data, out string characterID)
    {
        characterID = null;
        if (data == null) return false;

        if (UsesRevisedProgress())
        {
            // Assign before returning: an unassigned out parameter is CS0177, and both callers
            // (CharacterUnlockRevealController.cs, ProgressManagerTester.cs) pass `out _`.
            characterID = data.stableId;
            return ContentIdentity.IsCanonical(data.stableId) &&
                SaveManager.Instance.Repository.TryUnlockSymbol(data.stableId);
        }

        characterID = Normalize(data);
        if (string.IsNullOrEmpty(characterID)) return false;
        if (SaveManager.Instance != null && SaveManager.Instance.Mode == SaveManagerMode.RevisedBlocked)
            return false;

        HashSet<string> set = Load();
        if (!set.Add(characterID)) return false;

        Save(set);
        return true;
    }

    public static void ClearAllUnlocked()
    {
        if (UsesRevisedProgress())
            return;
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
    public static void ResetForTests() => ClearAllUnlocked();
#endif

    private static string Normalize(BaybayinCharacterSO data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.characterID)) return null;
        return data.characterID.Trim().ToLowerInvariant();
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
