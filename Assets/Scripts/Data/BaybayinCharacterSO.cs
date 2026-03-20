using UnityEngine;

[CreateAssetMenu(fileName = "BaybayinChar", menuName = "Salinlahi/Baybayin Character")]
public class BaybayinCharacterSO : ScriptableObject
{
    [Header("Identity")]
    public string characterID; // "BA", "KA", "GA" -- must match template filename

    public string syllable; // "ba", "ka", "ga" -- shown to player

    [Header("Visuals")]
    public Sprite displaySprite; // The Baybayin glyph shown on the enemy

    [Header("Audio")]
    public AudioClip pronunciationClip;

    [Header("Recognition")]
    [Tooltip("Filename in Resources/Templates/ without extension. Example: BA_template")]
    public string templateFileName;
}
