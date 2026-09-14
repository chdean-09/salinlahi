using System.Collections;
using UnityEngine;

public sealed class Level1TutorialEnemyController
{
    private readonly Enemy _enemy;
    private readonly Collider2D _contactCollider;
    private readonly EnemyMover _mover;

    public Level1TutorialEnemyController(Enemy enemy)
    {
        _enemy = enemy;
        if (_enemy == null)
            return;

        _contactCollider = _enemy.GetComponent<Collider2D>();
        _mover = _enemy.GetComponent<EnemyMover>();
    }

    public Enemy Enemy => _enemy;

    public void DisableContactDamage()
    {
        if (_contactCollider != null)
            _contactCollider.enabled = false;
    }

    public void FreezeThreat()
    {
        _mover?.Stop();
        DisableContactDamage();
    }

    public void Defeat()
    {
        if (_enemy == null || _enemy.IsDying)
            return;

        _enemy.TakeDamage(Mathf.Max(1, _enemy.CurrentHealth));
    }

    // Extracted from SoloTeachBeat when that beat was retired: still shared by
    // HeartLossDemoBeat's descent, so it lives here rather than on any one beat.
    internal static IEnumerator WalkEnemyTo(Level1TutorialEnemyController controller, Vector3 targetPos, float duration)
    {
        if (controller == null || controller.Enemy == null || duration <= 0f)
        {
            if (controller != null && controller.Enemy != null)
                controller.Enemy.transform.position = targetPos;
            yield break;
        }

        Transform enemyTransform = controller.Enemy.transform;
        EnemyMover mover = controller.Enemy.GetComponent<EnemyMover>();

        if (mover != null) mover.SetExternallyMoving(true);

        Vector3 startPos = enemyTransform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);
            if (enemyTransform == null)
            {
                if (mover != null) mover.SetExternallyMoving(false);
                yield break;
            }
            enemyTransform.position = Vector3.Lerp(startPos, targetPos, eased);
            yield return null;
        }

        if (enemyTransform != null) enemyTransform.position = targetPos;
        if (mover != null) mover.SetExternallyMoving(false);
    }
}
