using System;
using System.Collections.Generic;

/// <summary>
/// Shared, read-only state for Nawalang Mukha's identity-loss effect.
/// Sources are reference-counted so pooled enemies and overlapping effects are safe.
/// </summary>
public static class NameLossEffectRegistry
{
    private static readonly HashSet<object> Sources = new HashSet<object>();

    public static bool IsActive => Sources.Count > 0;
    public static event Action Changed;

    public static void Register(object source)
    {
        if (source != null && Sources.Add(source))
            Changed?.Invoke();
    }

    public static void Unregister(object source)
    {
        if (source != null && Sources.Remove(source))
            Changed?.Invoke();
    }

#if UNITY_INCLUDE_TESTS
    public static void ResetForTests()
    {
        if (Sources.Count == 0)
            return;

        Sources.Clear();
        Changed?.Invoke();
    }
#endif
}
