using System;
using System.Collections.Generic;

/// <summary>
/// Expands a <see cref="WaveCurveShape"/> against a level roster into the ordered
/// <see cref="WaveDefinition"/> list every existing consumer reads (WaveManager, LevelPhasePlan,
/// CampaignConfigValidator, LevelRoster). Materialising a concrete list is the point: those
/// consumers partition and index waves by count and must not learn a lazy abstraction.
///
/// Pure and static, System.Math only. Every generated wave is a plain pacing envelope: never an
/// intermission, and carrying the whole roster, which is the same shape
/// <c>WaveManager.BuildOverflowWave</c> already produces when the authored budget runs out.
/// </summary>
public static class WaveCurveExpander
{
    /// <summary>Asset entry point. A null curve expands to nothing, so a level with neither
    /// authored waves nor a curve reads as "no waves" exactly as it does today.</summary>
    public static List<WaveDefinition> Expand(
        WaveCurveSO curve,
        IReadOnlyList<BaybayinCharacterSO> characters,
        IReadOnlyList<EnemyDataSO> enemyTypes)
    {
        if (curve == null)
            return new List<WaveDefinition>();

        return Expand(curve.ToShape(), characters, enemyTypes);
    }

    public static List<WaveDefinition> Expand(
        WaveCurveShape shape,
        IReadOnlyList<BaybayinCharacterSO> characters,
        IReadOnlyList<EnemyDataSO> enemyTypes)
    {
        var waves = new List<WaveDefinition>();
        int count = Math.Max(0, shape.WaveCount);

        for (int i = 0; i < count; i++)
        {
            waves.Add(new WaveDefinition
            {
                isIntermissionWave = false,
                characters = CopyNonNull(characters),
                enemyTypes = CopyNonNull(enemyTypes),
                enemyCount = EnemyCountAt(shape, i),
                spawnInterval = SpawnIntervalAt(shape, i),
                waveStartDelay = i == 0 ? shape.OpeningWaveStartDelay : shape.RampWaveStartDelay,
            });
        }

        return waves;
    }

    /// <summary>Opening count at index 0; otherwise the ramp, rounded half away from zero.</summary>
    public static int EnemyCountAt(in WaveCurveShape shape, int waveIndex)
    {
        if (waveIndex <= 0)
            return shape.OpeningEnemyCount;

        double t = RampT(shape.WaveCount, waveIndex);
        double value = shape.RampFirstEnemyCount
            + (shape.RampLastEnemyCount - shape.RampFirstEnemyCount) * t;
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    /// <summary>Opening interval at index 0; otherwise linear along the ramp.</summary>
    public static float SpawnIntervalAt(in WaveCurveShape shape, int waveIndex)
    {
        if (waveIndex <= 0)
            return shape.OpeningSpawnInterval;

        double t = RampT(shape.WaveCount, waveIndex);
        return (float)(shape.RampFirstSpawnInterval
            + (shape.RampLastSpawnInterval - shape.RampFirstSpawnInterval) * t);
    }

    /// <summary>
    /// 0 at the first ramp wave (index 1), 1 at the last (index WaveCount - 1). A curve with a
    /// single ramp wave has no line to walk, so that wave takes the ramp's last values.
    /// </summary>
    private static double RampT(int waveCount, int waveIndex)
    {
        int rampWaves = waveCount - 1;
        if (rampWaves <= 1)
            return 1.0;

        return (waveIndex - 1) / (double)(rampWaves - 1);
    }

    private static List<T> CopyNonNull<T>(IReadOnlyList<T> source) where T : class
    {
        var copy = new List<T>();
        if (source == null)
            return copy;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                copy.Add(source[i]);
        }

        return copy;
    }
}
