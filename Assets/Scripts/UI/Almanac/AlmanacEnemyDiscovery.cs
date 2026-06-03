/// <summary>
/// The SINGLE point the Almanac reads enemy discovery through. No other Almanac file names the
/// teammate's discovery types, so the project compiles standalone today.
/// </summary>
public static class AlmanacEnemyDiscovery
{
    public static bool IsDiscovered(EnemyDataSO data)
    {
        return EnemyDiscoveryProgress.HasDiscovered(data);
    }
}
