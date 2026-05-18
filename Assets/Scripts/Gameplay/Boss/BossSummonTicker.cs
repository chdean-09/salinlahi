using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BossController))]
public class BossSummonTicker : MonoBehaviour
{
    public IEnumerator PlayTickAndSpawn(BossPhase phase, BossConfigSO config, WaveSpawner spawner)
    {
        if (phase == null || spawner == null)
            yield break;

        // Temporary: call SpawnEnemy once per tick so summon-cadence tests pass.
        // Full implementation replaces this in Task 6.
        EnemyDataSO data = null;
        if (phase.summonEnemyTypes != null && phase.summonEnemyTypes.Count > 0)
            data = phase.summonEnemyTypes[0];
        else if (config != null && config.fallbackEnemyTypes != null && config.fallbackEnemyTypes.Count > 0)
            data = config.fallbackEnemyTypes[0];

        if (data != null)
            spawner.SpawnEnemy(data);
    }
}
