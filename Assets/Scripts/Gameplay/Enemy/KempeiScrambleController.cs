using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class KempeiScrambleController : MonoBehaviour
{
    private const float MinGlitchInterval = 0.18f;
    private const float MaxGlitchInterval = 0.36f;

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
    private Enemy _enemy;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnDisable()
    {
        ClearAffectedEnemies();
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

        _activeSnapshot.Clear();
        _activeSnapshot.AddRange(tracker.GetActiveEnemiesSnapshot());

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

        List<Enemy> toClear = null;
        foreach (Enemy enemy in _affectedEnemies)
        {
            if (enemy != null && _stillAffected.Contains(enemy))
                continue;

            toClear ??= new List<Enemy>();
            toClear.Add(enemy);
        }

        if (toClear == null)
            return;

        for (int i = 0; i < toClear.Count; i++)
        {
            Enemy enemy = toClear[i];
            if (enemy != null)
                enemy.ClearVisualCharacterOverride(this);

            _affectedEnemies.Remove(enemy);
            _activeScrambles.Remove(enemy);
        }
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
            NextToggleTime = Time.time + Random.Range(MinGlitchInterval, MaxGlitchInterval)
        };
        _activeScrambles[target] = state;
        return state;
    }

    private void ApplyScramblePulse(Enemy target, ScrambleState scramble, bool wasAffected)
    {
        if (Time.time >= scramble.NextToggleTime)
        {
            scramble.IsScrambledVisible = !scramble.IsScrambledVisible;
            scramble.NextToggleTime = Time.time + Random.Range(MinGlitchInterval, MaxGlitchInterval);
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
        {
            for (int i = 0; i < _activeSnapshot.Count; i++)
                AddIfWrongCharacter(_activeSnapshot[i] != null ? _activeSnapshot[i].Character : null, realCharacter);
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
        _activeScrambles.Clear();
    }
}
