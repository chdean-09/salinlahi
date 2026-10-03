using UnityEngine;

/// <summary>
/// Data-driven Nawalang Mukha identity loss. While this pooled enemy is alive,
/// speaker/name labels are hidden globally; dialogue text and gameplay objectives remain.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class NawalangMukhaNameLossController : MonoBehaviour
{
    /// <summary>
    /// True while this spawn is the one that introduced its type, in which case the ability must do
    /// nothing at all. See <see cref="SetSuppressedForIntroductionSpawn"/>.
    /// </summary>
    private bool _suppressedForIntroductionSpawn;

    /// <summary>
    /// Makes this ability inert for one spawn — the spawn on which the enemy's introduction card
    /// plays — and arms it again on every later spawn of the type.
    ///
    /// <para>
    /// <b>Why the ability must not fire on the spawn that introduces it.</b> The introduction card
    /// states what the enemy does; the ability then proves it. Firing both at once gives the player
    /// no before to compare the after against, and for every Era 1 ability — each of which takes
    /// something away — an effect that was already running when the player first looked reads as the
    /// game being broken rather than as an enemy doing something. For name loss specifically, the
    /// romanisation labels winking out during the card would land in the same second as the card's
    /// own text arriving, and a first-time player attributes it to the card.
    /// </para>
    ///
    /// <para>
    /// <b>Pooling.</b> Suppression is per spawn, never per shell. <c>Enemy.Initialize</c> restates it
    /// on every spawn and <see cref="OnEnable"/> clears it, so a recycled shell always comes back
    /// unsuppressed — a stuck flag here would silently disable Nawalang Mukha's ability for the rest
    /// of the run, on a shell that looks identical to a working one.
    /// </para>
    /// </summary>
    public void SetSuppressedForIntroductionSpawn(bool suppressed)
    {
        if (_suppressedForIntroductionSpawn == suppressed)
        {
            // Still re-assert registration: Initialize calls this on a shell that was already
            // enabled, in which case OnEnable did not run and the registry state is whatever the
            // previous occupant left.
            ApplyRegistration();
            return;
        }

        _suppressedForIntroductionSpawn = suppressed;
        ApplyRegistration();
    }

    private void OnEnable()
    {
        // A pooled shell must not inherit the previous occupant's suppression.
        _suppressedForIntroductionSpawn = false;
        ApplyRegistration();
    }

    private void OnDisable()
    {
        NameLossEffectRegistry.Unregister(this);
    }

    /// <summary>
    /// The whole ability is its registry membership, so suppressing it is exactly withdrawing that
    /// membership. The registry is a ref-counted set, so registering an already-registered source
    /// and unregistering an absent one are both no-ops — which is what makes this safe to call from
    /// both the enable path and every per-spawn restatement.
    /// </summary>
    private void ApplyRegistration()
    {
        // isActiveAndEnabled is part of the condition, not an optimisation: the shared corruption
        // shell keeps this component attached and merely disabled for every enemy type that does not
        // remove names, and a disabled component that registered would hide every label in the game
        // for an enemy with no such ability.
        if (_suppressedForIntroductionSpawn || !isActiveAndEnabled)
            NameLossEffectRegistry.Unregister(this);
        else
            NameLossEffectRegistry.Register(this);
    }
}
