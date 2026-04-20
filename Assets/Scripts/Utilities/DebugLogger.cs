using System.Diagnostics;
using UnityEngine;

public static class DebugLogger
{
    [Conditional("ENABLE_SALINLAHI_LOG")]
    public static void Log(string message)
    {
        UnityEngine.Debug.Log($"[Salinlahi] {message}");
    }

    [Conditional("ENABLE_SALINLAHI_LOG")]
    public static void LogWarning(string message)
    {
        UnityEngine.Debug.LogWarning($"[Salinlahi] {message}");
    }

    public static void LogError(string message)
    {
        UnityEngine.Debug.LogError($"[Salinlahi] {message}");
    }
}
