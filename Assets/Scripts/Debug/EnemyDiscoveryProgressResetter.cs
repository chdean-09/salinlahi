using System.Collections.Generic;
using UnityEngine;

namespace Salinlahi.Debug
{
    public sealed class EnemyDiscoveryProgressResetter : MonoBehaviour
    {
        [ContextMenu("Clear Enemy Discovery Progress")]
        public void ClearEnemyDiscoveryProgress()
        {
            EnemyDiscoveryProgress.ClearAllDiscovered();
            int replayedCount = ReplayActiveEnemyDiscoveries(replayFirstOnly: false);
            DebugLogger.Log($"Enemy discovery progress cleared. Replayed {replayedCount} active enemy discovery event(s) without marking them discovered.");
        }

        [ContextMenu("Clear And Replay First Active Enemy Discovery")]
        public void ClearAndReplayFirstActiveEnemyDiscovery()
        {
            EnemyDiscoveryProgress.ClearAllDiscovered();
            int replayedCount = ReplayActiveEnemyDiscoveries(replayFirstOnly: true);

            if (replayedCount > 0)
                DebugLogger.Log($"Enemy discovery progress cleared and replayed {replayedCount} active enemy discovery event without marking it discovered.");
        }

        private static int ReplayActiveEnemyDiscoveries(bool replayFirstOnly)
        {
            if (TutorialRuntimeState.IsActive)
            {
                DebugLogger.LogWarning("Enemy discovery progress cleared, but discovery UI is suppressed while the tutorial is active.");
                return 0;
            }

            ActiveEnemyTracker tracker = ActiveEnemyTracker.Instance;
            if (tracker == null)
            {
                DebugLogger.LogWarning("Enemy discovery progress cleared, but no ActiveEnemyTracker exists to replay an active enemy.");
                return 0;
            }

            HashSet<string> replayedEnemyIDs = new HashSet<string>();
            int replayedCount = 0;
            foreach (Enemy enemy in tracker.GetActiveEnemiesSnapshot())
            {
                if (enemy == null || enemy.IsBoss || enemy.Data == null)
                    continue;

                string enemyID = NormalizeEnemyID(enemy.Data);
                if (enemyID == null || !replayedEnemyIDs.Add(enemyID))
                    continue;

                EventBus.RaiseEnemyDiscovered(enemy.Data, enemy);
                replayedCount++;

                if (replayFirstOnly)
                    return replayedCount;
            }

            if (replayedCount == 0)
                DebugLogger.LogWarning("Enemy discovery progress cleared, but no active non-boss enemy was available to replay.");

            return replayedCount;
        }

        private static string NormalizeEnemyID(EnemyDataSO data)
        {
            return EnemyDiscoveryProgress.NormalizeEnemyID(data);
        }
    }
}
