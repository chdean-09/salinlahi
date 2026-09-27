using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bakod's signature ability: "It blocks paths and separates." While Bakod lives, every non-boss
/// enemy <i>behind</i> it is held unresolvable — it can be neither marked as the active clue nor
/// damaged — so the player has to clear Bakod first.
///
/// <para>
/// "Behind" needs no geometry. The game already resolves the enemy lowest on screen first
/// (<c>ActiveEnemyTracker.FindClosestToBase</c>, <c>CombatResolver.FindClosestEligibleMatch</c>),
/// so <i>behind Bakod</i> is simply <i>greater Y than Bakod</i>. No lanes, no X axis, no movement
/// code — the ticket's "targeting constraint, not movement geometry" holds literally.
/// </para>
///
/// <para>
/// The block itself lives on <see cref="Enemy.AddResolutionBlock"/>, keyed on this controller
/// instance, so the resolver never learns Bakod exists.
/// </para>
///
/// Data-driven through <see cref="EnemyDataSO.blocksEnemiesBehind"/>; Enemy.Initialize attaches
/// this component on the shared corruption shell and toggles it per spawn.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class BakodShieldController : MonoBehaviour
{
    /// <summary>
    /// Shared scratch buffer for the per-tick tracker snapshot. Tick is never re-entrant (single
    /// threaded, and the loop below completes before any other Tick can start), and
    /// <c>FillActiveEnemiesSnapshot</c> clears it on entry, so sharing it across Bakods is safe and
    /// keeps the ability allocation-free per frame.
    /// </summary>
    private static readonly List<Enemy> SnapshotBuffer = new List<Enemy>();

    private Enemy _enemy;

    /// <summary>Enemies this controller currently holds blocked, so it can release exactly its own.</summary>
    private readonly List<Enemy> _blocked = new List<Enemy>();

    /// <summary>
    /// True while this spawn is the one that introduced its type, in which case the shield must do
    /// nothing at all. See <see cref="SetSuppressedForIntroductionSpawn"/>.
    /// </summary>
    private bool _suppressedForIntroductionSpawn;

    public int BlockedCount => _blocked.Count;

    public bool IsBlocking(Enemy enemy) => enemy != null && _blocked.Contains(enemy);

    /// <summary>True while this spawn is suppressed as its type's introduction spawn. Test/diagnostic seam, mirroring <see cref="AshFirstSlotController.IsSuppressedForIntroductionSpawn"/>.</summary>
    public bool IsSuppressedForIntroductionSpawn => _suppressedForIntroductionSpawn;

    /// <summary>
    /// Makes this ability inert for one spawn — the spawn on which the enemy's introduction card
    /// plays — and arms it again on every later spawn of the type.
    ///
    /// <para>
    /// <b>Why the shield must not raise on the spawn that introduces it.</b> Bakod's block is
    /// invisible: nothing on screen says why a symbol the player drew correctly did no damage. A
    /// player meeting Bakod for the first time, with the card still framing him, would read a
    /// correct draw that does nothing as recognition being broken — the exact "bug, not threat"
    /// failure the per-spawn arming rule exists to prevent. The card states the blocking; the next
    /// Bakod performs it, once the player knows a body can be in the way.
    /// </para>
    ///
    /// <para>
    /// Suppression releases every hold this controller owns, so toggling it mid-life cannot strand
    /// an enemy that is unresolvable with nothing shielding it.
    /// </para>
    ///
    /// <para>
    /// <b>Pooling.</b> Suppression is per spawn, never per shell. <c>Enemy.Initialize</c> restates it
    /// on every spawn and <see cref="OnEnable"/> clears it, so a recycled shell always comes back
    /// unsuppressed — a stuck flag here would silently disable Bakod's shield for the rest of the
    /// run, on a shell that looks identical to a working one.
    /// </para>
    /// </summary>
    public void SetSuppressedForIntroductionSpawn(bool suppressed)
    {
        if (_suppressedForIntroductionSpawn == suppressed)
            return;

        _suppressedForIntroductionSpawn = suppressed;
        if (suppressed)
        {
            ReleaseAll();
            _enemy?.AbilityVisuals?.SetActive(EnemyAbilityVisualId.BakodBarrier, false);
        }
    }

    /// <summary>Starts Bakod's authored wall-break one-shot when this spawn is defeated.</summary>
    public bool BeginDefeatVisual()
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        EnemyDataSO data = _enemy != null ? _enemy.Data : null;
        if (!IsBarrierActiveForSpawn()
            || (data?.deathFrames != null && data.deathFrames.Length > 0))
            return false;

        EnemyAbilityVisualPresenter presenter = _enemy != null ? _enemy.AbilityVisuals : null;
        if (presenter == null || !presenter.HasVisual(EnemyAbilityVisualId.BakodBarrier))
            return false;

        presenter.PlayExit(EnemyAbilityVisualId.BakodBarrier);
        return presenter.IsExitPlaying(EnemyAbilityVisualId.BakodBarrier);
    }

    private bool IsBarrierActiveForSpawn()
    {
        EnemyDataSO data = _enemy != null ? _enemy.Data : null;
        return !_suppressedForIntroductionSpawn
            && data != null
            && data.blocksEnemiesBehind
            && !_enemy.IsDying;
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        // A pooled shell must not inherit the previous occupant's suppression. Added for the
        // suppression flag alone: everything else this controller owns is rebuilt from live
        // positions on the next Tick, and its holds are released in OnDisable.
        _suppressedForIntroductionSpawn = false;
    }

    private void OnDisable()
    {
        // Pool safety, and the single most dangerous defect this ability can ship: a Bakod that
        // leaves play without releasing its holds strands a permanently unresolvable enemy on
        // screen — no exception, no failing test, just a level the player cannot finish.
        ReleaseAll();
        _enemy?.AbilityVisuals?.SetActive(EnemyAbilityVisualId.BakodBarrier, false);
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            return;

        Tick(Time.deltaTime);
    }

    /// <summary>
    /// Recomputes the shielded set from live positions. Public so it can be driven without frames
    /// in tests, mirroring <see cref="GlyphCoverController.Tick"/>.
    /// <para>
    /// <paramref name="deltaTime"/> is unused: unlike Takip's cover cycle this ability has no
    /// timer, and is a pure function of who is on screen right now. The parameter is kept so every
    /// ability controller presents the same driving surface.
    /// </para>
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        // The introduction-spawn suppression joins the same gate as the data flag rather than
        // getting its own early return, so every way of being inert lets its held enemies go
        // through the one path that has always done it.
        bool abilityActive = IsBarrierActiveForSpawn();
        _enemy.AbilityVisuals?.SetActive(EnemyAbilityVisualId.BakodBarrier, abilityActive);

        if (!abilityActive)
        {
            // Dead, disarmed, suppressed or recycled: the shield is down. This is what lifts the
            // block when Bakod is defeated.
            ReleaseAll();
            return;
        }

        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null)
        {
            ReleaseAll();
            return;
        }

        float ownY = transform.position.y;

        // Release first, so an enemy that overtook Bakod is freed in the same tick it passes.
        for (int i = _blocked.Count - 1; i >= 0; i--)
        {
            Enemy held = _blocked[i];
            if (held == null || !ShouldShield(held, ownY))
            {
                if (held != null)
                    held.RemoveResolutionBlock(this);
                _blocked.RemoveAt(i);
            }
        }

        tracker.FillActiveEnemiesSnapshot(SnapshotBuffer);
        for (int i = 0; i < SnapshotBuffer.Count; i++)
        {
            Enemy candidate = SnapshotBuffer[i];
            if (!ShouldShield(candidate, ownY))
                continue;
            if (_blocked.Contains(candidate))
                continue;

            candidate.AddResolutionBlock(this);
            _blocked.Add(candidate);
        }
    }

    private bool ShouldShield(Enemy candidate, float ownY)
    {
        if (candidate == null || candidate == _enemy)
            return false;
        // A shell that has been returned to the pool has null Data and is parked at y 9999; both
        // guards keep it out of the shielded set.
        if (candidate.Data == null)
            return false;
        if (!candidate.gameObject.activeInHierarchy)
            return false;
        if (candidate.IsBoss)
            return false;
        if (candidate.IsDying)
            return false;

        if (candidate.transform.position.y <= ownY)
            return false;

        // Context Gate: a carrier matching the next objective occurrence remains an opening in the
        // wall. The active objective is authoritative for the slot; no alternate recognizer or
        // target-selection rule is introduced here.
        if (_enemy.Data.learningAbility == EnemyLearningAbility.ContextGate)
        {
            string next = RestorationObjectiveController.Active?.State.NextTargetSymbolStableId;
            if (!string.IsNullOrEmpty(next)
                && candidate.Character != null
                && string.Equals(candidate.Character.stableId, next, System.StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private void ReleaseAll()
    {
        for (int i = 0; i < _blocked.Count; i++)
        {
            Enemy held = _blocked[i];
            if (held != null)
                held.RemoveResolutionBlock(this);
        }

        _blocked.Clear();
    }
}
