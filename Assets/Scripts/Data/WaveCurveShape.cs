/// <summary>
/// The nine numbers a chapter wave curve is made of. Wave 1 is an opening wave with its own
/// values; waves 2..WaveCount interpolate linearly from the ramp's first to last values.
///
/// Deliberately a plain struct with no UnityEngine types, so <see cref="WaveCurveExpander"/> can be
/// exercised from EditMode tests without an asset, following the precedent of
/// <see cref="TargetTextSlotMap"/>, <see cref="DrawTargetResolver"/> and
/// <see cref="ActiveClueSelector"/>. <see cref="WaveCurveSO"/> is the inspector-facing carrier.
///
/// Why an opening wave and not a single first-to-last line: Level 1's authored intervals are
/// 6.0, 5.0, 4.5, 4.0, 3.5 and its delays 3.0 then 2.0. No straight line through five points
/// gives that; an opening wave plus a straight ramp over the other four gives it exactly, and the
/// golden test in WaveCurveGoldenTests holds this type to that.
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
