using UnityEngine;

[System.Serializable]
public struct DialogueLine
{
    public string speakerName;
    public Sprite portrait;
    [TextArea(2, 6)] public string text;
}

[CreateAssetMenu(
    fileName = "New Dialogue",
    menuName = "Salinlahi/Dialogue")]
public class DialogueSO : ScriptableObject
{
    public DialogueLine[] lines;
}