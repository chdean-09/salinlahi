using UnityEngine;

/// <summary>
/// Hati's signature ability: "A masked creature that splits into two smaller enemies. It divides
/// villagers and creates arguments between them." When the source is defeated it spawns
/// <see cref="EnemyDataSO.splitCount"/> pieces of <see cref="EnemyDataSO.splitSpawnData"/> around
/// its own position, each carrying the source's glyph. The pieces are real enemies: they walk,
/// deal contact damage, must be defeated for the wave to clear, and never split again.
/// Data-driven through <see cref="EnemyDataSO.splitsOnDefeat"/>; Enemy.Initialize attaches this
/// component on the shared corruption shell and toggles it per spawn; Enemy.Defeat invokes it.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class HatiSplitController : MonoBehaviour
{
    private Enemy _enemy;
    private bool _splitThisLife;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        _splitThisLife = false;
    }

    /// <summary>
    /// Spawns the pieces. Safe to call more than once per life; only the first call splits.
    /// </summary>
    public void SpawnOnDefeat()
    {
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
                piece.AssignCharacter(glyph);
        }
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
