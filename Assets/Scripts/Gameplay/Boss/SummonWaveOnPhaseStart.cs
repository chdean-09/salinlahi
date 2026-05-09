using System.Collections.Generic;
using UnityEngine;

// Spawns a mini-wave of adds during specified active phases (distinct from
// the post-phase intermission, which is owned by BossController).
// El Inquisidor uses this for his "summons Soldado reinforcements" ability.
[RequireComponent(typeof(BossController))]
public class SummonWaveOnPhaseStart : MonoBehaviour
{
    [Tooltip("Phase indices (0-based) that should trigger the summon. Phases not listed are skipped.")]
    [SerializeField] private List<int> _triggerOnPhaseIndices = new();

    [Tooltip("Wave config to spawn when one of the listed phases starts.")]
    [SerializeField] private WaveConfigSO _waveToSpawn;

    [Tooltip("Optional explicit reference. If left empty, this component finds the WaveSpawner via FindFirstObjectByType at Awake — required because prefabs cannot reference scene objects directly.")]
    [SerializeField] private WaveSpawner _spawner;

    private BossController _boss;

    private void Awake()
    {
        _boss = GetComponent<BossController>();
        if (_spawner == null)
            _spawner = FindFirstObjectByType<WaveSpawner>();
    }

    private void OnEnable()
    {
        EventBus.OnBossPhaseStarted += HandlePhaseStarted;
    }

    private void OnDisable()
    {
        EventBus.OnBossPhaseStarted -= HandlePhaseStarted;
    }

    private void HandlePhaseStarted(int phaseIndex)
    {
        if (_waveToSpawn == null) return;
        if (_spawner == null)
        {
            DebugLogger.LogWarning("SummonWaveOnPhaseStart: WaveSpawner reference not set — skipping summon.");
            return;
        }
        if (_triggerOnPhaseIndices == null || !_triggerOnPhaseIndices.Contains(phaseIndex))
            return;

        // Ignore if the boss is not actually targetable — defensive against
        // event-ordering surprises during unit tests.
        if (_boss != null && !_boss.IsTargetable) return;

        StartCoroutine(_spawner.SpawnWave(_waveToSpawn));
    }
}
