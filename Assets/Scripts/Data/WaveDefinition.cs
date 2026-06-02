using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WaveDefinition
{
    [Tooltip("Set true for boss intermission waves.")]
    public bool isIntermissionWave;

    [Header("Spawn Settings")]
    [Tooltip("Baybayin characters that can appear in this wave (subset of the level roster).")]
    public List<BaybayinCharacterSO> characters = new();

    [Tooltip("Enemy data assets that can spawn in this wave (subset of the level roster).")]
    public List<EnemyDataSO> enemyTypes = new();

    [Tooltip("Total enemies to spawn across this wave.")]
    public int enemyCount = 5;

    [Tooltip("Seconds between each enemy spawn.")]
    public float spawnInterval = 3f;

    [Tooltip("Seconds to wait before this wave begins.")]
    public float waveStartDelay = 1f;
}
