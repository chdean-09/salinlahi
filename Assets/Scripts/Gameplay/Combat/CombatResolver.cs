using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// Listens for OnCharacterRecognized and defeats the correct enemy.
/// This is the bridge between the recognition pipeline and the
/// enemy system. Without this, drawing does nothing.
[DisallowMultipleComponent]
public class CombatResolver : MonoBehaviour
{
    [Tooltip("Minimum matching on-screen enemies required to trigger an AOE mass-defeat.")]
    [SerializeField, Min(1)] private int _aoeThreshold = 3;
    [SerializeField, Min(1)] private int _aoeDamagePerTarget = 1;
    [Header("AOE Defeat Timing")]
    [SerializeField] private bool _staggerAoeDefeats = false;
    [SerializeField] private bool _distanceWeightedDelay = true;
    [SerializeField] private Transform _baseAnchor;
    [SerializeField, Min(0f)] private float _aoeMinDelay = 0.02f;
    [SerializeField, Min(0f)] private float _aoeMaxDelay = 0.22f;
    [SerializeField, Min(0f)] private float _aoeRandomJitter = 0.06f;
    [SerializeField, Min(0f)] private float _aoeExtraRandomDelay = 0.12f;
    [SerializeField, Min(0f)] private float _aoeInitialDelayMin = 0.08f;
    [SerializeField, Min(0f)] private float _aoeInitialDelayMax = 0.2f;
    [Header("Pronunciation Timing")]
    [Tooltip("Small lead so pronunciation starts before damage resolves.")]
    [SerializeField, Min(0f)] private float _pronunciationLeadSeconds = 0.06f;
    private static CombatResolver _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
            return;
        }

        _instance = this;
    }

    private void OnEnable()
    {
        EventBus.OnCharacterRecognized += HandleCharacterRecognized;
        EnsureBaseAnchor();
    }

    private void OnDisable()
    {
        EventBus.OnCharacterRecognized -= HandleCharacterRecognized;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void HandleCharacterRecognized(string characterID)
    {
        if (TutorialRuntimeState.IsCombatOverrideActive)
            return;

        // Boss route — runs before AOE and closest-match. If the active boss
        // is targetable and the draw matches a required character, the boss
        // consumes the draw (Hit or Duplicate). Otherwise we fall through.
        BossController boss = GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null;
        if (boss != null && boss.IsTargetable)
        {
            BossRouteResult routed = boss.TryRouteDraw(characterID);
            if (routed != BossRouteResult.NotRouted)
                return;
        }

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
                if (!IsEligibleCombatTarget(m)) continue;
                if (m.IsBoss) continue;
                if (m.IsDecoy) continue;
                if (m.Data == null) continue;
                realMatchCount++;
            }
        }

        if (IsMultiKillChainEnabledForCurrentLevel()
            && realMatchCount >= _aoeThreshold)
        {
            // Snapshot to a local list because TakeDamage -> Defeat -> Unregister
            // mutates the tracker's shared buffer mid-iteration.
            var burstTargets = new List<Enemy>(matches);
            var chainTargets = new List<Enemy>(burstTargets.Count);
            int defeatedCount = 0;

            for (int i = 0; i < burstTargets.Count; i++)
            {
                Enemy candidate = burstTargets[i];
                if (candidate == null) continue;
                if (!IsEligibleCombatTarget(candidate)) continue;
                if (candidate.IsBoss) continue;
                if (candidate.IsDecoy) continue;
                if (candidate.Data == null) continue;

                chainTargets.Add(candidate);
                EventBus.RaiseEnemyTargeted(candidate);
                defeatedCount++;
            }

            if (chainTargets.Count > 0)
            {
                BaybayinCharacterSO pronunciationCharacter = chainTargets[0] != null ? chainTargets[0].Character : null;
                if (pronunciationCharacter != null)
                    EventBus.RaisePronunciationRequested(pronunciationCharacter);

                StartCoroutine(ApplyAoeDefeatAfterPronunciationLead(chainTargets));
            }

            if (defeatedCount > 0)
            {
                EventBus.RaiseAOETriggered(defeatedCount);
                DebugLogger.Log($"CombatResolver: AOE burst defeated {defeatedCount} for {characterID}");
            }

            return;
        }

        Enemy closestTarget = FindClosestEligibleMatch(matches);
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

    private static Enemy FindClosestEligibleMatch(List<Enemy> matches)
    {
        if (matches == null || matches.Count == 0)
            return null;

        Enemy closest = null;
        float lowestY = float.MaxValue;
        for (int i = 0; i < matches.Count; i++)
        {
            Enemy candidate = matches[i];
            if (!IsEligibleCombatTarget(candidate))
                continue;

            float y = candidate.transform.position.y;
            if (y < lowestY)
            {
                lowestY = y;
                closest = candidate;
            }
        }

        return closest;
    }

    private static bool IsEligibleCombatTarget(Enemy enemy)
    {
        if (enemy == null)
            return false;

        if (enemy.IsDying)
            return false;

        if (enemy.Data == null)
            return false;

        if (enemy.Data.isPhaser && !enemy.IsPhaserVisible)
            return false;

        // Bosses are excluded from AOE counts/bursts, but may still use the
        // single-target resolution path for boss-specific combat tuning.
        return true;
    }

    private static bool IsMultiKillChainEnabledForCurrentLevel()
    {
        LevelConfigSO levelConfig = GameManager.Instance != null
            ? GameManager.Instance.CurrentLevel
            : null;

        return levelConfig == null || levelConfig.multiKillChainEnabled;
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
            if (target.Character != null)
                EventBus.RaisePronunciationRequested(target.Character);

            if (_instance != null)
            {
                _instance.StartCoroutine(_instance.ResolveMatchedEnemyAfterPronunciationLead(target, characterID));
            }
            else
            {
                EventBus.RaiseEnemyTargeted(target);
                EventBus.RaiseSingleAttackHit(target);
                target.TakeDamage(1);
                DebugLogger.Log($"CombatResolver: Hit {characterID}");
            }
        }
    }

    private IEnumerator ResolveMatchedEnemyAfterPronunciationLead(Enemy target, string characterID)
    {
        float delay = Mathf.Max(0f, _pronunciationLeadSeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!IsEligibleCombatTarget(target))
            yield break;

        EventBus.RaiseEnemyTargeted(target);
        EventBus.RaiseSingleAttackHit(target);
        target.TakeDamage(1);
        DebugLogger.Log($"CombatResolver: Hit {characterID}");
    }

    private IEnumerator ApplyAoeDefeatAfterPronunciationLead(List<Enemy> targets)
    {
        float delay = Mathf.Max(0f, _pronunciationLeadSeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // Broadcast once for AOE-wide systems (audio/UI counters) at execution time.
        ApplyAoeDefeat(targets);
        EventBus.RaiseChainAttackHit(targets);
    }

    private void ApplyAoeDefeat(List<Enemy> targets)
    {
        if (targets == null || targets.Count == 0)
            return;

        if (!_staggerAoeDefeats)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                EventBus.RaiseChainAttackStep(targets[i]);
                DefeatTargetImmediate(targets[i], _aoeDamagePerTarget);
            }
            return;
        }

        EnsureBaseAnchor();
        float minDistance = float.MaxValue;
        float maxDistance = float.MinValue;

        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null)
                continue;

            float distance = GetDistanceToBase(enemy.transform.position);
            if (distance < minDistance) minDistance = distance;
            if (distance > maxDistance) maxDistance = distance;
        }

        if (minDistance == float.MaxValue)
        {
            minDistance = 0f;
            maxDistance = 0f;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            Enemy enemy = targets[i];
            if (enemy == null)
                continue;

            float delay = ComputeAoeDelay(enemy.transform.position, minDistance, maxDistance);
            StartCoroutine(DefeatTargetAfterDelay(enemy, delay));
        }
    }

    private IEnumerator DefeatTargetAfterDelay(Enemy target, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        EventBus.RaiseChainAttackStep(target);
        DefeatTargetImmediate(target, _aoeDamagePerTarget);
    }

    private static void DefeatTargetImmediate(Enemy target, int damage)
    {
        if (!IsEligibleCombatTarget(target))
            return;

        if (target.Data == null)
            return;

        target.TakeDamage(Mathf.Max(1, damage));
    }

    private float ComputeAoeDelay(Vector3 enemyPosition, float minDistance, float maxDistance)
    {
        float initialMin = Mathf.Min(_aoeInitialDelayMin, _aoeInitialDelayMax);
        float initialMax = Mathf.Max(_aoeInitialDelayMin, _aoeInitialDelayMax);
        float initialDelay = initialMax > 0f ? Random.Range(initialMin, initialMax) : 0f;

        float spanDelay = Mathf.Max(0f, _aoeMaxDelay - _aoeMinDelay);
        float weightedDelay = _aoeMinDelay;

        if (_distanceWeightedDelay && maxDistance - minDistance > 0.0001f)
        {
            float distance = GetDistanceToBase(enemyPosition);
            float t = Mathf.InverseLerp(minDistance, maxDistance, distance);
            weightedDelay = _aoeMinDelay + (spanDelay * t);
        }
        else if (spanDelay > 0f)
        {
            weightedDelay = Random.Range(_aoeMinDelay, _aoeMaxDelay);
        }

        float jitter = _aoeRandomJitter > 0f ? Random.Range(0f, _aoeRandomJitter) : 0f;
        float extraRandom = _aoeExtraRandomDelay > 0f ? Random.Range(0f, _aoeExtraRandomDelay) : 0f;
        return initialDelay + weightedDelay + jitter + extraRandom;
    }

    private float GetDistanceToBase(Vector3 enemyPosition)
    {
        if (_baseAnchor == null)
            return enemyPosition.magnitude;

        return Vector3.Distance(enemyPosition, _baseAnchor.position);
    }

    private void EnsureBaseAnchor()
    {
        if (_baseAnchor != null)
            return;

        GameObject baseObject = GameObject.FindGameObjectWithTag("PlayerBase");
        if (baseObject != null)
            _baseAnchor = baseObject.transform;
    }
}
