using UnityEngine;

// Boss-specific Enemy subclass. Two responsibilities only:
//   1. IsBoss returns true so CombatResolver excludes the boss from AOE
//      and closest-match.
//   2. TakeDamage no-ops with a warning, so a future direct-damage code path
//      (projectile, contact damage, etc.) cannot bypass BossController's
//      phase gate. All boss damage flows through BossController.TryRouteDraw.
// Co-located with BossController on the boss prefab.
public class BossEnemy : Enemy
{
    public override bool IsBoss => true;

    private SpriteRenderer _sr;

    public override void TakeDamage(int amount)
    {
        DebugLogger.LogWarning(
            $"BossEnemy.TakeDamage called with amount={amount}. "
            + "Boss damage is gated by BossController.TryRouteDraw — "
            + "this call has been ignored. Investigate the caller.");
    }

    protected override void Awake()
    {
        // base.Awake() resolves _mover, _hurtFeedback, _summonTicker, and the
        // base _renderer cache. Skipping it leaves _summonTicker null on the
        // boss, which silently disables the walk-suppression guard during the
        // summon tell on Pacing movement.
        base.Awake();
        _sr = GetComponent<SpriteRenderer>();
    }

    // OnEnable (not Start) so the sortingOrder reasserts on every pool reuse —
    // Start fires only once per GameObject lifetime. Inspector value on the
    // prefab is kept in sync (m_SortingOrder: 10) so designers see the truth.
    protected override void OnEnable()
    {
        base.OnEnable();
        if (_sr != null) _sr.sortingOrder = RenderOrder.Boss;
    }
}
