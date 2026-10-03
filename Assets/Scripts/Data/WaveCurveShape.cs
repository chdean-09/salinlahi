/// <summary>
/// The nine numbers a chapter wave curve is made of. Wave 1 is an opening wave with its own
/// values; waves 2..WaveCount interpolate linearly from the ramp's first to last values.
///
/// Deliberately a plain struct with no UnityEngine types, so <see cref="WaveCurveExpander"/> can be
/// exercised from EditMode tests without an asset, following the precedent of
/// <see cref="TargetTextSlotMap"/>, <see cref="DrawTargetResolver"/> and
/// <see cref="ActiveClueSelector"/>. <see cref="WaveCurveSO"/> is the inspector-facing carrier.
///
/// The opening wave has its own cadence; later waves use the authored ramp. This lets the three-
/// and five-wave Ugat curves share one expansion rule.
/// </summary>
[System.Serializable]
public struct WaveCurveShape
{
    public int WaveCount;

    public int OpeningEnemyCount;
    public float OpeningSpawnInterval;
    public float OpeningWaveStartDelay;

    public int RampFirstEnemyCount;
    public int RampLastEnemyCount;
    public float RampFirstSpawnInterval;
    public float RampLastSpawnInterval;
    public float RampWaveStartDelay;
}
