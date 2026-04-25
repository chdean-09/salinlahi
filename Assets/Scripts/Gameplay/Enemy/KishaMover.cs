using System.Collections;
using UnityEngine;

public class KishaMover : EnemyMover
{
    private enum ChargeState
    {
        Walking,
        Paused,
        Charging
    }

    private Enemy _enemy;
    private Coroutine _chargeRoutine;
    private ChargeState _state;
    private bool _warnedMissingCamera;

    protected override void OnEnable()
    {
        base.OnEnable();
        _enemy = GetComponent<Enemy>();
        _state = ChargeState.Walking;
        _warnedMissingCamera = false;
        RestartChargeRoutine();
    }

    protected override void OnDisable()
    {
        StopChargeRoutine();
        base.OnDisable();
    }

    public override void SetSpeed(float speed)
    {
        base.SetSpeed(speed);
        RestartChargeRoutine();
    }

    private void RestartChargeRoutine()
    {
        if (!isActiveAndEnabled)
            return;

        StopChargeRoutine();
        _chargeRoutine = StartCoroutine(ChargeRoutine());
    }

    private void StopChargeRoutine()
    {
        if (_chargeRoutine == null)
            return;

        StopCoroutine(_chargeRoutine);
        _chargeRoutine = null;
    }

    private IEnumerator ChargeRoutine()
    {
        yield return null;

        EnemyDataSO data = _enemy != null ? _enemy.Data : null;
        if (data == null)
            yield break;

        _state = ChargeState.Walking;
        float triggerY = Mathf.Clamp01(data.chargeTriggerYNormalized);

        while (isActiveAndEnabled && !HasReachedTriggerY(triggerY))
            yield return null;

        if (!isActiveAndEnabled)
            yield break;

        _state = ChargeState.Paused;
        Stop();

        float pauseDuration = Mathf.Max(0f, data.pauseDuration);
        if (pauseDuration > 0f)
            yield return new WaitForSeconds(pauseDuration);

        if (!isActiveAndEnabled)
            yield break;

        _state = ChargeState.Charging;
        float multiplier = Mathf.Max(0f, data.chargeMultiplier);
        base.SetSpeed(data.moveSpeed * multiplier);
    }

    private bool HasReachedTriggerY(float triggerY)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            if (!_warnedMissingCamera)
            {
                DebugLogger.LogWarning("KishaMover: Camera.main is missing. Falling back to immediate charge trigger.");
                _warnedMissingCamera = true;
            }

            return true;
        }

        float viewportY = camera.WorldToViewportPoint(transform.position).y;
        return viewportY <= triggerY;
    }

#if UNITY_INCLUDE_TESTS
    public string ChargeStateForTests => _state.ToString();
#endif
}
