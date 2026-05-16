using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SingleAttackHitVfxController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SingleAttackHitSpriteVfx _hitVfxPrefab;
    [SerializeField] private Transform _vfxRoot;

    [Header("Placement")]
    [SerializeField] private Vector3 _worldOffset = Vector3.zero;
    [SerializeField] private bool _useSpriteBoundsCenter = true;

    [Header("Pooling")]
    [SerializeField, Min(1)] private int _prewarmCount = 8;
    [SerializeField, Min(1)] private int _maxPoolSize = 16;

    [Header("Playback")]
    [SerializeField, Min(0.05f)] private float _fallbackLifetime = 0.45f;
    [SerializeField] private bool _detachFromEnemy = true;
    [SerializeField] private bool _debugWarnings = true;

    private readonly Queue<SingleAttackHitSpriteVfx> _available = new Queue<SingleAttackHitSpriteVfx>();
    private readonly HashSet<SingleAttackHitSpriteVfx> _allInstances = new HashSet<SingleAttackHitSpriteVfx>();
    private readonly Dictionary<SingleAttackHitSpriteVfx, Coroutine> _activeReturns = new Dictionary<SingleAttackHitSpriteVfx, Coroutine>();

    private void Awake()
    {
        PrewarmPool();
    }

    private void OnEnable()
    {
        EventBus.OnSingleAttackHit += HandleSingleAttackHit;
    }

    private void OnDisable()
    {
        EventBus.OnSingleAttackHit -= HandleSingleAttackHit;
        ResetAllInstances();
    }

    private void HandleSingleAttackHit(Enemy target)
    {
        if (target == null || target.IsDying)
            return;

        SingleAttackHitSpriteVfx instance = GetFromPool();
        if (instance == null)
            return;

        Transform parent = _detachFromEnemy ? _vfxRoot : target.transform;
        instance.transform.SetParent(parent, false);
        instance.transform.position = ResolveHitPosition(target);
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        instance.gameObject.SetActive(true);
        instance.Play(
            frameIndex => EventBus.RaiseSingleAttackVfxFrame(target, frameIndex),
            () => EventBus.RaiseSingleAttackVfxCompleted(target));

        float lifetime = GetPlaybackLifetime(instance);
        Coroutine existing;
        if (_activeReturns.TryGetValue(instance, out existing) && existing != null)
            StopCoroutine(existing);

        _activeReturns[instance] = StartCoroutine(ReturnAfterLifetime(instance, lifetime));
    }

    private Vector3 ResolveHitPosition(Enemy enemy)
    {
        if (enemy == null)
            return Vector3.zero;

        if (_useSpriteBoundsCenter && enemy.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
            return sr.bounds.center + _worldOffset;

        return enemy.transform.position + _worldOffset;
    }

    private void PrewarmPool()
    {
        if (_hitVfxPrefab == null)
        {
            Warn("SingleAttackHitVfxController: Missing hit VFX prefab.");
            return;
        }

        int prewarmTarget = Mathf.Clamp(_prewarmCount, 1, Mathf.Max(1, _maxPoolSize));
        for (int i = _allInstances.Count; i < prewarmTarget; i++)
        {
            SingleAttackHitSpriteVfx instance = CreateInstance();
            if (instance == null)
                break;

            ReturnToPool(instance);
        }
    }

    private SingleAttackHitSpriteVfx GetFromPool()
    {
        if (_hitVfxPrefab == null)
        {
            Warn("SingleAttackHitVfxController: Missing hit VFX prefab.");
            return null;
        }

        while (_available.Count > 0)
        {
            SingleAttackHitSpriteVfx pooled = _available.Dequeue();
            if (pooled != null)
                return pooled;
        }

        if (_allInstances.Count >= Mathf.Max(1, _maxPoolSize))
        {
            Warn("SingleAttackHitVfxController: Pool limit reached.");
            return null;
        }

        return CreateInstance();
    }

    private SingleAttackHitSpriteVfx CreateInstance()
    {
        if (_hitVfxPrefab == null)
            return null;

        Transform parent = _vfxRoot != null ? _vfxRoot : transform;
        SingleAttackHitSpriteVfx instance = Instantiate(_hitVfxPrefab, parent);
        if (instance == null)
            return null;

        instance.gameObject.SetActive(false);
        _allInstances.Add(instance);
        return instance;
    }

    private IEnumerator ReturnAfterLifetime(SingleAttackHitSpriteVfx instance, float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        ReturnToPool(instance);
    }

    private void ReturnToPool(SingleAttackHitSpriteVfx instance)
    {
        if (instance == null)
            return;

        Coroutine active;
        if (_activeReturns.TryGetValue(instance, out active))
        {
            if (active != null)
                StopCoroutine(active);
            _activeReturns.Remove(instance);
        }

        instance.ResetVisual();
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(_vfxRoot != null ? _vfxRoot : transform, false);
        if (!_available.Contains(instance))
            _available.Enqueue(instance);
    }

    private float GetPlaybackLifetime(SingleAttackHitSpriteVfx instance)
    {
        if (instance == null)
            return _fallbackLifetime;

        float total = instance.PlayDuration;
        if (total <= 0f)
            total = _fallbackLifetime;

        return Mathf.Max(0.05f, total);
    }

    private void ResetAllInstances()
    {
        foreach (var kv in _activeReturns)
        {
            if (kv.Value != null)
                StopCoroutine(kv.Value);
        }
        _activeReturns.Clear();
        _available.Clear();

        foreach (SingleAttackHitSpriteVfx instance in _allInstances)
        {
            if (instance == null)
                continue;

            instance.ResetVisual();
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(_vfxRoot != null ? _vfxRoot : transform, false);
            _available.Enqueue(instance);
        }
    }

    private void Warn(string message)
    {
        if (_debugWarnings)
            Debug.LogWarning(message, this);
    }
}
