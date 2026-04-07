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

    [Header("Prefab")]
    [SerializeField] private Enemy _enemyPrefab;

    [Header("Pool Size")]
    [SerializeField] private int _defaultCapacity = 10;
    [SerializeField] private int _maxSize = 20;

    [Header("Registered Enemy Prefabs")]
    [SerializeField] private List<EnemyPrefabRegistration> _registeredEnemyPrefabs = new();

    private readonly Dictionary<string, IObjectPool<Enemy>> _poolsByEnemyID = new();
    private IObjectPool<Enemy> _defaultPool;

    protected override void Awake()
    {
        base.Awake();
        _poolsByEnemyID.Clear();

        _defaultPool = BuildPool(_enemyPrefab, _defaultCapacity, _maxSize);

        foreach (EnemyPrefabRegistration registration in _registeredEnemyPrefabs)
        {
            if (registration == null || registration.prefab == null || string.IsNullOrWhiteSpace(registration.enemyID))
                continue;

            string key = registration.enemyID.Trim().ToLowerInvariant();
            if (_poolsByEnemyID.ContainsKey(key))
                continue;

            _poolsByEnemyID[key] = BuildPool(
                registration.prefab,
                registration.defaultCapacity,
                registration.maxSize
            );
        }
    }

    // Call this to retrieve an enemy and initialize it with data
    public Enemy Get(EnemyDataSO data)
    {
        IObjectPool<Enemy> pool = ResolvePool(data);
        Enemy enemy = pool.Get();
        enemy.Initialize(data, pool);
        return enemy;
    }

    private IObjectPool<Enemy> ResolvePool(EnemyDataSO data)
    {
        if (data != null && !string.IsNullOrWhiteSpace(data.enemyID))
        {
            string key = data.enemyID.Trim().ToLowerInvariant();
            if (_poolsByEnemyID.TryGetValue(key, out IObjectPool<Enemy> mappedPool))
                return mappedPool;
        }

        return _defaultPool;
    }

    private IObjectPool<Enemy> BuildPool(Enemy prefab, int defaultCapacity, int maxSize)
    {
        return new ObjectPool<Enemy>(
            createFunc: () => CreateEnemy(prefab),
            actionOnGet: OnGet,
            actionOnRelease: OnRelease,
            actionOnDestroy: OnDestroyEnemy,
            collectionCheck: false,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    private Enemy CreateEnemy(Enemy prefab)
    {
        Enemy enemy = Instantiate(prefab);
        enemy.gameObject.SetActive(false);
        return enemy;
    }

    private void OnGet(Enemy enemy) => enemy.gameObject.SetActive(true);
    private void OnRelease(Enemy enemy) => enemy.gameObject.SetActive(false);
    private void OnDestroyEnemy(Enemy enemy) => Destroy(enemy.gameObject);
}