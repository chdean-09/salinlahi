using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class KempeiScrambleController : MonoBehaviour
{
    private const float FallbackMinGlitchInterval = 0.18f;
    private const float FallbackMaxGlitchInterval = 0.36f;

    private sealed class ScrambleState
    {
        public BaybayinCharacterSO Character;
        public bool IsScrambledVisible = true;
        public bool AppliedScrambledVisible;
        public float NextToggleTime;
    }

    private readonly HashSet<Enemy> _affectedEnemies = new();
    private readonly HashSet<Enemy> _stillAffected = new();
    private readonly Dictionary<Enemy, ScrambleState> _activeScrambles = new();
    private readonly List<Enemy> _activeSnapshot = new();
    private readonly List<BaybayinCharacterSO> _candidateCharacters = new();
    private readonly List<Enemy> _enemiesToClear = new();
    private Enemy _enemy;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnDisable()
    {
        ClearAffectedEnemies();
        _activeSnapshot.Clear();
        _candidateCharacters.Clear();
        _enemiesToClear.Clear();
    }

    private void Update()
    {
        if (_enemy == null || _enemy.Data == null)
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
            if (scramble?.Character == null)
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
                enemy.ClearVisualCharacterOverride(this);

            _affectedEnemies.Remove(enemy);
            _activeScrambles.Remove(enemy);
        }

        _enemiesToClear.Clear();
    }

    private ScrambleState GetOrCreateScramble(Enemy target)
    {
        if (target == null)
            return null;

        BaybayinCharacterSO realCharacter = target.Character;
        if (_activeScrambles.TryGetValue(target, out ScrambleState existing)
            && IsWrongCharacter(existing.Character, realCharacter))
        {
            return existing;
        }

        BaybayinCharacterSO next = SelectWrongCharacter(realCharacter);
        if (next == null)
        {
            _activeScrambles.Remove(target);
            return null;
        }

        var state = new ScrambleState
        {
            Character = next,
            IsScrambledVisible = true,
            NextToggleTime = Time.time + Random.Range(GetMinGlitchInterval(), GetMaxGlitchInterval())
        };
        _activeScrambles[target] = state;
        return state;
    }

    private void ApplyScramblePulse(Enemy target, ScrambleState scramble, bool wasAffected)
    {
        if (Time.time >= scramble.NextToggleTime)
        {
            scramble.IsScrambledVisible = !scramble.IsScrambledVisible;
            scramble.NextToggleTime = Time.time + Random.Range(GetMinGlitchInterval(), GetMaxGlitchInterval());
        }

        if (scramble.IsScrambledVisible)
        {
            if (!wasAffected || !scramble.AppliedScrambledVisible)
            {
                target.ApplyVisualCharacterOverride(this, scramble.Character);
                scramble.AppliedScrambledVisible = true;
            }
        }
        else if (wasAffected && scramble.AppliedScrambledVisible)
        {
            target.ClearVisualCharacterOverride(this);
            scramble.AppliedScrambledVisible = false;
        }
    }

    private BaybayinCharacterSO SelectWrongCharacter(BaybayinCharacterSO realCharacter)
    {
        _candidateCharacters.Clear();
        IReadOnlyList<BaybayinCharacterSO> allowedCharacters = WaveManager.CurrentAllowedCharacters;

        if (allowedCharacters != null)
        {
            for (int i = 0; i < allowedCharacters.Count; i++)
                AddIfWrongCharacter(allowedCharacters[i], realCharacter);
        }

        if (_candidateCharacters.Count == 0)
            return null;

        return _candidateCharacters[Random.Range(0, _candidateCharacters.Count)];
    }

    private void AddIfWrongCharacter(BaybayinCharacterSO candidate, BaybayinCharacterSO realCharacter)
    {
        if (!IsWrongCharacter(candidate, realCharacter))
            return;

        if (!_candidateCharacters.Contains(candidate))
            _candidateCharacters.Add(candidate);
    }

    private bool IsWrongCharacter(BaybayinCharacterSO candidate, BaybayinCharacterSO realCharacter)
    {
        if (candidate == null || candidate == realCharacter)
            return false;

        if (realCharacter != null && candidate.characterID == realCharacter.characterID)
            return false;

        return true;
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
                enemy.ClearVisualCharacterOverride(this);
        }

        _affectedEnemies.Clear();
        _stillAffected.Clear();
        _activeScrambles.Clear();
    }

    private float GetMinGlitchInterval()
    {
        if (_enemy?.Data == null)
            return FallbackMinGlitchInterval;

        return Mathf.Max(0f, _enemy.Data.scrambleMinGlitchInterval);
    }

    private float GetMaxGlitchInterval()
    {
        if (_enemy?.Data == null)
            return FallbackMaxGlitchInterval;

        float min = GetMinGlitchInterval();
        return Mathf.Max(min, _enemy.Data.scrambleMaxGlitchInterval);
    }
}
