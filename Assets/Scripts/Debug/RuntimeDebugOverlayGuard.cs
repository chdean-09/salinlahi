using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Restricts the rendering debugger's touch toggle to developer builds.</summary>
internal static class RuntimeDebugOverlayGuard
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Configure()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        DebugManager.instance.displayRuntimeUI = false;
        DebugManager.instance.displayPersistentRuntimeUI = false;
        DebugManager.instance.enableRuntimeUI = false;
#endif
    }
}
