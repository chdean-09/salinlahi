using System.Collections.Generic;
using UnityEngine;

// every TICK seconds, applies the data-driven speed buff to every American-era
// non-boss enemy within auraRadius and removes the buff from anything that just left the radius
[RequireComponent(typeof(Enemy))]
public class GeneralAura : MonoBehaviour
{
    private const float TICK = 0.25f;

    private Enemy _self;
    private readonly HashSet<Enemy> _affected = new HashSet<Enemy>();
    private readonly HashSet<Enemy> _stillAffectedBuffer = new HashSet<Enemy>();
    private float _nextTick;

    private void OnEnable()
    {
        _self = GetComponent<Enemy>();
        _nextTick = 0f;
        _affected.Clear();
        _stillAffectedBuffer.Clear();
    }

    private void OnDisable()
    {
        foreach (Enemy e in _affected)
        {
            if (e != null) e.ClearSpeedBuff(this);
        }
        _affected.Clear();
        _stillAffectedBuffer.Clear();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameState.Playing) return;
        if (Time.time < _nextTick) return;
        _nextTick = Time.time + TICK;

        EnemyDataSO data = _self != null ? _self.Data : null;
        if (data == null || data.auraRadius <= 0f) return;

        if (ActiveEnemyTracker.Instance == null) return;
        List<Enemy> active = ActiveEnemyTracker.Instance.GetActiveEnemiesSnapshot();

        float radiusSq = data.auraRadius * data.auraRadius;
        Vector3 myPos = transform.position;

        _stillAffectedBuffer.Clear();

        for (int i = 0; i < active.Count; i++)
        {
            Enemy other = active[i];
            if (other == null || other == _self) continue;          // do not self-buff
            if (other.IsBoss) continue;
            EnemyDataSO otherData = other.Data;
            if (otherData == null) continue;
            if (otherData.era != Era.American) continue;

            float distSq = (other.transform.position - myPos).sqrMagnitude;
            if (distSq > radiusSq) continue;

            other.ApplySpeedBuff(this, data.auraSpeedMultiplier);
            _stillAffectedBuffer.Add(other);
        }

        // Anything in last tick's set that is no longer in radius loses the buff now.
        foreach (Enemy prev in _affected)
        {
            if (prev != null && !_stillAffectedBuffer.Contains(prev))
                prev.ClearSpeedBuff(this);
        }

        _affected.Clear();
        foreach (Enemy e in _stillAffectedBuffer) _affected.Add(e);
    }
}