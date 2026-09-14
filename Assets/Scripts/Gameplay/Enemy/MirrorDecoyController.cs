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
public sealed class MirrorDecoyController : MonoBehaviour, IIntroducibleAbility
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

    /// <summary>
    /// True while this spawn is the one that introduced its type, in which case no copy may be
    /// spawned at all. See <see cref="SetSuppressedForIntroductionSpawn"/>.
    /// </summary>
    private bool _suppressedForIntroductionSpawn;

    /// <summary>
    /// Latched the moment a copy is actually placed on the field. Deliberately NOT
    /// <c>_decoy != null</c>: the copy can leave on its own — it reaches the base and is ignored —
    /// and <see cref="Update"/> then drops the reference, which would make an ability that has
    /// visibly fired start reporting that it has not. Per spawn, cleared in <see cref="OnEnable"/>,
    /// mirroring <c>AshFirstSlotController._armedThisSpawn</c>.
    /// </summary>
    private bool _decoySpawnedThisSpawn;

    public Enemy Decoy => _decoy;

    /// <summary>
    /// <see cref="IIntroducibleAbility.HasFiredThisSpawn"/>. Iligaw's ability is visible the
    /// instant the copy stands beside its source — one enemy has become two — so that placement is
    /// what the lesson's beat 2 waits on.
    /// </summary>
    public bool HasFiredThisSpawn => _decoySpawnedThisSpawn;

    /// <summary>True while this spawn is suppressed as its type's introduction spawn. Test/diagnostic seam, mirroring <see cref="AshFirstSlotController.IsSuppressedForIntroductionSpawn"/>.</summary>
    public bool IsSuppressedForIntroductionSpawn => _suppressedForIntroductionSpawn;

    /// <summary>
    /// <see cref="IIntroducibleAbility.CanFireThisSpawn"/>. Unlike the ash — which is a change this
    /// component makes to things already on screen — the copy is a second Enemy, and the only place
    /// one comes from is <see cref="EnemyPool"/>. With no pool there is nothing to place and
    /// <see cref="HasFiredThisSpawn"/> can never become true, so a lesson's beat 2 would sit out its
    /// whole arm timeout behind a dimmed, halted field waiting for a copy that cannot exist.
    ///
    /// <para>
    /// Deliberately only the pool. <c>Update</c>'s other conditions — the source still settling, no
    /// character assigned yet, the pool declining to hand one out — are all states a later frame can
    /// leave, so they are the wait's business, not this one's.
    /// </para>
    /// </summary>
    public bool CanFireThisSpawn => EnemyPool.Instance != null;

    /// <summary>
    /// Makes this ability inert for one spawn — the spawn on which the enemy's introduction card
    /// plays — and arms it again on every later spawn of the type.
    ///
    /// <para>
    /// <b>Why the copy must not appear on the spawn that introduces it.</b> Iligaw's introduction is
    /// also, in Level 1, the player's first successful drawing, and a decoy standing beside its
    /// source means a wrong guess costs a heart on the one attempt the player has no basis for
    /// making. Two bodies arriving at the same moment as the card that explains them also destroys
    /// the card's own framing: the player cannot tell which of the pair the portrait is naming. The
    /// card teaches the enemy; the next Iligaw teaches the deception.
    /// </para>
    ///
    /// <para>
    /// Suppression releases any copy already on the field, so toggling it mid-life cannot leave an
    /// orphan shadow walking with no source driving its position.
    /// </para>
    ///
    /// <para>
    /// <b>Pooling.</b> Suppression is per spawn, never per shell. <c>Enemy.Initialize</c> restates it
    /// on every spawn and <see cref="OnEnable"/> clears it, so a recycled shell always comes back
    /// unsuppressed.
    /// </para>
    /// </summary>
    public void SetSuppressedForIntroductionSpawn(bool suppressed)
    {
        if (_suppressedForIntroductionSpawn == suppressed)
            return;

        _suppressedForIntroductionSpawn = suppressed;
        if (suppressed)
            ReleaseDecoy();
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        _spawnAttempted = false;
        _decoy = null;
        _decoyRenderer = null;
        _decoySpawnedThisSpawn = false;
        // A pooled shell must not inherit the previous occupant's suppression.
        _suppressedForIntroductionSpawn = false;
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

        // The introduction spawn shows the enemy, never the deception. Returning before
        // _spawnAttempted is set means an un-suppression later in this same life would still get its
        // copy, which keeps the flag's meaning exactly "not right now" rather than "not ever".
        if (_suppressedForIntroductionSpawn)
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

        decoy.AssignCharacter(PickDecoyCharacter(_enemy.Character, data));

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
        _decoySpawnedThisSpawn = true;
    }

    /// <summary>Translucent, cooled-down version of a colour, so the copy reads as a shadow.</summary>
    private static Color Shadowed(Color c) =>
        new Color(c.r * DecoyShadowTint.r, c.g * DecoyShadowTint.g, c.b * DecoyShadowTint.b, DecoyAlpha);

    /// <summary>
    /// The glyph the mirrored copy carries: a symbol that is NOT the source's own. Iligaw means "to
    /// lead astray" - the real one keeps its own symbol above it, and the fake beside it shows a
    /// different one, so the player has to read the pair rather than answer the silhouette. Drawing
    /// the copy's glyph is still the decoy penalty it always was.
    ///
    /// <para>
    /// <b>The authored confusion pair comes first.</b> Picking at random from the level pool teaches
    /// only that a fake exists: a copy carrying NA beside a source carrying E/I is separable at a
    /// glance, so the player learns to count bodies rather than to read glyphs. The lesson this
    /// ability is staged for is "look closely", and it exists only when the two glyphs are genuinely
    /// hard to tell apart - Level 1's pair being A ᜀ and E/I ᜁ, one dot apart. The authored table on
    /// the enemy's own data supplies that partner deterministically instead of leaving it to the
    /// draw; see <see cref="GlyphConfusionPairsSO"/>.
    /// </para>
    ///
    /// <para>
    /// The partner is used <b>whether or not it is in the level pool</b>. A confusable glyph the
    /// level does not otherwise teach is still the most instructive possible false copy, and the copy
    /// is never an answerable target for a slot - only a penalty - so it cannot make the level
    /// unwinnable or imply a symbol the player is expected to know.
    /// </para>
    ///
    /// <para>
    /// Falls back to the previous behaviour - a random other symbol from the level's pool - when no
    /// table is authored or the source glyph has no partner in it, and finally to mirroring the
    /// source when the level teaches nothing else, rather than spawning a blank copy.
    /// </para>
    /// </summary>
    private static BaybayinCharacterSO PickDecoyCharacter(
        BaybayinCharacterSO sourceCharacter,
        EnemyDataSO sourceData)
    {
        GlyphConfusionPairsSO pairs = sourceData != null ? sourceData.mirrorDecoyConfusionPairs : null;
        if (pairs != null
            && pairs.TryGetPartner(sourceCharacter, out BaybayinCharacterSO partner)
            && partner != null
            && partner != sourceCharacter)
        {
            return partner;
        }

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
