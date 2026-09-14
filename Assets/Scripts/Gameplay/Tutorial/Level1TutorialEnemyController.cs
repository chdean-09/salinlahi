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
}
