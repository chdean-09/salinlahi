using UnityEngine;
using System.Collections.Generic;

/// Listens for OnCharacterRecognized and defeats the correct enemy.
/// This is the bridge between the recognition pipeline and the
/// enemy system. Without this, drawing does nothing.
public class CombatResolver : MonoBehaviour
{
    [Tooltip("Minimum matching on-screen enemies required to trigger an AOE mass-defeat.")]
    [SerializeField, Min(1)] private int _aoeThreshold = 3;
    [Header("Single Target Lethal Timing")]
    [Tooltip("First burst frame index (0-based) that can apply lethal damage.")]
    [SerializeField, Min(0)] private int _singleHitKillFrameMin = 3;
    [Tooltip("Last burst frame index (0-based) that can apply lethal damage.")]
    [SerializeField, Min(0)] private int _singleHitKillFrameMax = 4;
    private readonly HashSet<Enemy> _pendingLethalSingleHits = new HashSet<Enemy>();
    private readonly Dictionary<Enemy, int> _pendingLethalTriggerFrame = new Dictionary<Enemy, int>();

    private void OnEnable()
    {
        EventBus.OnCharacterRecognized += HandleCharacterRecognized;
        EventBus.OnSingleAttackVfxFrame += HandleSingleAttackVfxFrame;
        EventBus.OnSingleAttackVfxCompleted += HandleSingleAttackVfxCompleted;
    }

    private void OnDisable()
    {
        EventBus.OnCharacterRecognized -= HandleCharacterRecognized;
        EventBus.OnSingleAttackVfxFrame -= HandleSingleAttackVfxFrame;
        EventBus.OnSingleAttackVfxCompleted -= HandleSingleAttackVfxCompleted;
        _pendingLethalSingleHits.Clear();
        _pendingLethalTriggerFrame.Clear();
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

    private void ResolveMatchedEnemy(Enemy target, string characterID)
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
            EventBus.RaiseSingleAttackHit(target);

            bool willBeLethal = target.CurrentHealth <= 1;
            if (willBeLethal)
            {
                _pendingLethalSingleHits.Add(target);
                _pendingLethalTriggerFrame[target] = ChooseLethalTriggerFrame();
            }
            else
                target.TakeDamage(1);

            DebugLogger.Log($"CombatResolver: Hit {characterID}");
        }
    }

    private void HandleSingleAttackVfxFrame(Enemy target, int frameIndex)
    {
        if (target == null)
            return;

        if (!_pendingLethalSingleHits.Contains(target))
            return;

        int triggerFrame = 3;
        if (_pendingLethalTriggerFrame.TryGetValue(target, out int configuredFrame))
            triggerFrame = configuredFrame;

        if (frameIndex < triggerFrame)
            return;

        ApplyPendingLethalDamage(target);
    }

    private void HandleSingleAttackVfxCompleted(Enemy target)
    {
        if (target == null)
            return;

        if (!_pendingLethalSingleHits.Contains(target))
            return;

        ApplyPendingLethalDamage(target);
    }

    private void ApplyPendingLethalDamage(Enemy target)
    {
        _pendingLethalSingleHits.Remove(target);
        _pendingLethalTriggerFrame.Remove(target);

        if (target == null || target.IsDying || !target.gameObject.activeInHierarchy)
            return;

        target.TakeDamage(1);
    }

    private int ChooseLethalTriggerFrame()
    {
        int minFrame = Mathf.Max(0, _singleHitKillFrameMin);
        int maxFrame = Mathf.Max(0, _singleHitKillFrameMax);
        if (maxFrame < minFrame)
            maxFrame = minFrame;

        return Random.Range(minFrame, maxFrame + 1);
    }
}
