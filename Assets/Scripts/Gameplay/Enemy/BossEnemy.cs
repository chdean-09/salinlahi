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

    public override void TakeDamage(int amount)
    {
        DebugLogger.LogWarning(
            $"BossEnemy.TakeDamage called with amount={amount}. "
            + "Boss damage is gated by BossController.TryRouteDraw — "
            + "this call has been ignored. Investigate the caller.");
    }
}
