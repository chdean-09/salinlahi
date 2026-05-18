using System.Collections;
using UnityEngine;

// Drives the boss's transform per CurrentPhase.movementPattern.
// All coroutines use WaitForSeconds (scaled). Do not use WaitForSecondsRealtime.
// REWORK: refactored in Task 7 — movement now driven directly by BossController
// calling StartPattern/StopPattern/TeleportNow instead of EventBus subscriptions.
[RequireComponent(typeof(BossController))]
public class PhaseBasedMovement : MonoBehaviour
{
    [Header("Pace Pattern")]
    [Tooltip("Horizontal range (world units) the boss paces left/right around its starting X.")]
    [SerializeField] private float _paceHalfRange = 1.5f;

    [Header("Teleport Pattern")]
    [Tooltip("Seconds between teleport jumps.")]
    [SerializeField] private float _teleportInterval = 1.5f;
    [Tooltip("Horizontal range (world units) for teleport destination, around starting X.")]
    [SerializeField] private float _teleportHalfRange = 2.0f;

    private BossController _boss;
    private EnemyMover _mover;
    private Vector3 _baseLocalPosition;
    private Coroutine _movementRoutine;

    private void Awake()
    {
        _boss = GetComponent<BossController>();
        _mover = GetComponent<EnemyMover>();
        _baseLocalPosition = transform.localPosition;
    }

    // REWORK: refactored in Task 7 — previously subscribed to OnBossStarted,
    // OnBossPhaseStarted, OnBossPhaseAdsReturning, and OnBossDefeated via EventBus.
    // Those events were removed/renamed in the Task 2 EventBus refactor.
    // BossController now calls StartPattern/StopPattern/TeleportNow directly.

    public virtual void StartPattern(BossPhase phase)
    {
        if (phase == null) return;
        StopPattern();
        _movementRoutine = StartCoroutine(RunPattern(phase));
    }

    public virtual void StopPattern()
    {
        if (_movementRoutine != null)
        {
            StopCoroutine(_movementRoutine);
            _movementRoutine = null;
        }
        if (_mover != null) _mover.SetExternallyMoving(false);
    }

    public virtual void TeleportNow(BossPhase phase)
    {
        if (phase == null) return;
        float halfRange = phase.teleportHalfRange.x;
        float x = _baseLocalPosition.x + Random.Range(-halfRange, halfRange);
        transform.localPosition = new Vector3(x, _baseLocalPosition.y, _baseLocalPosition.z);
    }

    private IEnumerator RunPattern(BossPhase phase)
    {
        switch (phase.movementPattern)
        {
            case BossMovementPattern.Hover:
                yield break;

            case BossMovementPattern.Pace:
                if (_mover != null) _mover.SetExternallyMoving(true);
                yield return Pace(phase.movementSpeed, phase.paceHalfRange);
                break;

            case BossMovementPattern.Teleport:
                // Teleport cadence is driven by BossController calling TeleportNow
                // at each summon tick; this coroutine just holds until stopped.
                yield return new WaitUntil(() => false);
                break;
        }
    }

    private IEnumerator Pace(float speed, float halfRange)
    {
        float dir = 1f;
        float minX = _baseLocalPosition.x - halfRange;
        float maxX = _baseLocalPosition.x + halfRange;
        BossDamageFeedback dmgFeedback = GetComponent<BossDamageFeedback>();

        while (true)
        {
            if (dmgFeedback != null && dmgFeedback.IsHurtPaused)
            {
                yield return null;
                continue;
            }

            float newX = Mathf.Clamp(transform.localPosition.x + dir * speed * Time.deltaTime, minX, maxX);
            transform.localPosition = new Vector3(newX, transform.localPosition.y, transform.localPosition.z);
            if (newX >= maxX || newX <= minX) dir *= -1f;
            yield return null;
        }
    }
}
