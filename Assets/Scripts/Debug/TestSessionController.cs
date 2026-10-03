using UnityEngine;

/// <summary>
/// Always-compiled static hint holder referenced by CombatResolver and
/// RecognitionManager. Empty during normal gameplay; set only by the
/// dev-only session tool below.
/// </summary>
public partial class TestSessionController
{
    /// <summary>Empty during normal gameplay. Set only by the dev-only session tool.</summary>
    public static string IntendedCharacterID { get; set; } = "";
}

#if SALINLAHI_DEV || UNITY_EDITOR
/// <summary>
/// Debug tool for structured recognition test sessions.
/// Set the intended character, draw it, and the logger records the ground
/// truth for confusion matrix analysis. This is NOT used during normal gameplay.
/// Compiled only under UNITY_EDITOR or SALINLAHI_DEV — never in a release build.
/// See docs/release/RELEASE-PROFILE.md §6.
/// </summary>
public partial class TestSessionController : MonoBehaviour
{
    [SerializeField]
    private string _targetCharacter = "";

    public void SetTargetCharacter(string charID)
    {
        _targetCharacter = charID;
        IntendedCharacterID = charID;
        DebugLogger.Log(
            $"TestSession: Target set to {charID}");
    }

    public void EndSession()
    {
        IntendedCharacterID = "";
        _targetCharacter = "";
        DebugLogger.Log(
            "TestSession: Session ended.");
    }
}
#endif
