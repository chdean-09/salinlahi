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
    private const float DecoyAlpha = 0.8f;

    // One runtime decoy data per source data; never saved, never duplicated per spawn.
    private static readonly Dictionary<EnemyDataSO, EnemyDataSO> DecoyDataBySource = new();

    private Enemy _enemy;
    private Enemy _decoy;
    private SpriteRenderer _decoyRenderer;
    private bool _spawnAttempted;

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
            }
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

        decoy.AssignCharacter(_enemy.Character);

        // Mirror toward the emptier side of the lane so the pair reads as a reflection.
        float side = transform.position.x >= 0f ? -1f : 1f;
        decoy.transform.position = transform.position + new Vector3(side * data.mirrorDecoyOffsetX, 0f, 0f);

        _decoyRenderer = decoy.GetComponent<SpriteRenderer>();
        if (_decoyRenderer != null)
        {
            _decoyRenderer.flipX = true;
            Color color = _decoyRenderer.color;
            color.a = DecoyAlpha;
            _decoyRenderer.color = color;
        }

        _decoy = decoy;
    }

    /// <summary>
    /// The decoy copy's data: same identity and glyph as the source, flagged as a decoy that deals no
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
