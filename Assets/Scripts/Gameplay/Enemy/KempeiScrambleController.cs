using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class KempeiScrambleController : MonoBehaviour
{
    private sealed class ScrambleState
    {
        public GlyphStainCycle Cycle;
        public bool AppliedScrambledVisible;
    }

    private readonly HashSet<Enemy> _affectedEnemies = new();
    private readonly HashSet<Enemy> _stillAffected = new();
    private readonly Dictionary<Enemy, ScrambleState> _activeScrambles = new();
    private readonly List<Enemy> _activeSnapshot = new();
    private readonly List<Enemy> _enemiesToClear = new();
    private Enemy _enemy;

    /// <summary>
    /// True while this spawn is the one that introduced its type, in which case the stain must do
    /// nothing at all. See <see cref="SetSuppressedForIntroductionSpawn"/>.
    /// </summary>
    private bool _suppressedForIntroductionSpawn;

    /// <summary>
    /// Makes this ability inert for one spawn — the spawn on which the enemy's introduction card
    /// plays — and arms it again on every later spawn of the type.
    ///
    /// <para>
    /// <b>Why the stain must not fire on the spawn that introduces it.</b> Mantsa's ink pulse makes a
    /// neighbour's stable badge harder to read. A player who has never been told that happens,
    /// watching it for the first time while the card explaining it is still sliding in, reads the
    /// visual change as a rendering fault — and, worse, may be mid-stroke against the very badge
    /// that is being stained. The card sets the expectation first; the stain arms on a later Mantsa,
    /// against a board the player has already read cleanly.
    /// </para>
    ///
    /// <para>
    /// Suppression withdraws the effect as well as preventing it: any neighbour already carrying a
    /// stain is lifted immediately, rather than waiting for the next
    /// churn step that will now never come.
    /// </para>
    ///
    /// <para>
    /// <b>Pooling.</b> Suppression is per spawn, never per shell. <c>Enemy.Initialize</c> restates it
    /// on every spawn and <see cref="OnEnable"/> clears it, so a recycled shell always comes back
    /// unsuppressed.
    /// </para>
    /// </summary>
    public void SetSuppressedForIntroductionSpawn(bool suppressed)
    {
        if (_suppressedForIntroductionSpawn == suppressed)
            return;

        _suppressedForIntroductionSpawn = suppressed;
        if (suppressed)
            ClearAffectedEnemies();
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        // A pooled shell must not inherit the previous occupant's suppression.
        _suppressedForIntroductionSpawn = false;
    }

    private void OnDisable()
    {
        ClearAffectedEnemies();
        _activeSnapshot.Clear();
        _enemiesToClear.Clear();
    }

    private void Update()
    {
        // Gated by data so the shared corruption shell can carry this for Mantsa ("It stains part
        // of a correct character until its true form is hard to see") and stay inert for everyone else.
        // The introduction-spawn suppression joins the same gate rather than getting its own early
        // return, so both ways of being inert release held neighbours through one path.
        if (_suppressedForIntroductionSpawn
            || _enemy == null
            || _enemy.Data == null
            || !_enemy.Data.stainsNearbyGlyphs)
        {
            ClearAffectedEnemies();
            return;
        }

        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null)
        {
            ClearAffectedEnemies();
            return;
        }

        tracker.FillActiveEnemiesSnapshot(_activeSnapshot);

        float radius = Mathf.Max(0f, _enemy.Data.scrambleRadius);
        float radiusSqr = radius * radius;
        Vector3 center = transform.position;

        _stillAffected.Clear();
        for (int i = 0; i < _activeSnapshot.Count; i++)
        {
            Enemy target = _activeSnapshot[i];
            if (target == null || target == _enemy)
                continue;

            if ((target.transform.position - center).sqrMagnitude > radiusSqr)
                continue;

            ScrambleState scramble = GetOrCreateScramble(target);
            if (scramble == null)
                continue;

            bool wasAffected = _affectedEnemies.Contains(target);
            ApplyScramblePulse(target, scramble, wasAffected);

            _affectedEnemies.Add(target);
            _stillAffected.Add(target);
        }

        RemoveUnaffectedEnemies();
    }

    private void RemoveUnaffectedEnemies()
    {
        if (_affectedEnemies.Count == 0)
            return;

        _enemiesToClear.Clear();
        foreach (Enemy enemy in _affectedEnemies)
        {
            if (enemy != null && _stillAffected.Contains(enemy))
                continue;

            _enemiesToClear.Add(enemy);
        }

        if (_enemiesToClear.Count == 0)
            return;

        for (int i = 0; i < _enemiesToClear.Count; i++)
        {
            Enemy enemy = _enemiesToClear[i];
            if (enemy != null)
                enemy.SetGlyphStained(this, false);

            _affectedEnemies.Remove(enemy);
            _activeScrambles.Remove(enemy);
        }

        _enemiesToClear.Clear();
    }

    private ScrambleState GetOrCreateScramble(Enemy target)
    {
        if (target == null)
            return null;

        if (_activeScrambles.TryGetValue(target, out ScrambleState existing))
        {
            return existing;
        }

        var state = new ScrambleState
        {
            Cycle = new GlyphStainCycle(
                Time.time,
                GetMinGlitchInterval(),
                GetMaxGlitchInterval(),
                GetTrueMinDwell(),
                GetTrueMaxDwell(),
                GetFalseBurstCount(),
                Random.value)
        };
        _activeScrambles[target] = state;
        return state;
    }

    private void ApplyScramblePulse(Enemy target, ScrambleState scramble, bool wasAffected)
    {
        bool changed = scramble.Cycle.Advance(Time.time, Random.value);

        if (scramble.Cycle.IsFalseGlyphVisible)
        {
            if (changed || !wasAffected || !scramble.AppliedScrambledVisible)
            {
                // The stain is presentation-only. Do not replace the stable glyph with a polished
                // wrong character: doing so teaches a false Baybayin association and makes a
                // correctly recognized drawing appear to disagree with the enemy.
                target.SetGlyphStained(this, true);
                scramble.AppliedScrambledVisible = true;
            }
        }
        else if (wasAffected && scramble.AppliedScrambledVisible)
        {
            target.SetGlyphStained(this, false);
            scramble.AppliedScrambledVisible = false;
        }
    }

    private void ClearAffectedEnemies()
    {
        if (_affectedEnemies.Count == 0)
        {
            _activeScrambles.Clear();
            return;
        }

        foreach (Enemy enemy in _affectedEnemies)
        {
            if (enemy != null)
                enemy.SetGlyphStained(this, false);
        }

        _affectedEnemies.Clear();
        _stillAffected.Clear();
        _activeScrambles.Clear();
    }

    // The authored values are a request, not a guarantee. The churn band is floored at
    // GlyphStainCycle.MinimumFalseGlyphInterval and the rest band at MinimumReadableInterval, so
    // Mantsa's shipped 0.18/0.36 now reads as what it always was - the churn speed - while the
    // true face gets a long window the authoring cannot shorten.
    private float GetMinGlitchInterval()
    {
        if (_enemy?.Data == null)
            return GlyphStainCycle.DefaultFalseMinInterval;

        return Mathf.Max(0f, _enemy.Data.scrambleMinGlitchInterval);
    }

    private float GetTrueMinDwell()
    {
        if (_enemy?.Data == null)
            return GlyphStainCycle.DefaultTrueMinInterval;

        return Mathf.Max(0f, _enemy.Data.scrambleTrueGlyphMinDwell);
    }

    private float GetTrueMaxDwell()
    {
        if (_enemy?.Data == null)
            return GlyphStainCycle.DefaultTrueMaxInterval;

        return Mathf.Max(GetTrueMinDwell(), _enemy.Data.scrambleTrueGlyphMaxDwell);
    }

    private int GetFalseBurstCount()
    {
        if (_enemy?.Data == null)
            return GlyphStainCycle.DefaultFalseBurstCount;

        return Mathf.Max(1, _enemy.Data.scrambleFalseBurstCount);
    }

    private float GetMaxGlitchInterval()
    {
        if (_enemy?.Data == null)
            return GlyphStainCycle.DefaultFalseMaxInterval;

        float min = GetMinGlitchInterval();
        return Mathf.Max(min, _enemy.Data.scrambleMaxGlitchInterval);
    }
}
