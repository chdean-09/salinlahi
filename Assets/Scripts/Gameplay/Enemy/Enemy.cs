using UnityEngine;
using UnityEngine.Pool;

// Attach to Enemy prefab root. Holds data reference and returns itself to pool.
[RequireComponent(typeof(EnemyMover))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private EnemyDataSO _data;

    private IObjectPool<Enemy> _pool;
    private EnemyMover _mover;
    private SpriteRenderer _renderer;

    public BaybayinCharacterSO Character => _data?.assignedCharacter;
    public string EnemyID => _data?.enemyID;

    private void Awake()
    {
        _mover = GetComponent<EnemyMover>();
        _renderer = GetComponent<SpriteRenderer>();
    }

    // Called by EnemyPool when this enemy is retrieved from the pool
    public void Initialize(EnemyDataSO data, IObjectPool<Enemy> pool)
    {
        _data = data;
        _pool = pool;
        _mover.SetSpeed(_data.moveSpeed);
        if (_data.walkFrames != null && _data.walkFrames.Length > 0)
            _renderer.sprite = _data.walkFrames[0];
    }

    // Call this to defeat the enemy and return it to the pool
    public void Defeat()
    {
        EventBus.RaiseEnemyDefeated(Character);
        ReturnToPool();
    }

    public void ReturnToPool()
    {
        _pool?.Release(this);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _mover?.Stop();
    }
}
