using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class EnemyPool : Singleton<EnemyPool>
{
    [Serializable]
    private class EnemyPrefabRegistration
    {
        public string enemyID;
        public Enemy prefab;
        public int defaultCapacity = 10;
        public int maxSize = 20;
    }

    private sealed class PoolState
    {
        public string EnemyID;
        public Enemy Prefab;
        public int DefaultCapacity;
        public int MaxSize;
        public int CreatedCount;
        public IObjectPool<Enemy> Pool;
    }

    [Header("Prefab")]
    [SerializeField] private Enemy _enemyPrefab;

    [Header("Pool Size")]
    [SerializeField] private int _defaultCapacity = 10;
    [SerializeField] private int _maxSize = 20;

    [Header("Registered Enemy Prefabs")]
    [SerializeField] private List<EnemyPrefabRegistration> _registeredEnemyPrefabs = new();

    private readonly Dictionary<string, PoolState> _poolStatesByEnemyID = new();
    private readonly Dictionary<Enemy, PoolState> _owningPools = new();
    private readonly HashSet<Enemy> _checkedOutEnemies = new();
    private PoolState _defaultPoolState;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        _poolStatesByEnemyID.Clear();
        _owningPools.Clear();
        _checkedOutEnemies.Clear();
        _defaultPoolState = null;

        _defaultPoolState = BuildPoolState("default", _enemyPrefab, _defaultCapacity, _maxSize, true);

        foreach (EnemyPrefabRegistration registration in _registeredEnemyPrefabs)
        {
            if (registration == null)
                continue;

            if (string.IsNullOrWhiteSpace(registration.enemyID))
            {
                DebugLogger.LogWarning("EnemyPool: Skipping registered enemy pool with empty enemyID.");
                continue;
            }

            string key = registration.enemyID.Trim().ToLowerInvariant();
            if (_poolStatesByEnemyID.ContainsKey(key))
            {
                DebugLogger.LogWarning($"EnemyPool: Duplicate enemyID '{registration.enemyID}' ignored.");
                continue;
            }

            PoolState state = BuildPoolState(
                registration.enemyID,
                registration.prefab,
                registration.defaultCapacity,
                registration.maxSize,
                false
            );

            if (state != null)
                _poolStatesByEnemyID[key] = state;
        }
    }

    public bool IsCheckedOut(Enemy enemy)
    {
        return enemy != null && _checkedOutEnemies.Contains(enemy);
    }

    public Enemy Get(EnemyDataSO data)
    {
        if (data == null)
        {
            DebugLogger.LogError("EnemyPool.Get: EnemyDataSO is null.");
            return null;
        }

        PoolState state = ResolvePoolState(data);
        if (state == null)
        {
            DebugLogger.LogError($"EnemyPool.Get: No pool resolved for enemyID '{data.enemyID}'.");
            return null;
        }

        Enemy enemy = state.Pool.Get();
        if (enemy == null)
        {
            DebugLogger.LogError($"EnemyPool.Get: Pool returned null for enemyID '{state.EnemyID}'.");
            return null;
        }

        if (!_owningPools.TryGetValue(enemy, out PoolState owner))
        {
            _owningPools[enemy] = state;
            owner = state;
        }
        else if (owner != state)
        {
            DebugLogger.LogError(
                $"EnemyPool.Get: Ownership corruption for '{enemy.name}'. "
                + $"Expected '{state.EnemyID}', found '{owner.EnemyID}'. Destroying corrupted instance.");
            QuarantineAndDestroy(enemy, owner);
            return null;
        }

        if (!_checkedOutEnemies.Add(enemy))
        {
            DebugLogger.LogError($"EnemyPool.Get: Enemy '{enemy.name}' was checked out twice. Destroying corrupted instance.");
            QuarantineAndDestroy(enemy, owner);
            return null;
        }

        if (!enemy.Initialize(data))
        {
            Return(enemy);
            return null;
        }

        return enemy;
    }

    public void Return(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (!_owningPools.TryGetValue(enemy, out PoolState state) || state == null)
        {
            DebugLogger.LogWarning($"EnemyPool.Return: Enemy '{enemy.name}' has no owning pool. Ignoring return.");
            return;
        }

        if (!_checkedOutEnemies.Contains(enemy))
        {
            DebugLogger.LogWarning($"EnemyPool.Return: Enemy '{enemy.name}' was already returned. Ignoring duplicate return.");
            return;
        }

        _checkedOutEnemies.Remove(enemy);
        ActiveEnemyTracker.Instance?.Unregister(enemy);

        try
        {
            enemy.ResetForPool();
        }
        catch (Exception ex)
        {
            DebugLogger.LogError($"EnemyPool.Return: ResetForPool failed for '{enemy.name}': {ex.Message}");
        }

        state.Pool.Release(enemy);
    }

    private PoolState ResolvePoolState(EnemyDataSO data)
    {
        string enemyID = data.enemyID;

        if (!string.IsNullOrWhiteSpace(enemyID))
        {
            string key = enemyID.Trim().ToLowerInvariant();
            if (_poolStatesByEnemyID.TryGetValue(key, out PoolState mappedState))
                return mappedState;

            DebugLogger.LogWarning($"EnemyPool: Unknown enemyID '{enemyID}'. Falling back to default pool.");
        }

        if (_defaultPoolState == null)
        {
            DebugLogger.LogError("EnemyPool: Default pool is unavailable. Fix EnemyPool configuration.");
            return null;
        }

        return _defaultPoolState;
    }

    private PoolState BuildPoolState(string enemyID, Enemy prefab, int defaultCapacity, int maxSize, bool isDefaultPool)
    {
        if (prefab == null)
        {
            DebugLogger.LogError($"EnemyPool: Missing prefab for pool '{enemyID}'. Skipping pool.");
            return null;
        }

        if (defaultCapacity < 0)
        {
            DebugLogger.LogError($"EnemyPool: defaultCapacity < 0 for '{enemyID}'. Clamping to 0.");
            defaultCapacity = 0;
        }

        if (maxSize <= 0)
        {
            DebugLogger.LogError($"EnemyPool: maxSize <= 0 for '{enemyID}'. Skipping pool.");
            return null;
        }

        if (maxSize < defaultCapacity)
        {
            if (defaultCapacity > 0)
            {
                DebugLogger.LogError(
                    $"EnemyPool: maxSize < defaultCapacity for '{enemyID}'. Clamping maxSize to {defaultCapacity}.");
                maxSize = defaultCapacity;
            }
            else
            {
                DebugLogger.LogError($"EnemyPool: Invalid capacities for '{enemyID}'. Skipping pool.");
                return null;
            }
        }

        PoolState state = new PoolState
        {
            EnemyID = enemyID,
            Prefab = prefab,
            DefaultCapacity = defaultCapacity,
            MaxSize = maxSize,
            CreatedCount = 0
        };

        state.Pool = new ObjectPool<Enemy>(
            createFunc: () => CreateEnemy(state),
            actionOnGet: OnGet,
            actionOnRelease: OnRelease,
            actionOnDestroy: OnDestroyEnemy,
            collectionCheck: false,
            defaultCapacity: state.DefaultCapacity,
            maxSize: state.MaxSize
        );

        Prewarm(state);

        if (isDefaultPool)
            DebugLogger.Log($"EnemyPool: Default pool ready (capacity {state.DefaultCapacity}, max {state.MaxSize}).");

        return state;
    }

    private Enemy CreateEnemy(PoolState state)
    {
        if (state.CreatedCount >= state.MaxSize)
        {
            DebugLogger.LogError(
                $"EnemyPool overflow: '{state.EnemyID}' created beyond maxSize ({state.MaxSize}). "
                + "Increase pool capacity for this enemy type.");
        }

        Enemy enemy = Instantiate(state.Prefab, transform);
        state.CreatedCount++;
        _owningPools[enemy] = state;
        enemy.gameObject.SetActive(false);
        return enemy;
    }

    private void OnGet(Enemy enemy)
    {
        if (enemy == null) return;
        enemy.gameObject.SetActive(true);
    }

    private void OnRelease(Enemy enemy)
    {
        if (enemy == null) return;
        enemy.gameObject.SetActive(false);
    }

    private void OnDestroyEnemy(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (_owningPools.TryGetValue(enemy, out PoolState owner) && owner != null)
        {
            if (owner.CreatedCount > 0)
                owner.CreatedCount--;
            _owningPools.Remove(enemy);
        }

        _checkedOutEnemies.Remove(enemy);

        if (enemy.gameObject != null)
            Destroy(enemy.gameObject);
    }

    private void Prewarm(PoolState state)
    {
        if (state == null || state.Pool == null || state.DefaultCapacity <= 0)
            return;

        List<Enemy> prewarmed = new List<Enemy>(state.DefaultCapacity);

        for (int i = 0; i < state.DefaultCapacity; i++)
        {
            Enemy enemy = state.Pool.Get();
            if (enemy == null)
                break;
            prewarmed.Add(enemy);
        }

        for (int i = 0; i < prewarmed.Count; i++)
            state.Pool.Release(prewarmed[i]);
    }

    private void QuarantineAndDestroy(Enemy enemy, PoolState owner)
    {
        if (enemy == null)
            return;

        _checkedOutEnemies.Remove(enemy);

        if (_owningPools.Remove(enemy) && owner != null && owner.CreatedCount > 0)
            owner.CreatedCount--;

        if (enemy.gameObject != null)
        {
            enemy.gameObject.SetActive(false);
            Destroy(enemy.gameObject);
        }
    }
}
