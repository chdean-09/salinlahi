using UnityEngine;

/// Debug tool for structured recognition test sessions.
/// Set the intended character, draw it, and the logger
/// records the ground truth for confusion matrix analysis.
/// This is NOT used during normal gameplay.
public class TestSessionController : MonoBehaviour
{
    /// Empty during normal gameplay.
    public static string IntendedCharacterID { get; set; }
        = "";

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
