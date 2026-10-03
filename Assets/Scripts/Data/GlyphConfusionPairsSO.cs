using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authored table of <b>visually confusable</b> Baybayin glyph pairs — glyphs a learner can mistake
/// for one another because the shapes differ by one mark, not because the syllables are related.
/// Level 1's pair is <c>A ᜀ</c> and <c>E/I ᜁ</c>, one dot apart.
///
/// <para>
/// Its one consumer today is <see cref="MirrorDecoyController"/>, Iligaw's false copy. That ability
/// used to pick a random other symbol from the level pool, so the copy could carry a glyph nothing
/// like its source. A player looking at an E/I source beside an NA copy learns only "one of these is
/// fake" — a lesson about decoys existing. The deception lesson the beat is staged for is "look
/// closely", and it only exists when the two glyphs are genuinely hard to separate. This table is
/// what makes the pair land reliably instead of by luck of the draw.
/// </para>
///
/// <para>
/// <b>Pairs are symmetric.</b> One authored row serves both directions, so an A source yields E/I
/// and an E/I source yields A. Authoring both directions would be two rows to keep in agreement and
/// a silent asymmetry the first time someone edited one of them, which is exactly the class of
/// content bug a lookup can rule out instead. A glyph may appear in several rows; the first row that
/// matches wins, so order the rows most-confusable first.
/// </para>
///
/// <para>
/// Matching is by <see cref="BaybayinCharacterSO.characterID"/> through
/// <see cref="BaybayinIdCanonicalizer"/> rather than by asset reference, so a duplicate or runtime
/// clone of a character asset still resolves to its partner. A glyph with no authored partner is not
/// an error: the caller falls back to its previous behaviour.
/// </para>
/// </summary>
[CreateAssetMenu(
    fileName = "GlyphConfusionPairs",
    menuName = "Salinlahi/Glyph Confusion Pairs")]
public sealed class GlyphConfusionPairsSO : ScriptableObject
{
    /// <summary>
    /// One symmetric pair of glyphs a learner can confuse for one another. Both ends must be
    /// assigned and distinct; a row that fails either check is skipped by the lookup rather than
    /// resolving to something arbitrary.
    /// </summary>
    [Serializable]
    public struct ConfusionPair
    {
        [Tooltip("One glyph of the pair. Order within a row is irrelevant — the lookup is symmetric.")]
        public BaybayinCharacterSO first;

        [Tooltip("The other glyph of the pair.")]
        public BaybayinCharacterSO second;

        [TextArea(1, 2)]
        [Tooltip("Authoring note: why these two are confusable (e.g. \"one dot apart\"). Never shown to the player.")]
        public string note;
    }

    [Tooltip("Symmetric confusable-glyph rows. The first row matching a source glyph supplies its partner, so order the most confusable pairs first.")]
    [SerializeField] private List<ConfusionPair> _pairs = new();

    /// <summary>Authored rows, for validation tooling and tests. Never mutated at runtime.</summary>
    public IReadOnlyList<ConfusionPair> Pairs => _pairs;

    /// <summary>
    /// The authored partner of <paramref name="source"/>, or false when this table says nothing
    /// about that glyph. Callers are expected to have their own fallback: a missing partner means
    /// "no opinion", never "no decoy".
    /// <para>
    /// A row is consulted from both ends, so one authored <c>A ↔ E/I</c> row answers an A source
    /// with E/I and an E/I source with A.
    /// </para>
    /// </summary>
    public bool TryGetPartner(BaybayinCharacterSO source, out BaybayinCharacterSO partner)
    {
        partner = null;
        if (source == null || _pairs == null)
            return false;

        string sourceId = CanonicalId(source);
        if (sourceId == null)
            return false;

        for (int i = 0; i < _pairs.Count; i++)
        {
            ConfusionPair pair = _pairs[i];
            if (!IsUsableRow(pair))
                continue;

            if (string.Equals(CanonicalId(pair.first), sourceId, StringComparison.Ordinal))
            {
                partner = pair.second;
                return true;
            }

            if (string.Equals(CanonicalId(pair.second), sourceId, StringComparison.Ordinal))
            {
                partner = pair.first;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A row is usable only when both ends resolve to distinct canonical IDs. A half-filled row is
    /// an authoring slip in progress, and a row pairing a glyph with itself would hand the caller
    /// back its own source — which for the decoy means a copy indistinguishable from the real
    /// enemy, the behaviour the pair table exists to replace.
    /// </summary>
    private static bool IsUsableRow(ConfusionPair pair)
    {
        string firstId = CanonicalId(pair.first);
        string secondId = CanonicalId(pair.second);
        return firstId != null
            && secondId != null
            && !string.Equals(firstId, secondId, StringComparison.Ordinal);
    }

    private static string CanonicalId(BaybayinCharacterSO character)
    {
        if (character == null || string.IsNullOrWhiteSpace(character.characterID))
            return null;

        string canonical = BaybayinIdCanonicalizer.Canonicalize(character.characterID);
        return string.IsNullOrWhiteSpace(canonical) ? null : canonical;
    }
}
