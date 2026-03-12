using UnityEngine;

public static class DebugLogger
{
    private static bool _enabled = true;

    public static void Log(string message)
    {
        if (_enabled) Debug.Log($"[Salinlahi] {message}");
    }

    public static void LogWarning(string message)
    {
        if (_enabled) Debug.LogWarning($"[Salinlahi] {message}");
    }

    public static void LogError(string message)
    {
        // Errors always print regardless of toggle
        Debug.LogError($"[Salinlahi] {message}");
    }

    public static void SetEnabled(bool enabled) => _enabled = enabled;
}
