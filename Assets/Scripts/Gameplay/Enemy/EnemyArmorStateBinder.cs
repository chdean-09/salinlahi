using UnityEngine;

/// <summary>
/// Maps the enemy's health state to an authored armor layer. It listens to actual non-lethal hits
/// separately from health restoration so healing to full reinstates the static armor without
/// mistakenly replaying its break animation.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class EnemyArmorStateBinder : MonoBehaviour
{
    private Enemy _enemy;
    private EnemyAbilityVisualPresenter _presenter;

    public void Bind(Enemy enemy, EnemyAbilityVisualPresenter presenter)
    {
        Unsubscribe();
        _enemy = enemy;
        _presenter = presenter;
        Subscribe();
        SyncFromHealth();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        _presenter?.SetActive(EnemyAbilityVisualId.Armor, false);
    }

    private void Subscribe()
    {
        if (_enemy == null || !isActiveAndEnabled)
            return;

        _enemy.HealthChanged -= HandleHealthChanged;
        _enemy.NonLethalDamageTaken -= HandleNonLethalDamageTaken;
        _enemy.HealthChanged += HandleHealthChanged;
        _enemy.NonLethalDamageTaken += HandleNonLethalDamageTaken;
    }

    private void Unsubscribe()
    {
        if (_enemy == null)
            return;

        _enemy.HealthChanged -= HandleHealthChanged;
        _enemy.NonLethalDamageTaken -= HandleNonLethalDamageTaken;
    }

    private void HandleHealthChanged(Enemy enemy, int previousHealth, int currentHealth)
    {
        SyncFromHealth();
    }

    private void HandleNonLethalDamageTaken(Enemy enemy, int previousHealth, int currentHealth)
    {
        if (_presenter == null
            || !_presenter.HasVisual(EnemyAbilityVisualId.Armor)
            || _enemy == null
            || previousHealth != _enemy.MaxHealth
            || currentHealth <= 0
            || currentHealth >= previousHealth)
        {
            return;
        }

        // HealthChanged has already removed the persistent layer. PlayExit replaces it for one
        // independent sequence, which continues even if hurt feedback pauses walk-frame changes.
        _presenter.PlayExit(EnemyAbilityVisualId.Armor);
    }

    private void SyncFromHealth()
    {
        if (_presenter == null || !_presenter.HasVisual(EnemyAbilityVisualId.Armor) || _enemy == null)
            return;

        if (!_enemy.IsDying && _enemy.CurrentHealth >= _enemy.MaxHealth)
        {
            _presenter.SetActive(EnemyAbilityVisualId.Armor, true);
        }
        else if (!_presenter.IsExitPlaying(EnemyAbilityVisualId.Armor))
        {
            _presenter.SetActive(EnemyAbilityVisualId.Armor, false);
        }
    }
}
