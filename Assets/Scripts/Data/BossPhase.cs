using System.Collections.Generic;
using UnityEngine;

public enum BossMovementPattern { Hover, Pace, Teleport }

// Single phase definition embedded in BossConfigSO.phases. Phase clears when
// every requiredCharacters entry has been drawn exactly once, in any order.
[System.Serializable]
public class BossPhase
{
    [Header("Gate")]
    [Tooltip("Characters the player must draw (any order, each once) to clear this phase.")]
    public List<BaybayinCharacterSO> requiredCharacters;

    [Header("Movement")]
    public BossMovementPattern movementPattern;
    [Tooltip("Movement speed in world units per second. 0 = stationary (Hover) or teleport-only (Teleport).")]
    public float movementSpeed;

    [Header("Intermission (after this phase clears)")]
    [Tooltip("Mini-wave spawned before the next phase begins. Null = no intermission.")]
    public WaveConfigSO intermissionWave;
    [Tooltip("Seconds to wait after the intermission wave clears before the next phase starts.")]
    public float postIntermissionDelay;
}
