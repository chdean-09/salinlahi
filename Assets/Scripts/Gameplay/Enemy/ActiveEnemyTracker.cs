using System.Collections.Generic;
using UnityEngine;

/// Tracks all active (on-screen, alive) enemies.
/// Enemies register on spawn and unregister on defeat or base hit.
public class ActiveEnemyTracker : Singleton<ActiveEnemyTracker>
{
    private readonly List<Enemy> _activeEnemies = new List<Enemy>();

    public int ActiveCount
    {
        get
        {
            CleanupStaleEntries();
            return _activeEnemies.Count;
        }
    }

    public bool IsClear => ActiveCount == 0;

    public void Register(Enemy enemy)
    {
        if (enemy == null)
            return;

        CleanupStaleEntries();
        if (!_activeEnemies.Contains(enemy))
            _activeEnemies.Add(enemy);
    }

    public void Unregister(Enemy enemy)
    {
        if (enemy == null)
            return;

        _activeEnemies.Remove(enemy);
    }

    public List<Enemy> GetActiveEnemiesSnapshot()
    {
        CleanupStaleEntries();
        return new List<Enemy>(_activeEnemies);
    }

    /// Returns the active enemy closest to the base (lowest Y)
    /// that carries the given characterID. Returns null if none.
    public Enemy FindClosestToBase(string characterID)
    {
        CleanupStaleEntries();

        Enemy closest = null;
        float lowestY = float.MaxValue;

        for (int i = 0; i < _activeEnemies.Count; i++)
        {
            Enemy e = _activeEnemies[i];
            if (e.Character == null) continue;
            if (e.Character.characterID != characterID) continue;

            float y = e.transform.position.y;
            if (y < lowestY)
            {
                lowestY = y;
                closest = e;
            }
        }

        return closest;
    }

    /// Returns all active enemies carrying the given characterID.
    /// Used later for AOE resolution in Sprint 3.
    public List<Enemy> FindAllWithCharacter(string characterID)
    {
        CleanupStaleEntries();

        List<Enemy> matches = new List<Enemy>();
        for (int i = 0; i < _activeEnemies.Count; i++)
        {
            Enemy e = _activeEnemies[i];
            if (e.Character != null
                && e.Character.characterID == characterID)
            {
                matches.Add(e);
            }
        }

        return matches;
    }

    /// No-argument overload: returns the enemy closest to base,
    /// regardless of character. Used by PromptUpdater for the HUD.
    public Enemy FindClosestToBase()
    {
        CleanupStaleEntries();

        Enemy closest = null;
        float lowestY = float.MaxValue;

        for (int i = 0; i < _activeEnemies.Count; i++)
        {
            Enemy e = _activeEnemies[i];
            float y = e.transform.position.y;
            if (y < lowestY)
            {
                lowestY = y;
                closest = e;
            }
        }

        return closest;
    }

    private void CleanupStaleEntries()
    {
        for (int i = _activeEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = _activeEnemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
                _activeEnemies.RemoveAt(i);
        }
    }
}
