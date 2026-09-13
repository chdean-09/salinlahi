using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kadena's signature ability: "It binds villagers." On spawn Kadena chains the nearest
/// <i>other</i> enemy and holds it under a resolution block — it can be neither marked as the
/// active clue nor damaged — until Kadena is defeated.
///
/// <para>
/// The block itself lives on <see cref="Enemy.AddResolutionBlock"/>, keyed on this controller
/// instance, exactly as <see cref="BakodShieldController"/> does. That seam (SALIN-286) was built
/// to take a second source without touching resolver code, so <b>nothing in
/// <c>CombatResolver</c> or <c>ActiveClueDirector</c> changes for this ability</b>: both already
/// refuse a blocked enemy (<c>CombatResolver.IsEligibleCombatTarget</c>,
/// <c>ActiveClueDirector.IsEligibleClue</c>). No <c>Enemy.TakeDamage</c> guard is added either —
/// every damage path Kadena can reach passes through <c>IsEligibleCombatTarget</c> first, so a
/// guard there would be unreachable code.
/// </para>
///
/// <para>
/// <b>One target, acquired once, never re-chained.</b> "On spawn" is read as <i>the first tick at
/// which a candidate exists</i>, not literally the spawn frame: <c>WaveSpawner</c> can place
/// Kadena first in its wave (Level7_Config wave 1 spawns 5 enemies at 2.2 s intervals), at which
/// moment no other enemy is on screen. A literal reading would silently no-op for that spawn. Once
/// acquired the target is frozen for the rest of this spawn — the latch is keyed on
/// <see cref="Enemy.SpawnSequence"/> so a pooled shell reused as another Kadena chains afresh.
/// </para>
///
/// Data-driven through <see cref="EnemyDataSO.chainsNearestEnemy"/>; Enemy.Initialize attaches
/// this component on the shared corruption shell and toggles it per spawn.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class KadenaChainController : MonoBehaviour
{
    /// <summary>
    /// Shared scratch buffer for the tracker snapshot, mirroring
    /// <see cref="BakodShieldController"/>. Tick is never re-entrant and
    /// <c>FillActiveEnemiesSnapshot</c> clears it on entry, so sharing it across Kadenas is safe.
    /// </summary>
    private static readonly List<Enemy> SnapshotBuffer = new List<Enemy>();

    /// <summary>
    /// Squared-distance tolerance below which two candidates count as equidistant and the
    /// deterministic tiebreaker below decides. Without it the winner depends on float noise and
    /// the tie test is flaky.
    /// </summary>
    private const float TieToleranceSqr = 0.0001f;

    private Enemy _enemy;
    private Enemy _chained;

    /// <summary>
    /// The <see cref="Enemy.SpawnSequence"/> this controller already chained for, or -1. Keyed on
    /// the spawn rather than a plain bool because a pooled shell reused as Kadena again keeps this
    /// component enabled throughout, so OnEnable never fires to reset a bool.
    /// </summary>
    private long _acquiredForSpawn = -1;

    /// <summary>The enemy currently held, or null. Test seam.</summary>
    public Enemy ChainedEnemy => _chained;

    public bool IsChaining => _chained != null;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnDisable()
    {
        // Pool safety, and the defect BakodShieldController.cs:49-55 names: a chain that outlives
        // its holder strands a permanently unresolvable enemy on screen — no exception, no failing
        // test, just a level the player cannot finish.
        Release();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            return;

        Tick(Time.deltaTime);
    }

    /// <summary>
    /// Acquires the chain if it is not held yet, and drops it once it is no longer valid. Public so
    /// it can be driven without frames in tests, mirroring <see cref="BakodShieldController.Tick"/>.
    /// <para>
    /// <paramref name="deltaTime"/> is unused — the ability has no timer — and is kept so every
    /// ability controller presents the same driving surface.
    /// </para>
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        EnemyDataSO data = _enemy != null ? _enemy.Data : null;
        if (data == null || !data.chainsNearestEnemy || _enemy.IsDying)
        {
            // Dead, disarmed or recycled: the chain falls. This is what lifts the block when
            // Kadena is defeated.
            Release();
            return;
        }

        if (_chained != null)
        {
            // Never re-target within one spawn. The hold is dropped only when the holdee stops
            // being valid, or when this shell has been re-initialized for a new spawn without an
            // intervening disable (Enemy.Initialize bumps SpawnSequence but leaves an already
            // enabled ability component alone, so OnDisable never fires on that path).
            if (_acquiredForSpawn != _enemy.SpawnSequence || !IsChainable(_chained))
                Release();
            else
                return;
        }

        if (_acquiredForSpawn == _enemy.SpawnSequence)
            return; // already spent this spawn's one chain

        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null)
            return;

        Enemy nearest = FindNearestChainable(tracker);
        if (nearest == null)
            return; // Kadena is alone so far; try again next tick rather than no-op forever

        _chained = nearest;
        _acquiredForSpawn = _enemy.SpawnSequence;
        _chained.AddResolutionBlock(this);
    }

    private Enemy FindNearestChainable(ActiveEnemyTracker tracker)
    {
        tracker.FillActiveEnemiesSnapshot(SnapshotBuffer);

        Vector3 own = transform.position;
        Enemy best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < SnapshotBuffer.Count; i++)
        {
            Enemy candidate = SnapshotBuffer[i];
            if (!IsChainable(candidate))
                continue;

            float sqr = (candidate.transform.position - own).sqrMagnitude;
            if (best == null || sqr < bestSqr - TieToleranceSqr)
            {
                best = candidate;
                bestSqr = sqr;
                continue;
            }

            if (sqr <= bestSqr + TieToleranceSqr && WinsTie(candidate, best))
            {
                best = candidate;
                bestSqr = sqr;
            }
        }

        return best;
    }

    /// <summary>
    /// D-011's deterministic order for equidistant candidates: lower Y (nearer the base) first,
    /// then the lower <see cref="Enemy.SpawnSequence"/>.
    /// </summary>
    private static bool WinsTie(Enemy candidate, Enemy incumbent)
    {
        float candidateY = candidate.transform.position.y;
        float incumbentY = incumbent.transform.position.y;

        if (!Mathf.Approximately(candidateY, incumbentY))
            return candidateY < incumbentY;

        return candidate.SpawnSequence < incumbent.SpawnSequence;
    }

    private bool IsChainable(Enemy candidate)
    {
        if (candidate == null || candidate == _enemy)
            return false;
        // A shell returned to the pool has null Data and is parked off screen; both guards keep it
        // out of the candidate set.
        if (candidate.Data == null)
            return false;
        if (!candidate.gameObject.activeInHierarchy)
            return false;
        if (candidate.IsBoss)
            return false;
        if (candidate.IsDying)
            return false;

        return true;
    }

    private void Release()
    {
        if (_chained == null)
            return;

        _chained.RemoveResolutionBlock(this);
        _chained = null;
    }
}
