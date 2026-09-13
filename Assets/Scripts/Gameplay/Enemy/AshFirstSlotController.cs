using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Abo ng Simula's signature ability: "It covers the first symbol of a word with ash." While an Abo
/// lives, the HUD's incomplete-word clue ashes over the word's <b>first</b> slot as well as the
/// target one, so the player cannot read the opening symbol
/// (<c>ActiveCluePresenter.BuildMaskedSpelling</c>).
///
/// <para>
/// <b>This ability changes only what is drawn.</b> Credit is decided in
/// <c>ActiveClueDirector.TryConsumeClue</c> from enemy identity, and recognition matching is a
/// <c>characterID</c> compare in <c>CombatResolver</c>; neither reads anything this ability
/// affects. A correct draw resolves exactly as it would with no Abo on screen — asserted, not
/// assumed, in AbongSimulaAshTests.
/// </para>
///
/// <para>
/// The controller itself carries no per-frame behaviour. Its whole job is to answer "is an Abo
/// alive right now?" for the presenter, and to stop answering yes the moment it dies, is disabled,
/// or is recycled — which is what ends the effect at the death and pool boundaries.
/// </para>
///
/// Data-driven through <see cref="EnemyDataSO.ashesFirstSlot"/>; Enemy.Initialize attaches this
/// component on the shared corruption shell and toggles it per spawn.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class AshFirstSlotController : MonoBehaviour
{
    /// <summary>
    /// Live Abo controllers. A set rather than a counter because a pooled shell that is disabled
    /// without its OnDisable running (domain reload, test teardown) would strand a counter
    /// permanently; membership can be re-verified instead, which <see cref="IsAnyActive"/> does.
    /// </summary>
    private static readonly HashSet<AshFirstSlotController> Registered = new();

    private Enemy _enemy;

    /// <summary>
    /// True while at least one Abo ng Simula is alive and armed. Self-healing: every call re-checks
    /// each registered controller and drops any that has been destroyed, disabled, recycled, had
    /// its flag cleared, or started dying — so the ash lifts on death and cannot outlive a spawn
    /// even if a disable callback is missed.
    /// </summary>
    public static bool IsAnyActive()
    {
        Registered.RemoveWhere(controller => !IsArmed(controller));
        return Registered.Count > 0;
    }

    /// <summary>Test seam: forget every registration. Never called from gameplay code.</summary>
    public static void ResetRegistryForTests() => Registered.Clear();

    private static bool IsArmed(AshFirstSlotController controller)
    {
        if (controller == null)
            return false;
        if (!controller.isActiveAndEnabled)
            return false;

        Enemy enemy = controller._enemy != null
            ? controller._enemy
            : controller.GetComponent<Enemy>();
        if (enemy == null || enemy.IsDying)
            return false;
        if (!enemy.gameObject.activeInHierarchy)
            return false;

        EnemyDataSO data = enemy.Data;
        return data != null && data.ashesFirstSlot;
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        Registered.Add(this);
    }

    private void OnDisable()
    {
        Registered.Remove(this);
    }

    private void OnDestroy()
    {
        Registered.Remove(this);
    }

    /// <summary>
    /// Re-asserts this controller's registration. Public so the ability can be driven without
    /// frames in tests, mirroring <see cref="GlyphCoverController.Tick"/>. EditMode never fires
    /// OnEnable, so a test drives registration through here.
    /// <para>
    /// <paramref name="deltaTime"/> is unused: the ability has no timer. Withdrawal is handled by
    /// <see cref="IsAnyActive"/>'s re-check rather than by counting down here.
    /// </para>
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        if (IsArmed(this))
            Registered.Add(this);
        else
            Registered.Remove(this);
    }
}
