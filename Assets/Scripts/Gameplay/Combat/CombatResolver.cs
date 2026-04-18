using UnityEngine;

/// Listens for OnCharacterRecognized and defeats the correct enemy.
/// This is the bridge between the recognition pipeline and the
/// enemy system. Without this, drawing does nothing.
public class CombatResolver : MonoBehaviour
{
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
        Enemy target = ActiveEnemyTracker.Instance
            .FindClosestToBase(characterID);

        if (target != null)
        {
            EventBus.RaiseEnemyTargeted(target);
            target.TakeDamage(1);
            DebugLogger.Log($"CombatResolver: Hit {characterID}");
        }
        else
        {
            EventBus.RaiseDrawingMissed();
            DebugLogger.Log(
                $"CombatResolver: No enemy carries "
                + $"{characterID} -- miss");
        }
    }
}
