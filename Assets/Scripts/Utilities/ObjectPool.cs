using UnityEngine;
using UnityEngine.Pool;

// Generic object pool wrapper.
// Usage: inherit from this or use EnemyPool which wraps it directly.
// Do not call Instantiate or Destroy in game loop code.
// Always get from pool, always return to pool.
public abstract class PooledObject<T> : MonoBehaviour where T : MonoBehaviour
{
    private IObjectPool<T> _pool;

    public void SetPool(IObjectPool<T> pool) => _pool = pool;

    public void ReturnToPool()
    {
        if (_pool != null)
            _pool.Release(this as T);
        else
            Destroy(gameObject);
    }
}