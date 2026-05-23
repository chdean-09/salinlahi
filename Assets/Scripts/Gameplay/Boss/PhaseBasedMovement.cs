using System.Collections;
using UnityEngine;

// Drives the boss's transform per a BossPhase passed in by BossController.
// No EventBus subscriptions — invoked imperatively. All coroutines use
// WaitForSeconds (scaled). Do not use WaitForSecondsRealtime.
[RequireComponent(typeof(BossController))]
public class PhaseBasedMovement : MonoBehaviour
{
    private EnemyMover _mover;
    private Vector3 _baseLocalPosition;
    private Coroutine _movementRoutine;
    private BossSummonTicker _summonTicker;
    private BossDamageFeedback _dmgFeedback;
    private bool _baseCaptured;

    private void Awake()
    {
        _mover = GetComponent<EnemyMover>();
        _summonTicker = GetComponent<BossSummonTicker>();
        _dmgFeedback = GetComponent<BossDamageFeedback>();
    }

    private void OnDisable()
    {
        StopPattern();
        _baseCaptured = false;
    }

    // virtual so SpyPhaseBasedMovement in BossControllerTests can intercept.
    public virtual void StartPattern(BossPhase phase)
    {
        if (phase == null) return;

        // Capture the boss's spawn position the first time movement runs —
        // by then WaveSpawner has placed it. Awake catches only (0, 9999, 0).
        if (!_baseCaptured)
        {
            _baseLocalPosition = transform.localPosition;
            _baseCaptured = true;
        }

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

    // Imperative one-shot relocation used by BossController on each
    // Teleport-pattern summon tick (spec §10). virtual for test interception.
    public virtual void TeleportNow(BossPhase phase)
    {
        if (phase == null) return;
        if (!_baseCaptured)
        {
            _baseLocalPosition = transform.localPosition;
            _baseCaptured = true;
        }
        float x = _baseLocalPosition.x
            + Random.Range(-phase.teleportHalfRange.x, phase.teleportHalfRange.x);
        float y = _baseLocalPosition.y
            + Random.Range(-phase.teleportHalfRange.y, 0f);
        transform.localPosition = new Vector3(x, y, _baseLocalPosition.z);
    }

    private IEnumerator RunPattern(BossPhase phase)
    {
        switch (phase.movementPattern)
        {
            case BossMovementPattern.Hover:
                yield break;

            case BossMovementPattern.Pace:
                if (_mover != null) _mover.SetExternallyMoving(true);
                yield return Pace(phase);
                break;

            case BossMovementPattern.Teleport:
                // Teleport is event-driven by BossController.TeleportNow();
                // nothing continuous to do here.
                while (true) yield return null;
        }
    }

    private IEnumerator Pace(BossPhase phase)
    {
        float dir = 1f;
        float minX = _baseLocalPosition.x - phase.paceHalfRange;
        float maxX = _baseLocalPosition.x + phase.paceHalfRange;

        while (true)
        {
            if (_summonTicker != null && _summonTicker.IsPlayingSummonAnimation)
            {
                yield return null;
                continue;
            }
            if (_dmgFeedback != null && _dmgFeedback.IsHurtPaused)
            {
                yield return null;
                continue;
            }

            float newX = Mathf.Clamp(
                transform.localPosition.x + dir * phase.movementSpeed * Time.deltaTime,
                minX, maxX);
            transform.localPosition = new Vector3(newX, transform.localPosition.y, transform.localPosition.z);
            if (newX >= maxX || newX <= minX) dir *= -1f;
            yield return null;
        }
    }
}
