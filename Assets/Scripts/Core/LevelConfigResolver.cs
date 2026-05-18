using UnityEngine;

public static class LevelConfigResolver
{
    public static LevelConfigSO ResolveSelected(
        LevelConfigSO gameManagerLevel,
        LevelConfigSO[] registry,
        LevelConfigSO inspectorFallback)
    {
        int selectedLevel = PlayerPrefs.GetInt(ProgressManager.SelectedLevelKey, 1);
        return Resolve(selectedLevel, gameManagerLevel, registry, inspectorFallback);
    }

    public static LevelConfigSO Resolve(
        int levelNumber,
        LevelConfigSO gameManagerLevel,
        LevelConfigSO[] registry,
        LevelConfigSO inspectorFallback)
    {
        if (gameManagerLevel != null && gameManagerLevel.levelNumber == levelNumber)
            return gameManagerLevel;

        LevelConfigSO registryConfig = ResolveFromRegistry(levelNumber, registry);
        if (registryConfig != null)
            return registryConfig;

        LevelConfigSO resourcesConfig = Resources.Load<LevelConfigSO>($"LevelConfigs/Level{levelNumber}_Config");
        if (resourcesConfig != null)
            return resourcesConfig;

        if (inspectorFallback != null)
        {
            DebugLogger.LogWarning(
                $"LevelConfigResolver: Could not resolve Level {levelNumber}; "
                + $"using inspector fallback '{inspectorFallback.name}'.");
            return inspectorFallback;
        }

        DebugLogger.LogError($"LevelConfigResolver: Could not resolve Level {levelNumber}.");
        return null;
    }

    private static LevelConfigSO ResolveFromRegistry(int levelNumber, LevelConfigSO[] registry)
    {
        if (registry == null || registry.Length == 0)
            return null;

        for (int i = 0; i < registry.Length; i++)
        {
            LevelConfigSO candidate = registry[i];
            if (candidate != null && candidate.levelNumber == levelNumber)
                return candidate;
        }

        int index = levelNumber - 1;
        if (index >= 0 && index < registry.Length)
            return registry[index];

        return null;
    }
}
