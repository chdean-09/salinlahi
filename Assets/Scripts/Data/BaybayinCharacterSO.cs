using UnityEngine;

[CreateAssetMenu(fileName = "BaybayinChar", menuName = "Salinlahi/Baybayin Character")]
public class BaybayinCharacterSO : ScriptableObject
{
    [Header("Identity")]
    public string characterID; // "BA", "KA", "GA" -- must match template filename

    public string syllable; // "ba", "ka", "ga" -- shown to player

    [TextArea]
    [Tooltip("Almanac detail copy. Optional — the detail view omits empty text.")]
    public string description;

    [Header("Visuals")]
    public Sprite displaySprite; // The Baybayin glyph shown on the enemy

    [Tooltip("Framed Baybayin glyph displayed by EnemyGlyphBadge above each enemy during gameplay. Distinct from displaySprite, which is the bare glyph used by the Tracing Dojo.")]
    public Sprite badgeSprite;

    [Tooltip("Optional. Framed + glitched variant shown when a visual override (e.g. Kempei scramble) is active. If null, falls back to badgeSprite.")]
    public Sprite scrambledBadgeSprite;

    [Header("Audio")]
    public AudioClip pronunciationClip;

    [Header("Recognition")]
    [Tooltip("Filename in Resources/Templates/ without extension. Example: BA_template_01")]
    public string templateFileName;
}
