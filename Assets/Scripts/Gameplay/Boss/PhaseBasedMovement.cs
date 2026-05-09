using System.Collections;
using UnityEngine;

// Drives the boss's transform per CurrentPhase.movementPattern.
// Subscribes to EventBus boss events; never touches BossController internals.
// All coroutines use WaitForSeconds (scaled). Do not use WaitForSecondsRealtime.
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
    private Vector3 _baseLocalPosition;
    private Coroutine _movementRoutine;

    private void Awake()
    {
        _boss = GetComponent<BossController>();
        _baseLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        EventBus.OnBossPhaseStarted += HandlePhaseStarted;
        EventBus.OnBossPhaseCleared += HandlePhaseCleared;
        EventBus.OnBossIntermissionStarted += StopMovement;
        EventBus.OnBossDefeated += StopMovement;
    }

    private void OnDisable()
    {
        EventBus.OnBossPhaseStarted -= HandlePhaseStarted;
        EventBus.OnBossPhaseCleared -= HandlePhaseCleared;
        EventBus.OnBossIntermissionStarted -= StopMovement;
        EventBus.OnBossDefeated -= StopMovement;
        StopMovement();
    }

    private void HandlePhaseStarted(int phaseIndex)
    {
        if (_boss == null || _boss.CurrentPhase == null) return;
        StopMovement();
        _baseLocalPosition = transform.localPosition;
        _movementRoutine = StartCoroutine(RunPattern(_boss.CurrentPhase));
    }

    private void HandlePhaseCleared(int phaseIndex) => StopMovement();

    private void StopMovement()
    {
        if (_movementRoutine != null)
        {
            StopCoroutine(_movementRoutine);
            _movementRoutine = null;
        }
    }

    private IEnumerator RunPattern(BossPhase phase)
    {
        switch (phase.movementPattern)
        {
            case BossMovementPattern.Hover:
                // Stationary — nothing to do beyond holding position.
                yield break;

            case BossMovementPattern.Pace:
                yield return Pace(phase.movementSpeed);
                break;

            case BossMovementPattern.Teleport:
                yield return Teleport();
                break;
        }
    }

    private IEnumerator Pace(float speed)
    {
        float dir = 1f;
        float minX = _baseLocalPosition.x - _paceHalfRange;
        float maxX = _baseLocalPosition.x + _paceHalfRange;
        while (true)
        {
            float newX = Mathf.Clamp(transform.localPosition.x + dir * speed * Time.deltaTime, minX, maxX);
            transform.localPosition = new Vector3(newX, transform.localPosition.y, transform.localPosition.z);
            if (newX >= maxX || newX <= minX) dir *= -1f;
            yield return null;
        }
    }

    private IEnumerator Teleport()
    {
        while (true)
        {
            yield return new WaitForSeconds(_teleportInterval);
            float x = _baseLocalPosition.x + Random.Range(-_teleportHalfRange, _teleportHalfRange);
            transform.localPosition = new Vector3(x, _baseLocalPosition.y, _baseLocalPosition.z);
        }
    }
}
