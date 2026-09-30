using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the live state for Punit's TornContext ability. A living, unsuppressed Punit tears the
/// current clue in the HUD; the shared shell can be reused, so this state is cleared on every
/// pool/defeat path and re-established from the next spawn's data and introduction outcome.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class PunitTornController : MonoBehaviour
{
    private static readonly HashSet<PunitTornController> Registered = new();

    private Enemy _enemy;
    private bool _suppressedForIntroductionSpawn;

    public bool IsSuppressedForIntroductionSpawn => _suppressedForIntroductionSpawn;

    /// <summary>True while at least one live Punit has its ability enabled.</summary>
    public static bool IsAnyActive()
    {
        Registered.RemoveWhere(controller => !IsActiveNow(controller));
        return Registered.Count > 0;
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        if (_enemy != null)
            _enemy.HealthChanged += HandleHealthChanged;

        RefreshRegistration();
    }

    private void OnDisable()
    {
        if (_enemy != null)
            _enemy.HealthChanged -= HandleHealthChanged;

        ResetForPool();
    }

    private void OnDestroy()
    {
        Registered.Remove(this);
    }

    /// <summary>Re-establishes this spawn's state when an enabled shell is reused in place.</summary>
    public void ResetForSpawn()
    {
        _suppressedForIntroductionSpawn = false;
        RefreshRegistration();
    }

    /// <summary>
    /// Applies the introduction rule immediately. The first introduction spawn remains inert;
    /// later spawns become active without needing a new component enable callback.
    /// </summary>
    public void SetSuppressedForIntroductionSpawn(bool suppressed)
    {
        _suppressedForIntroductionSpawn = suppressed;
        RefreshRegistration();
    }

    /// <summary>Immediately removes Punit from the active set as soon as its defeat begins.</summary>
    public void NotifyDefeated()
    {
        Registered.Remove(this);
    }

    /// <summary>Clears state before this shell is parked or assigned another enemy type.</summary>
    public void ResetForPool()
    {
        Registered.Remove(this);
        _suppressedForIntroductionSpawn = false;
    }

    /// <summary>Test seam for the scene-independent live ability registry.</summary>
    public static void ResetRegistryForTests()
    {
        Registered.Clear();
    }

    private void HandleHealthChanged(Enemy enemy, int previousHealth, int currentHealth)
    {
        if (enemy == _enemy)
            RefreshRegistration();
    }

    private void RefreshRegistration()
    {
        if (IsActiveNow(this))
            Registered.Add(this);
        else
            Registered.Remove(this);
    }

    private static bool IsActiveNow(PunitTornController controller)
    {
        if (controller == null || !controller.isActiveAndEnabled || controller._suppressedForIntroductionSpawn)
            return false;

        Enemy enemy = controller._enemy != null ? controller._enemy : controller.GetComponent<Enemy>();
        return enemy != null
            && enemy.gameObject.activeInHierarchy
            && !enemy.IsDying
            && enemy.CurrentHealth > 0
            && enemy.Data != null
            && enemy.Data.learningAbility == EnemyLearningAbility.TornContext;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearRuntimeRegistry()
    {
        Registered.Clear();
    }
}
