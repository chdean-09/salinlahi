using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class KempeiScrambleController : MonoBehaviour
{
    private readonly HashSet<Enemy> _affectedEnemies = new();
    private readonly HashSet<Enemy> _stillAffected = new();
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

            BaybayinCharacterSO wrongCharacter = SelectWrongCharacter(target.Character);
            if (wrongCharacter == null)
                continue;

            target.ApplyVisualCharacterOverride(this, wrongCharacter);
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
        if (candidate == null || candidate == realCharacter)
            return;

        if (realCharacter != null && candidate.characterID == realCharacter.characterID)
            return;

        if (!_candidateCharacters.Contains(candidate))
            _candidateCharacters.Add(candidate);
    }

    private void ClearAffectedEnemies()
    {
        if (_affectedEnemies.Count == 0)
            return;

        foreach (Enemy enemy in _affectedEnemies)
        {
            if (enemy != null)
                enemy.ClearVisualCharacterOverride(this);
        }

        _affectedEnemies.Clear();
    }
}
