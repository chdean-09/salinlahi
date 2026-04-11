using UnityEngine;
using UnityEngine.Pool;

// Attach to Enemy prefab root. Holds data reference and returns itself to pool.
[RequireComponent(typeof(EnemyMover))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private EnemyDataSO _data;

    [Header("Shield Break Placeholder Visual")]
    [SerializeField] private bool _useShieldBreakColorFeedback;
    [SerializeField] private Color _shieldIntactColor = new(0f, 0.75f, 0.65f, 1f);
    [SerializeField] private Color _shieldBrokenColor = new(0.55f, 0.55f, 0.55f, 1f);

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
        ResetShieldBreakVisual();
        ActiveEnemyTracker.Instance?.Register(this);
    }

    public void TakeDamage(int amount)
    {
        int previousHealth = _currentHealth;
        _currentHealth -= amount;
        DebugLogger.Log(
            $"Enemy [{Character?.characterID}] took {amount} damage. "
            + $"HP: {_currentHealth}");

        if (_currentHealth <= 0)
        {
            Defeat();
        }
        else if (ShouldTriggerShieldBreak(previousHealth))
        {
            TriggerShieldBreakVisual();
        }
    }

    private bool ShouldTriggerShieldBreak(int previousHealth)
    {
        return _data != null
            && _data.maxHealth > 1
            && previousHealth == _data.maxHealth
            && _currentHealth < previousHealth
            && _currentHealth > 0;
    }

    private void ResetShieldBreakVisual()
    {
        if (!_useShieldBreakColorFeedback || _data == null || _data.maxHealth <= 1)
            return;

        _renderer.color = _shieldIntactColor;
    }

    private void TriggerShieldBreakVisual()
    {
        if (!_useShieldBreakColorFeedback)
            return;

        _renderer.color = _shieldBrokenColor;
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
