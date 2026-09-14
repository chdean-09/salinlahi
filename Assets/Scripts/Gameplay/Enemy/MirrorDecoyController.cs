using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Iligaw's signature ability: "It changes directions and creates false copies of the correct
/// symbol." The zigzag fields on the data handle the direction changes; this component spawns one
/// mirrored decoy copy beside the source, carrying the same glyph. The copy is a real decoy: drawing
/// its glyph while it is the closest match costs a heart (CombatResolver decoy penalty), it deals no
/// contact damage, and it leaves the field with its source.
/// Data-driven through <see cref="EnemyDataSO.spawnsMirrorDecoy"/>; Enemy.Initialize attaches this
/// component on the shared corruption shell and toggles it per spawn.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class MirrorDecoyController : MonoBehaviour
{
    // The copy now carries a different glyph from its source, so a player who has not memorised
    // the real symbol needs a tell that does not depend on reading the glyph at all. The decoy data
    // is named Anino - shadow - so it is drawn as one: translucent and darkened. Both the body and
    // its badge are dimmed, or a full-strength badge over a faded body reads as the real enemy.
    private const float DecoyAlpha = 0.45f;
    private static readonly Color DecoyShadowTint = new Color(0.55f, 0.58f, 0.7f);

    // One runtime decoy data per source data; never saved, never duplicated per spawn.
    private static readonly Dictionary<EnemyDataSO, EnemyDataSO> DecoyDataBySource = new();

    private Enemy _enemy;
    private Enemy _decoy;
    private SpriteRenderer _decoyRenderer;
    private bool _spawnAttempted;
    // Lane offset the copy holds relative to its source, so the pair stays side by side.
    private float _decoyOffsetX;

    public Enemy Decoy => _decoy;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        _spawnAttempted = false;
        _decoy = null;
        _decoyRenderer = null;
    }

    private void OnDisable()
    {
        ReleaseDecoy();
    }

    private void Update()
    {
        if (_spawnAttempted)
        {
            // The copy can leave on its own (it reached the base and was ignored); stop tracking it.
            if (_decoy != null && !_decoy.gameObject.activeInHierarchy)
            {
                _decoy = null;
                _decoyRenderer = null;
                return;
            }

            // The copy is carried by its source rather than walking itself. It used to run its own
            // mover, so any difference in pace pulled the pair apart - the level speed multiplier
            // applies to wave spawns and never reached a pool-spawned copy, and the two zigzags run
            // on independent phases. A reflection that drifts away stops reading as one, so the copy
            // is pinned beside the source every frame instead.
            if (_decoy != null)
                _decoy.transform.position =
                    transform.position + new Vector3(_decoyOffsetX, 0f, 0f);
            return;
        }

        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        EnemyDataSO data = _enemy != null ? _enemy.Data : null;
        if (data == null || !data.spawnsMirrorDecoy)
            return;

        // First Update runs after WaveSpawner has positioned the source and assigned its character;
        // OnEnable fires before either, at the off-screen pool position.
        _spawnAttempted = true;
        if (_enemy.IsDying || _enemy.Character == null)
            return;

        EnemyPool pool = EnemyPool.Instance;
        if (pool == null)
            return;

        Enemy decoy = pool.Get(GetDecoyData(data));
        if (decoy == null)
            return;

        decoy.AssignCharacter(PickDecoyCharacter(_enemy.Character));

        // Mirror toward the emptier side of the lane so the pair reads as a reflection.
        float side = transform.position.x >= 0f ? -1f : 1f;
        _decoyOffsetX = side * data.mirrorDecoyOffsetX;
        decoy.transform.position = transform.position + new Vector3(_decoyOffsetX, 0f, 0f);

        // Park the copy's own mover: its position is driven from the source from here on, so the
        // two cannot drift apart no matter what speed or zigzag either would have walked at.
        EnemyMover decoyMover = decoy.GetComponent<EnemyMover>();
        if (decoyMover != null)
            decoyMover.Stop();

        _decoyRenderer = decoy.GetComponent<SpriteRenderer>();
        if (_decoyRenderer != null)
        {
            _decoyRenderer.flipX = true;
            _decoyRenderer.color = Shadowed(_decoyRenderer.color);
        }

        SpriteRenderer decoyBadge = decoy.GlyphBadge != null
            ? decoy.GlyphBadge.GetComponent<SpriteRenderer>()
            : null;
        if (decoyBadge != null)
            decoyBadge.color = Shadowed(decoyBadge.color);

        _decoy = decoy;
    }

    /// <summary>Translucent, cooled-down version of a colour, so the copy reads as a shadow.</summary>
    private static Color Shadowed(Color c) =>
        new Color(c.r * DecoyShadowTint.r, c.g * DecoyShadowTint.g, c.b * DecoyShadowTint.b, DecoyAlpha);

    /// <summary>
    /// The glyph the mirrored copy carries: a symbol from the current level's pool that is NOT the
    /// source's own. Iligaw means "to lead astray" - the real one keeps its own symbol above it, and
    /// the fake beside it shows a different one, so the player has to read the pair rather than
    /// answer the silhouette. Drawing the copy's glyph is still the decoy penalty it always was.
    /// Falls back to mirroring the source when the level teaches nothing else, which restores the
    /// previous same-glyph behaviour rather than spawning a blank copy.
    /// </summary>
    private static BaybayinCharacterSO PickDecoyCharacter(BaybayinCharacterSO sourceCharacter)
    {
        LevelConfigSO level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
        if (level == null || level.cumulativeSymbolPool == null)
            return sourceCharacter;

        List<BaybayinCharacterSO> others = new List<BaybayinCharacterSO>();
        for (int i = 0; i < level.cumulativeSymbolPool.Count; i++)
        {
            BaybayinCharacterSO candidate = level.cumulativeSymbolPool[i]?.symbol;
            if (candidate == null || candidate == sourceCharacter) continue;
            if (!others.Contains(candidate)) others.Add(candidate);
        }

        if (others.Count == 0)
            return sourceCharacter;

        return others[Random.Range(0, others.Count)];
    }

    /// <summary>
    /// The decoy copy's data: same identity as the source, flagged as a decoy that deals no
    /// contact damage, never spawns its own copy, and never raises a discovery event. Cached per
    /// source so repeated spawns share one instance.
    /// </summary>
    public static EnemyDataSO GetDecoyData(EnemyDataSO source)
    {
        if (source == null)
            return null;

        if (DecoyDataBySource.TryGetValue(source, out EnemyDataSO cached) && cached != null)
            return cached;

        EnemyDataSO decoy = Instantiate(source);
        decoy.name = source.name + "_Anino";
        decoy.hideFlags = HideFlags.DontSave;
        decoy.isDecoy = true;
        decoy.dealsContactDamage = false;
        decoy.spawnsMirrorDecoy = false;
        decoy.stainsNearbyGlyphs = false;
        decoy.coversOwnGlyph = false;
        decoy.suppressDiscovery = true;
        DecoyDataBySource[source] = decoy;
        return decoy;
    }

    private void ReleaseDecoy()
    {
        if (_decoyRenderer != null)
            _decoyRenderer.flipX = false;

        if (_decoy != null && _decoy.gameObject.activeInHierarchy && !_decoy.IsDying)
            _decoy.ReturnToPool();

        _decoy = null;
        _decoyRenderer = null;
    }
}
