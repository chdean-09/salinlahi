using System.Collections.Generic;
using UnityEngine;

/// Tracks all active (on-screen, alive) enemies.
/// Enemies register on spawn and unregister on defeat or base hit.
public class ActiveEnemyTracker : Singleton<ActiveEnemyTracker>
{
    private readonly List<Enemy> _activeEnemies = new List<Enemy>();

    /// <summary>
    /// Reusable buffer for FindAllWithCharacter. Callers must NOT cache the returned list.
    /// </summary>
    private readonly List<Enemy> _characterMatchBuffer = new List<Enemy>();

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
    /// whose real combat character matches the given characterID. Returns null if none.
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

/// <summary>
    /// Returns all active enemies whose real combat character matches the given characterID.
    /// Used later for AOE resolution so decoy display labels do not affect the match set.
    /// <para><b>Do NOT cache the returned list</b> — it is reused across calls.</para>
    /// </summary>
    public List<Enemy> FindAllWithCharacter(string characterID)
    {
        CleanupStaleEntries();
        _characterMatchBuffer.Clear();
        for (int i = 0; i < _activeEnemies.Count; i++)
        {
            Enemy e = _activeEnemies[i];
            if (e.Character != null
                && e.Character.characterID == characterID)
            {
                _characterMatchBuffer.Add(e);
            }
        }

        return _characterMatchBuffer;
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
