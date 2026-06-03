/// <summary>
/// The SINGLE point the Almanac reads enemy discovery through. No other Almanac file names the
/// teammate's discovery types, so the project compiles standalone today.
/// </summary>
public static class AlmanacEnemyDiscovery
{
    public static bool IsDiscovered(EnemyDataSO data)
    {
        // TEMP: the teammate's EnemyDiscoveryProgress is not merged yet (they are blocked on a bug).
        // Treat all non-boss enemies as discovered so the grid populates during development/demo.
        // INTEGRATION: replace the body with:
        //     return EnemyDiscoveryProgress.HasDiscovered(data);
        return data != null;
    }

    public static bool IsBossDiscovered(BossConfigSO config) =>
        BossDiscoveryProgress.HasDiscovered(config);
}
