using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BaybayinChar", menuName = "Salinlahi/Baybayin Character")]
public class BaybayinCharacterSO : ScriptableObject
{
    [Header("Identity")]
    public string characterID; // "BA", "KA", "GA" -- must match template filename

    public string syllable; // "ba", "ka", "ga" -- shown to player

    [Header("Revised Campaign Identity")]
    public string stableId;
    public List<string> legacyAliases = new();
    public List<SpokenValueDefinition> spokenValues = new();
    public string firstIntroductionLevelId;

    [TextArea]
    [Tooltip("Almanac detail copy. Optional — the detail view omits empty text.")]
    public string description;

    [Header("Visuals")]
    public Sprite displaySprite; // The Baybayin glyph shown on the enemy

    [Tooltip("Stylized glyph shown in the Almanac character grid and detail view (Art/UI/Almanac/[ID]-Almanac.png). If null, falls back to displaySprite.")]
    public Sprite almanacSprite;

    [Tooltip("Framed Baybayin glyph displayed by EnemyGlyphBadge above each enemy during gameplay. Distinct from displaySprite, which is the bare glyph used by the Tracing Dojo.")]
    public Sprite badgeSprite;

    [Tooltip("Optional. Framed + glitched variant shown when a visual override (e.g. Kempei scramble) is active. If null, falls back to badgeSprite.")]
    public Sprite scrambledBadgeSprite;

    [Header("Audio")]
    public AudioClip pronunciationClip;

    [Header("Recognition")]
    [Tooltip("Filename in Resources/Templates/ without extension. Example: BA_template_01")]
    public string templateFileName;

    public bool TryGetSpokenValue(string spokenValueId, out SpokenValueDefinition value)
    {
        value = null;
        if (!ContentIdentity.IsCanonical(spokenValueId) || spokenValues == null)
            return false;

        int matchCount = 0;
        for (int i = 0; i < spokenValues.Count; i++)
        {
            SpokenValueDefinition candidate = spokenValues[i];
            if (candidate == null ||
                !string.Equals(candidate.stableId, spokenValueId, StringComparison.Ordinal))
            {
                continue;
            }

            matchCount++;
            value = candidate;
        }

        if (matchCount != 1)
        {
            value = null;
            return false;
        }

        return true;
    }
}
