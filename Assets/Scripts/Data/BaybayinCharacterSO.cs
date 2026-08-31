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
    // NOTE: none of these three is a bare glyph on a transparent background. Every one is a
    // composed card or framed plate. There is currently no bare-glyph art in the project, which
    // is why anything wanting a clean tracing underlay has nothing good to point at.
    [Tooltip("Learning CARD, not a bare glyph: Resources/[ID].png is a filled panel carrying the " +
             "glyph AND its romanised syllable (BA-VA.png reads \"ba, va\"). Consumed by the " +
             "Tracing Dojo ghost and list, and by SymbolLearningCardController. Do NOT use it " +
             "anywhere the romanisation would give away an answer the player is being asked for.")]
    public Sprite displaySprite;

    [Tooltip("Stylized glyph card shown in the Almanac character grid and detail view (Art/UI/Almanac/[ID]-Almanac.png). If null, falls back to displaySprite.")]
    public Sprite almanacSprite;

    [Tooltip("Framed Baybayin glyph (Art/UI/GlyphBadges/[ID].png) displayed by EnemyGlyphBadge " +
             "above each enemy during gameplay. This — not displaySprite — is what appears on " +
             "enemies. It carries no romanisation, so it is also what the trace hint shows.")]
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
