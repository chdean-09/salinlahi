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
    private int _currentHealth;

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
        _currentHealth = _data.maxHealth;
        _mover.SetSpeed(_data.moveSpeed);
        if (_data.walkFrames != null && _data.walkFrames.Length > 0)
            _renderer.sprite = _data.walkFrames[0];
        ActiveEnemyTracker.Instance?.Register(this);
    }

    public void TakeDamage(int amount)
    {
        _currentHealth -= amount;
        DebugLogger.Log(
            $"Enemy [{Character?.characterID}] took {amount} damage. "
            + $"HP: {_currentHealth}");

        if (_currentHealth <= 0)
        {
            Defeat();
        }
        // else: shield-break effect can go here!
    }


    // Call this to defeat the enemy and return it to the pool
    public void Defeat()
    {
        ActiveEnemyTracker.Instance?.Unregister(this);
        EventBus.RaiseEnemyDefeated(Character);
        _mover?.Stop();
        ReturnToPool();
    }

    public void ReturnToPool()
    {
        ActiveEnemyTracker.Instance?.Unregister(this);
        _pool?.Release(this);
    }

    private void OnDisable()
    {
        _mover?.Stop();
    }
}