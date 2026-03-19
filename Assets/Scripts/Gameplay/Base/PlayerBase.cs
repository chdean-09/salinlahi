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

    private void HandleBaseHit()
    {
        _heartSystem.LoseHeart();
    }
}