using UnityEngine;
using UnityEngine.Pool;

public class EnemyPool : Singleton<EnemyPool>
{
    [Header("Prefab")]
    [SerializeField] private Enemy _enemyPrefab;

    [Header("Pool Size")]
    [SerializeField] private int _defaultCapacity = 10;
    [SerializeField] private int _maxSize = 20;

    private IObjectPool<Enemy> _pool;

    protected override void Awake()
    {
        base.Awake();
        _pool = new ObjectPool<Enemy>(
            createFunc: CreateEnemy,
            actionOnGet: OnGet,
            actionOnRelease: OnRelease,
            actionOnDestroy: OnDestroyEnemy,
            collectionCheck: false,       // disabling avoids overhead in builds
            defaultCapacity: _defaultCapacity,
            maxSize: _maxSize
        );
    }

    // Call this to retrieve an enemy and initialize it with data
    public Enemy Get(EnemyDataSO data)
    {
        Enemy e = _pool.Get();
        e.Initialize(data, _pool);
        return e;
    }

    private Enemy CreateEnemy()
    {
        Enemy e = Instantiate(_enemyPrefab);
        e.gameObject.SetActive(false);
        return e;
    }

    private void OnGet(Enemy e) => e.gameObject.SetActive(true);
    private void OnRelease(Enemy e) => e.gameObject.SetActive(false);
    private void OnDestroyEnemy(Enemy e) => Destroy(e.gameObject);
}
