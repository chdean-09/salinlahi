using System.Collections;
using UnityEngine;

// Plays a summon animation on the boss's SpriteRenderer and spawns
// 2-3 minions per tick at the boss's CURRENT world position (so movement
// patterns gain mechanical meaning). Stateless — invoked by BossController
// only; no event subscriptions.
[RequireComponent(typeof(BossController))]
public class BossSummonTicker : MonoBehaviour
{
    [Header("Visual Tell")]
    [SerializeField] private Sprite[] _summonFrames;
    [SerializeField] private float _summonAnimationFps = 8f;
    [SerializeField] private float _holdLastFrameDuration = 0.3f;

    private Enemy _enemy;
    private SpriteRenderer _renderer;

    // Mirrors EnemyHurtFeedback.IsPlayingHurtAnimation. Enemy.AdvanceWalkAnimation
    // reads this to suppress the walk loop while the tell is playing.
    public bool IsPlayingSummonAnimation { get; private set; }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _renderer = GetComponent<SpriteRenderer>();
    }

    public IEnumerator PlayTickAndSpawn(BossPhase phase, BossConfigSO config, WaveSpawner spawner)
    {
        if (phase == null || spawner == null)
            yield break;

        // ---- Play tell (if frames configured) ----
        IsPlayingSummonAnimation = true;
        if (_summonFrames != null && _summonFrames.Length > 0 && _renderer != null)
        {
            float fps = _summonAnimationFps > 0f ? _summonAnimationFps : 8f;
            float frameDur = 1f / fps;
            for (int idx = 0; idx < _summonFrames.Length; idx++)
            {
                Sprite frame = _summonFrames[idx];
                if (frame != null) _renderer.sprite = frame;
                float t = 0f;
                while (t < frameDur) { yield return null; t += Time.deltaTime; }
            }
            if (_holdLastFrameDuration > 0f)
                yield return new WaitForSeconds(_holdLastFrameDuration);
        }

        // ---- Stream spawns one at a time ----
        int min = Mathf.Max(0, phase.minionsPerSummonMin);
        int max = Mathf.Max(min, phase.minionsPerSummonMax);
        int count = Random.Range(min, max + 1);
        float perSpawnDelay = Mathf.Max(0f, phase.delayBetweenMinions);

        for (int n = 0; n < count; n++)
        {
            EnemyDataSO data = PickEnemyType(phase, config);
            if (data != null)
            {
                Enemy summon = spawner.SpawnEnemy(data);
                if (summon != null)
                {
                    // Override the random-X position with boss.position ± summonSpawnRange.
                    Vector3 origin = transform.position;
                    float dx = Random.Range(-phase.summonSpawnRange.x, phase.summonSpawnRange.x);
                    float dy = Random.Range(-phase.summonSpawnRange.y, phase.summonSpawnRange.y);
                    float spawnX = origin.x + dx;
                    float spawnY = origin.y + dy;

                    // Hard cap to keep summons on-screen even when the boss drifts toward the edge.
                    if (config != null && config.summonHorizontalBounds.y > config.summonHorizontalBounds.x)
                        spawnX = Mathf.Clamp(spawnX, config.summonHorizontalBounds.x, config.summonHorizontalBounds.y);

                    summon.transform.position = new Vector3(spawnX, spawnY, 0f);

                    // Render summons above the boss so they don't disappear behind its sprite
                    // when their spawn position overlaps. Enemy.OnEnable resets this on pool
                    // reuse, so normal wave spawns are unaffected.
                    SpriteRenderer summonRenderer = summon.GetComponent<SpriteRenderer>();
                    if (summonRenderer != null)
                        summonRenderer.sortingOrder = RenderOrder.BossSummon;

                    // Assign a random allowed character so the minion is defeatable.
                    BaybayinCharacterSO character = PickAllowedCharacter();
                    if (character != null)
                        summon.AssignCharacter(character);
                }
            }

            // Stagger between spawns. Skip the wait after the LAST spawn — the
            // post-stream pose tail below covers the "hold pose" beat.
            if (n < count - 1 && perSpawnDelay > 0f)
                yield return new WaitForSeconds(perSpawnDelay);
        }

        // ---- Post-stream pose tail ----
        // Hold the cast-pose sprite (last frame of _summonFrames) for a beat
        // after the final spawn before returning the boss to its walk loop.
        if (_holdLastFrameDuration > 0f)
            yield return new WaitForSeconds(_holdLastFrameDuration);

        if (_enemy != null) _enemy.ResetWalkAnimation();
        IsPlayingSummonAnimation = false;
    }

    private static EnemyDataSO PickEnemyType(BossPhase phase, BossConfigSO config)
    {
        if (phase.summonEnemyTypes != null && phase.summonEnemyTypes.Count > 0)
        {
            int idx = Random.Range(0, phase.summonEnemyTypes.Count);
            return phase.summonEnemyTypes[idx];
        }
        if (config != null && config.fallbackEnemyTypes != null && config.fallbackEnemyTypes.Count > 0)
        {
            int idx = Random.Range(0, config.fallbackEnemyTypes.Count);
            return config.fallbackEnemyTypes[idx];
        }
        DebugLogger.LogWarning("BossSummonTicker: No enemy types configured for phase or fallback. Skipping summon.");
        return null;
    }

    private static BaybayinCharacterSO PickAllowedCharacter()
    {
        LevelConfigSO level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
        if (level == null || level.allowedCharacters == null || level.allowedCharacters.Count == 0)
            return null;
        int idx = Random.Range(0, level.allowedCharacters.Count);
        return level.allowedCharacters[idx];
    }
}
