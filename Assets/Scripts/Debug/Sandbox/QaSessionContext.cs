using UnityEngine;

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
#endif

/// <summary>
/// Editor-only QA state. Progress values are redirected to a QA-scoped EditorPrefs profile while
/// a QA level is running, and campaign saves are backed by in-memory storage. The player's
/// PlayerPrefs and save files are never used as the QA write target.
/// </summary>
public static class QaSessionContext
{
#if UNITY_EDITOR
    private const string Root = "Salinlahi.QA.Session.";
    private const string ActiveKey = Root + "Active";
    private const string ProfileKey = Root + "ProfileId";
    private const string SelectedLevelPathKey = Root + "SelectedLevelPath";
    private const string SelectedLevelNumberKey = Root + "SelectedLevelNumber";
    private const string EventTraceKey = Root + "EventTrace";
    private const string TrackedKeysKey = Root + "TrackedPreferenceKeys";
    private const string PlayStartTimeKey = Root + "PlayStartTime";

    public static bool IsActive => EditorPrefs.GetBool(ActiveKey, false);
    public static string ProfileId => EditorPrefs.GetString(ProfileKey, string.Empty);
    public static string SelectedLevelPath => EditorPrefs.GetString(SelectedLevelPathKey, string.Empty);
    public static int SelectedLevelNumber => EditorPrefs.GetInt(SelectedLevelNumberKey, 0);
    public static string EventTrace => EditorPrefs.GetString(EventTraceKey, string.Empty);

    public static void StartNewCampaign()
    {
        EditorPrefs.SetString(ProfileKey, Guid.NewGuid().ToString("N"));
        EditorPrefs.SetString(TrackedKeysKey, string.Empty);
        EditorPrefs.SetString(EventTraceKey, string.Empty);
        EditorPrefs.DeleteKey(PlayStartTimeKey);
        EditorPrefs.SetBool(ActiveKey, false);
    }

    public static void BeginLevel(int levelNumber, string assetPath)
    {
        if (string.IsNullOrWhiteSpace(ProfileId))
            StartNewCampaign();

        EditorPrefs.SetInt(SelectedLevelNumberKey, levelNumber);
        EditorPrefs.SetString(SelectedLevelPathKey, assetPath ?? string.Empty);
        EditorPrefs.SetString(EventTraceKey, string.Empty);
        EditorPrefs.DeleteKey(PlayStartTimeKey);
        EditorPrefs.SetBool(ActiveKey, true);
    }

