using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WaveConfig", menuName = "Salinlahi/Wave Config")]
public class WaveConfigSO : ScriptableObject
{
    [Header("Identity")]
    public string waveID;
    public int waveNumber;

    [Header("Spawn Settings")]
    [Tooltip("Baybayin characters that can appear in this wave")]
    public List<BaybayinCharacterSO> charactersInWave;

    [Tooltip("Total enemies to spawn across this wave")]
    public int enemyCount = 5;

    [Tooltip("Seconds between each enemy spawn")]
    public float spawnInterval = 3f;

    [Tooltip("Seconds to wait before this wave begins")]
    public float waveStartDelay = 1f;
}
