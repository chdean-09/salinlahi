using UnityEngine;

// Listens for OnBaseHit and forwards it to HeartSystem.
[RequireComponent(typeof(HeartSystem))]
public class PlayerBase : MonoBehaviour
{
    private HeartSystem _heartSystem;

    private void Awake()
    {
        _heartSystem = GetComponent<HeartSystem>();
    }

    private void OnEnable()
    {
        EventBus.OnBaseHit += HandleBaseHit;
    }

    private void OnDisable()
    {
        EventBus.OnBaseHit -= HandleBaseHit;
    }

    private void HandleBaseHit(int damage)
    {
        // The onboarding heart-loss demo stages a base hit for teaching and must never charge for
        // it. It parks a real pooled enemy on the shrine to do so, so anything that reaches here
        // during its window is the demo's own prop leaking into the real accounting — which is
        // exactly how the demo came to cost two of three hearts. Loud on purpose: silently
        // swallowing it would hide the next version of the same bug.
        if (TutorialRuntimeState.IsHeartLossDemoActive)
        {
            DebugLogger.LogWarning(
                $"PlayerBase: ignored a real base hit of {damage} raised during the tutorial "
                + "heart-loss demo. The demo is a visual-only path (EventBus.OnTutorialBaseHitDemo) "
                + "and must not reach HeartSystem. Something on the field is still dealing contact "
                + "damage or being defeated through the combat path — see "
                + "Level1TutorialEnemyController.DespawnSilently.");
            return;
        }

        _heartSystem.LoseHeart(damage);
    }
}