    public static void BeginPlayTiming()
    {
        if (!IsActive)
            return;

        EditorPrefs.SetString(PlayStartTimeKey,
            EditorApplication.timeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        Record("session", $"begin level={SelectedLevelNumber} asset={SelectedLevelPath}");
    }

    public static void EndLevel()
    {
        EditorPrefs.SetBool(ActiveKey, false);
    }

    public static bool TryGetSelectedLevel(out LevelConfigSO level)
    {
        level = null;
        string path = SelectedLevelPath;
        if (!IsActive || string.IsNullOrWhiteSpace(path))
            return false;

        level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(path);
        return level != null;
    }

    public static void Record(string category, string details)
    {
        if (!IsActive)
            return;

        double playStart = 0d;
        double.TryParse(EditorPrefs.GetString(PlayStartTimeKey, string.Empty),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out playStart);
        double elapsed = playStart > 0d
            ? Math.Max(0d, EditorApplication.timeSinceStartup - playStart)
            : 0d;

        string line = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0:O}\t{1:F3}\t{2}\t{3}",
            DateTime.UtcNow,
            elapsed,
            Clean(category),
            Clean(details));
        string existing = EventTrace;
        EditorPrefs.SetString(EventTraceKey, string.IsNullOrEmpty(existing)
            ? line
            : existing + "\n" + line);
    }

    public static void RecordSpawn(Enemy enemy)
    {
        if (enemy == null)
            return;

        string enemyType = enemy.Data != null
            ? (string.IsNullOrWhiteSpace(enemy.Data.displayName) ? enemy.Data.enemyID : enemy.Data.displayName)
            : enemy.name;
        string glyph = enemy.Character != null ? enemy.Character.characterID : "UNKNOWN";
        Vector3 position = enemy.transform.position;
        Record("spawn", $"type={enemyType}; glyph={glyph}; world=({position.x:F2},{position.y:F2}); frame={Time.frameCount}");
    }

    public static string PreferencePrefix => Root + "Profile." + ProfileId + ".";

    public static int GetInt(string key, int defaultValue)
    {
        return EditorPrefs.GetInt(PreferencePrefix + key, defaultValue);
    }

    public static string GetString(string key, string defaultValue)
    {
        return EditorPrefs.GetString(PreferencePrefix + key, defaultValue);
    }

    public static bool HasKey(string key)
    {
        return EditorPrefs.HasKey(PreferencePrefix + key);
    }

    public static void SetInt(string key, int value)
    {
        Track(key);
        EditorPrefs.SetInt(PreferencePrefix + key, value);
    }

    public static void SetString(string key, string value)
    {
        Track(key);
        EditorPrefs.SetString(PreferencePrefix + key, value ?? string.Empty);
    }

    public static void DeleteKey(string key)
    {
        Track(key);
        EditorPrefs.DeleteKey(PreferencePrefix + key);
    }

    public static void ClearCampaign()
    {
        string[] keys = EditorPrefs.GetString(TrackedKeysKey, string.Empty)
            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < keys.Length; i++)
            EditorPrefs.DeleteKey(PreferencePrefix + keys[i]);

        EditorPrefs.SetString(TrackedKeysKey, string.Empty);
        EditorPrefs.SetString(EventTraceKey, string.Empty);
        EditorPrefs.DeleteKey(PlayStartTimeKey);
        EditorPrefs.SetString(SelectedLevelPathKey, string.Empty);
        EditorPrefs.SetInt(SelectedLevelNumberKey, 0);
        EditorPrefs.SetString(ProfileKey, string.Empty);
        EditorPrefs.SetBool(ActiveKey, false);
    }

    private static void Track(string key)
    {
        string existing = EditorPrefs.GetString(TrackedKeysKey, string.Empty);
        if (Array.IndexOf(existing.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries), key) >= 0)
            return;

        EditorPrefs.SetString(TrackedKeysKey, string.IsNullOrEmpty(existing)
            ? key
            : existing + "\n" + key);
    }

    private static string Clean(string value)
    {
        return (value ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }
#endif
}

/// <summary>
/// PlayerPrefs facade for campaign and tutorial progress. During Editor QA it redirects reads and
/// writes to the current QA profile; outside QA it preserves the existing PlayerPrefs behavior.
/// </summary>
public static class ProgressPrefs
{
    public static int GetInt(string key, int defaultValue = 0)
    {
#if UNITY_EDITOR
        if (QaSessionContext.IsActive)
            return QaSessionContext.GetInt(key, defaultValue);
#endif
        return PlayerPrefs.GetInt(key, defaultValue);
    }

    public static string GetString(string key, string defaultValue = "")
    {
#if UNITY_EDITOR
        if (QaSessionContext.IsActive)
            return QaSessionContext.GetString(key, defaultValue);
#endif
        return PlayerPrefs.GetString(key, defaultValue);
    }

    public static bool HasKey(string key)
    {
#if UNITY_EDITOR
        if (QaSessionContext.IsActive)
            return QaSessionContext.HasKey(key);
#endif
        return PlayerPrefs.HasKey(key);
    }

    public static void SetInt(string key, int value)
    {
#if UNITY_EDITOR
        if (QaSessionContext.IsActive)
        {
            QaSessionContext.SetInt(key, value);
            return;
        }
#endif
        PlayerPrefs.SetInt(key, value);
    }

    public static void SetString(string key, string value)
    {
#if UNITY_EDITOR
        if (QaSessionContext.IsActive)
        {
            QaSessionContext.SetString(key, value);
            return;
        }
#endif
        PlayerPrefs.SetString(key, value);
    }

    public static void DeleteKey(string key)
    {
#if UNITY_EDITOR
        if (QaSessionContext.IsActive)
        {
            QaSessionContext.DeleteKey(key);
            return;
        }
#endif
        PlayerPrefs.DeleteKey(key);
    }

    public static void Save()
    {
#if UNITY_EDITOR
        if (QaSessionContext.IsActive)
            return;
#endif
        PlayerPrefs.Save();
    }
}
