public interface ILegacyProgressSource
{
    bool HasKey(string key);
    int GetInt(string key, int defaultValue);
    float GetFloat(string key, float defaultValue);
    string GetString(string key, string defaultValue);
}

public sealed class PlayerPrefsLegacyProgressSource : ILegacyProgressSource
{
    public bool HasKey(string key) => UnityEngine.PlayerPrefs.HasKey(key);
    public int GetInt(string key, int defaultValue) => UnityEngine.PlayerPrefs.GetInt(key, defaultValue);
    public float GetFloat(string key, float defaultValue) => UnityEngine.PlayerPrefs.GetFloat(key, defaultValue);
    public string GetString(string key, string defaultValue) => UnityEngine.PlayerPrefs.GetString(key, defaultValue);
}

/// <summary>
/// Empty migration source used by isolated Editor QA runs. QA must not import a player's
/// legacy progress into its temporary in-memory campaign.
/// </summary>
public sealed class EmptyLegacyProgressSource : ILegacyProgressSource
{
    public static readonly EmptyLegacyProgressSource Instance = new EmptyLegacyProgressSource();

    private EmptyLegacyProgressSource() { }

    public bool HasKey(string key) => false;
    public int GetInt(string key, int defaultValue) => defaultValue;
    public float GetFloat(string key, float defaultValue) => defaultValue;
    public string GetString(string key, string defaultValue) => defaultValue;
}
