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

    [Tooltip("Enemy data assets that can spawn in this wave")]
    public List<EnemyDataSO> enemyTypesInWave;

    [Header("First Spawn Override")]
    [Tooltip("When enabled, spawn index 0 uses the override enemy and/or character before normal random selection resumes.")]
    public bool useFirstSpawnOverride;

    [Tooltip("Optional enemy type for the first spawn in this wave.")]
    public EnemyDataSO firstSpawnEnemyType;

    [Tooltip("Optional Baybayin character for the first spawn in this wave.")]
    public BaybayinCharacterSO firstSpawnCharacter;

    [Tooltip("Total enemies to spawn across this wave")]
    public int enemyCount = 5;

    [Tooltip("Seconds between each enemy spawn")]
    public float spawnInterval = 3f;

    [Tooltip("Seconds to wait before this wave begins")]
    public float waveStartDelay = 1f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(waveID))
            Debug.LogError($"WaveConfigSO '{name}' is missing waveID.", this);

        if (waveNumber <= 0)
            Debug.LogError($"WaveConfigSO '{name}' has invalid waveNumber {waveNumber}.", this);

        if (enemyCount < 0)
            Debug.LogError($"WaveConfigSO '{name}' has enemyCount < 0.", this);

        if (spawnInterval < 0f)
            Debug.LogError($"WaveConfigSO '{name}' has spawnInterval < 0.", this);

        if (waveStartDelay < 0f)
            Debug.LogError($"WaveConfigSO '{name}' has waveStartDelay < 0.", this);

        if (useFirstSpawnOverride && firstSpawnEnemyType == null && firstSpawnCharacter == null)
            Debug.LogError($"WaveConfigSO '{name}' has useFirstSpawnOverride enabled but no first spawn override references.", this);

        ValidateList(enemyTypesInWave, "enemyTypesInWave");
        ValidateList(charactersInWave, "charactersInWave");
    }

    private void ValidateList<T>(List<T> values, string fieldName) where T : Object
    {
        if (values == null)
            return;

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == null)
                Debug.LogError($"WaveConfigSO '{name}' has a missing reference in {fieldName}[{i}].", this);
        }
    }
#endif
}
