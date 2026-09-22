using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hati's signature ability: "A masked creature that splits into two smaller enemies. It divides
/// villagers and creates arguments between them." When the source is defeated it spawns
/// <see cref="EnemyDataSO.splitCount"/> pieces of <see cref="EnemyDataSO.splitSpawnData"/> around
/// its own position. When the active roster provides alternatives, each piece carries a different
/// learned glyph; a one-symbol roster falls back to the source. The pieces are real enemies: they
/// walk, deal contact damage, must be defeated for the wave to clear, and never split again.
/// Data-driven through <see cref="EnemyDataSO.splitsOnDefeat"/>; Enemy.Initialize attaches this
/// component on the shared corruption shell and toggles it per spawn; Enemy.Defeat invokes it.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class HatiSplitController : MonoBehaviour
{
    private Enemy _enemy;
    private bool _splitThisLife;

    /// <summary>
    /// True while this spawn is the one that introduced its type, in which case the split must not
    /// happen at all. See <see cref="SetSuppressedForIntroductionSpawn"/>.
    /// </summary>
    private bool _suppressedForIntroductionSpawn;

    /// <summary>True while this spawn is suppressed as its type's introduction spawn. Test/diagnostic seam, mirroring <see cref="AshFirstSlotController.IsSuppressedForIntroductionSpawn"/>.</summary>
    public bool IsSuppressedForIntroductionSpawn => _suppressedForIntroductionSpawn;

    /// <summary>
    /// Makes this ability inert for one spawn — the spawn on which the enemy's introduction card
    /// plays — and arms it again on every later spawn of the type.
    ///
    /// <para>
    /// <b>Why the split must not happen on the spawn that introduces it.</b> Hati's introduction is
    /// the player's first correct draw against him, and the reward for it is two more enemies. A
    /// player who has just been told what Hati is, and is then punished for beating him by a board
    /// that got worse, reads the split as the defeat having failed rather than as the enemy's
    /// ability. Worse, the pieces arrive while the card is still framing the source, so the portrait
    /// is naming one of three bodies. The card states the dividing; the next Hati performs it.
    /// </para>
    ///
    /// <para>
    /// Nothing to withdraw when suppression is switched on: unlike the cover, the shield or the
    /// stain, this ability has no standing effect to lift — it either spawns pieces at the moment of
    /// defeat or it does not, and pieces already on the field are real enemies of their own that the
    /// player must still clear.
    /// </para>
    ///
    /// <para>
    /// <b>Pooling.</b> Suppression is per spawn, never per shell. <c>Enemy.Initialize</c> restates it
    /// on every spawn and <see cref="OnEnable"/> clears it, so a recycled shell always comes back
    /// unsuppressed — a stuck flag here would silently disable Hati's split for the rest of the run,
    /// on a shell that looks identical to a working one.
    /// </para>
    /// </summary>
    public void SetSuppressedForIntroductionSpawn(bool suppressed)
    {
        _suppressedForIntroductionSpawn = suppressed;
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        _splitThisLife = false;
        // A pooled shell must not inherit the previous occupant's suppression.
        _suppressedForIntroductionSpawn = false;
    }

    /// <summary>
    /// Spawns the pieces. Safe to call more than once per life; only the first call splits, and a
    /// spawn suppressed as its type's introduction splits not at all.
    /// </summary>
    public void SpawnOnDefeat()
    {
        // The introduction spawn shows the enemy, never the dividing. Returning before
        // _splitThisLife is latched means an un-suppression later in this same life would still get
        // its split, which keeps the flag's meaning exactly "not right now" rather than "not ever".
        if (_suppressedForIntroductionSpawn)
            return;

        if (_splitThisLife)
            return;
        _splitThisLife = true;

        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        EnemyDataSO data = _enemy != null ? _enemy.Data : null;
        EnemyDataSO spawnData = ResolveSpawnData(data);
        if (spawnData == null)
            return;

        EnemyPool pool = EnemyPool.Instance;
        if (pool == null)
            return;

        BaybayinCharacterSO glyph = _enemy.Character;
        int count = Mathf.Max(1, data.splitCount);
        for (int i = 0; i < count; i++)
        {
            Enemy piece = pool.Get(spawnData);
            if (piece == null)
                continue;

            piece.transform.position = transform.position + SplitOffset(i, count, data.splitOffsetX);
            if (glyph != null)
                piece.AssignCharacter(SelectReviewCharacter(
                    glyph,
                    i,
                    count,
                    WaveManager.CurrentAllowedCharacters));
        }
    }

    /// <summary>
    /// Selects a different learned character for each split piece when the current wave has other
    /// learned characters available. Falling back to the source keeps Hati safe in a one-symbol
    /// roster and preserves the old split behavior for isolated tests or early content.
    /// </summary>
    public static BaybayinCharacterSO SelectReviewCharacter(
        BaybayinCharacterSO source,
        int pieceIndex,
        int pieceCount,
        IReadOnlyList<BaybayinCharacterSO> allowedCharacters)
    {
        if (source == null || allowedCharacters == null || allowedCharacters.Count == 0)
            return source;

        var alternatives = new List<BaybayinCharacterSO>();
        for (int i = 0; i < allowedCharacters.Count; i++)
        {
            BaybayinCharacterSO candidate = allowedCharacters[i];
            if (candidate == null || candidate == source)
                continue;
            if (string.Equals(candidate.characterID, source.characterID, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (!alternatives.Contains(candidate))
                alternatives.Add(candidate);
        }

        if (alternatives.Count == 0)
            return source;

        int index = Mathf.Abs(pieceIndex) % alternatives.Count;
        return alternatives[index];
    }

    /// <summary>
    /// The data the pieces spawn with, or null when the source does not split, names no piece data,
    /// or names piece data that would itself split (which would recurse forever).
    /// </summary>
    public static EnemyDataSO ResolveSpawnData(EnemyDataSO source)
    {
        if (source == null || !source.splitsOnDefeat)
            return null;

        EnemyDataSO piece = source.splitSpawnData;
        if (piece == null || piece == source || piece.splitsOnDefeat)
            return null;

        return piece;
    }

    /// <summary>
    /// Pieces fan out horizontally around the source: index 0 sits at -offset, the last at +offset,
    /// any in between spread evenly. A single piece sits on the source.
    /// </summary>
    public static Vector3 SplitOffset(int index, int count, float offsetX)
    {
        if (count <= 1)
            return Vector3.zero;

        float t = index / (float)(count - 1) * 2f - 1f;
        return new Vector3(t * offsetX, 0f, 0f);
    }
}
