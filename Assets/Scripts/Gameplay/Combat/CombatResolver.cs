using UnityEngine;
using System.Collections.Generic;

/// Listens for OnCharacterRecognized and defeats the correct enemy.
/// This is the bridge between the recognition pipeline and the
/// enemy system. Without this, drawing does nothing.
public class CombatResolver : MonoBehaviour
{
    [Tooltip("Minimum matching on-screen enemies required to trigger an AOE mass-defeat.")]
    [SerializeField, Min(1)] private int _aoeThreshold = 3;

    private void OnEnable()
    {
        EventBus.OnCharacterRecognized += HandleCharacterRecognized;
    }

    private void OnDisable()
    {
        EventBus.OnCharacterRecognized -= HandleCharacterRecognized;
    }

    private void HandleCharacterRecognized(string characterID)
    {
        ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
        if (tracker == null)
            return;

        List<Enemy> matches = tracker.FindAllWithCharacter(characterID);

        // Real-match count: decoys and bosses cannot enable an AOE burst. Decoys
        // remain on screen as their own threat; burst is a reward path for sets
        // of legitimate enemies only.
        int realMatchCount = 0;
        if (matches != null)
        {
            for (int i = 0; i < matches.Count; i++)
            {
                Enemy m = matches[i];
                if (m == null) continue;
                if (m.IsBoss) continue;
                if (m.IsDecoy) continue;
                if (m.Data == null) continue;
                realMatchCount++;
            }
        }

        if (realMatchCount >= _aoeThreshold)
        {
            // Snapshot to a local list because TakeDamage -> Defeat -> Unregister
            // mutates the tracker's shared buffer mid-iteration.
            var burstTargets = new List<Enemy>(matches);
            int defeatedCount = 0;

            for (int i = 0; i < burstTargets.Count; i++)
            {
                Enemy candidate = burstTargets[i];
                if (candidate == null) continue;
                if (candidate.IsBoss) continue;
                if (candidate.IsDecoy) continue;
                if (candidate.Data == null) continue;

                EventBus.RaiseEnemyTargeted(candidate);
                candidate.TakeDamage(candidate.Data.maxHealth);
                defeatedCount++;
            }

            if (defeatedCount > 0)
            {
                EventBus.RaiseAOETriggered(defeatedCount);
                DebugLogger.Log($"CombatResolver: AOE burst defeated {defeatedCount} for {characterID}");
            }

            return;
        }

        Enemy closestTarget = tracker.FindClosestToBase(characterID);
        if (closestTarget == null)
        {
            EventBus.RaiseDrawingMissed();
            DebugLogger.Log(
                $"CombatResolver: No enemy carries "
                + $"{characterID} -- miss");
            return;
        }

        ResolveMatchedEnemy(closestTarget, characterID);
    }

    private static void ResolveMatchedEnemy(Enemy target, string characterID)
    {
        if (target == null)
            return;

        if (target.IsDecoy)
        {
            EventBus.RaiseBaseHit(1);
            target.ApplyDecoyPenalty();

            RecognitionLogger.LogOutcome(
                outcome: "decoy_penalty",
                recognizedCharacterID: characterID,
                intendedCharacterID: TestSessionController.IntendedCharacterID);

            DebugLogger.Log($"CombatResolver: Decoy penalty on {characterID}");
        }
        else
        {
            EventBus.RaiseEnemyTargeted(target);
            target.TakeDamage(1);
            DebugLogger.Log($"CombatResolver: Hit {characterID}");
        }
    }
}
