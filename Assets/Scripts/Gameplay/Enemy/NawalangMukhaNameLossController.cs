using UnityEngine;

/// <summary>
/// Data-driven Nawalang Mukha identity loss. While this pooled enemy is alive,
/// speaker/name labels are hidden globally; dialogue text and gameplay objectives remain.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class NawalangMukhaNameLossController : MonoBehaviour
{
    private void OnEnable()
    {
        NameLossEffectRegistry.Register(this);
    }

    private void OnDisable()
    {
        NameLossEffectRegistry.Unregister(this);
    }
}
