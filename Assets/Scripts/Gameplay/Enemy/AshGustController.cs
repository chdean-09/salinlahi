using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The gust that announces Abo ng Simula's ash arming: a grey sweep that leaves the Abo and
/// arrives at the HUD clue panel, where <see cref="ActiveCluePresenter"/> crumbles the readable
/// letters to their mask as it lands.
///
/// <para>
/// <b>Why a gust exists at all.</b> The ash is a HUD change made by an enemy standing somewhere
/// else on screen. Without a travelling cue the clue panel simply reads differently one frame
/// later, in a different screen region from anything the player just did, and a first-time player
/// credits their own last action instead of the Abo — the "bug, not threat" failure. The gust is
/// the attribution, and Abo's authored description asks for it directly: "Its face disappears
/// whenever the wind blows."
/// </para>
///
/// <para>
/// <b>No new art.</b> Everything here is driven by an authored VFX prefab reference and a tint,
/// both serialized. The intended wiring is <c>Assets/Prefabs/VFX/SingleAttackHitVFX.prefab</c>
/// re-tinted ash-grey: it is already a short burst that plays and ends on its own. The tint is
/// applied to whatever the prefab actually renders — sprite renderers and particle systems alike —
/// so an authored particle variant drops in later with no code change.
/// </para>
///
/// <para>
/// <b>Scene-level, not enemy-level.</b> The gust needs the clue panel's position, which is a HUD
/// fact rather than an enemy fact, and it has to outlive the Abo that fired it (the sweep is still
/// in flight while the player may already be drawing). So one of these lives on the HUD/VFX root
/// and the firing Abo asks it to play, rather than every pooled enemy shell carrying a copy of the
/// wiring.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class AshGustController : MonoBehaviour
{
    /// <summary>
    /// The most recently enabled gust presenter. A static handle rather than a singleton base
    /// class because the firing side (<see cref="AshFirstSlotController"/>) lives on a pooled
    /// enemy shell and must not take a hard reference to a HUD object that may not exist on a
    /// level without the ability.
    /// </summary>
    private static AshGustController _active;

    [Header("Reused VFX")]
    [Tooltip("Authored burst prefab to sweep. Intended wiring: an ash-grey variant of "
             + "Assets/Prefabs/VFX/SingleAttackHitVFX.prefab. No gust plays while this is empty.")]
    [SerializeField] private GameObject _gustVfxPrefab;

    [Tooltip("Colour every renderer in the spawned VFX is tinted to. Ash-grey by default: the "
             + "reused prefab is authored as a bright attack hit, which would read as a strike "
             + "rather than as wind.")]
    [SerializeField] private Color _ashTint = new Color(0.63f, 0.61f, 0.58f, 0.9f);

    [Tooltip("Sorting order for the sweeping VFX. Above the enemy glyph badges it passes, so the "
             + "gust is never occluded by the scrolls it blows across.")]
    [SerializeField] private int _gustSortingOrder = RenderOrder.EnemyGlyphBadge;

    [Header("Sweep")]
    [Tooltip("Seconds from leaving the Abo to arriving at the clue panel. 0.6 s per the Level 1 "
             + "design: one continuous motion, with the clue readable right up to the frame it "
             + "lands. Keep this in step with ActiveCluePresenter's crumble lead plus duration.")]
    [SerializeField, Min(0.05f)] private float _gustDurationSeconds = 0.6f;

    [Tooltip("Optional world-space stand-in for the HUD clue panel. Wire an empty child of the "
             + "stage placed under the panel; the viewport fallback below is used when empty.")]
    [SerializeField] private Transform _cluePanelAnchor;

    [Tooltip("Fallback destination as a viewport point (0-1, origin bottom-left) when no anchor "
             + "is wired. Defaults to the top-centre of the screen, where the clue panel sits.")]
    [SerializeField] private Vector2 _cluePanelViewportPoint = new Vector2(0.5f, 0.88f);

    [Tooltip("Distance in front of the camera the viewport fallback is projected to, in world "
             + "units. Matches the gameplay plane so the sweep stays in focus and in scale.")]
    [SerializeField, Min(0.1f)] private float _cluePanelViewportDepth = 10f;

    [Tooltip("How far the sweep bows away from a straight line, in world units. A dead-straight "
             + "streak reads as a projectile; the arc reads as wind.")]
    [SerializeField] private float _sweepArcHeight = 1.2f;

    [Tooltip("Scale the VFX starts at, where 1 is the prefab's authored scale.")]
    [SerializeField, Min(0.01f)] private float _startScale = 0.85f;

    [Tooltip("Scale the VFX ends at. Growing slightly as it crosses the screen reads as ash "
             + "spreading rather than as an object flying.")]
    [SerializeField, Min(0.01f)] private float _endScale = 1.35f;

    [Tooltip("Fraction of the sweep spent fading out, measured from the end. The ash disperses "
             + "into the clue panel instead of popping out of existence on arrival.")]
    [SerializeField, Range(0f, 1f)] private float _fadeOutFraction = 0.35f;

    /// <summary>
    /// How long a gust takes, so a caller staging its own reaction can line up with the arrival
    /// instead of hardcoding the same number twice.
    /// </summary>
    public float GustDurationSeconds => _gustDurationSeconds;

    /// <summary>
    /// Plays a gust on the active presenter, if there is one. Returns false when no presenter is
    /// in the scene or no prefab is wired, so the caller can say so rather than arming a HUD
    /// change with nothing to attribute it to.
    /// </summary>
    public static bool PlayGustFrom(Vector3 worldOrigin)
        => _active != null && _active.Play(worldOrigin);

    /// <summary>True while a presenter able to play a gust is enabled in the scene.</summary>
    public static bool HasActivePresenter => _active != null && _active._gustVfxPrefab != null;

    /// <summary>
    /// Sweeps one gust from <paramref name="worldOrigin"/> to the clue panel. Returns false when
    /// no prefab is wired; the ability is presentational, so a missing reference must degrade to
    /// "no gust" rather than to an exception mid-defense.
    /// </summary>
    public bool Play(Vector3 worldOrigin)
    {
        if (_gustVfxPrefab == null)
            return false;

        GameObject instance = Instantiate(_gustVfxPrefab, worldOrigin, Quaternion.identity);
        instance.name = "[Runtime] AshGust";
        StartCoroutine(SweepGust(instance, worldOrigin, ResolveClueWorldPoint()));
        return true;
    }

    private void OnEnable()
    {
        _active = this;
    }

    private void OnDisable()
    {
        // Only clear the handle if it still points at us: a scene with two presenters (a HUD
        // prefab plus a level override) must not be left with a null handle when the loser of
        // that race is torn down.
        if (_active == this)
            _active = null;
    }

    /// <summary>
    /// The sweep itself. Unscaled time throughout, for the same reason the introduction cards run
    /// out of band: the level's teaching beats drop <c>Time.timeScale</c>, and a gust that
    /// stretched to four seconds because a card was on screen would stop reading as wind.
    /// </summary>
    private IEnumerator SweepGust(GameObject instance, Vector3 from, Vector3 to)
    {
        // Per sweep rather than a reused field: a second gust must not re-point the first one's
        // renderer list at its own instance and fade a VFX it does not own.
        var spriteRenderers = new List<SpriteRenderer>();
        instance.transform.GetComponentsInChildren(true, spriteRenderers);
        ApplyTint(instance, spriteRenderers);

        // The reused prefab animates sprite frames on demand rather than on enable.
        var spriteVfx = instance.GetComponent<SingleAttackHitSpriteVfx>();
        if (spriteVfx != null)
            spriteVfx.Play();

        // Perpendicular to the travel direction, so the bow is always across the sweep rather
        // than along an axis that happens to point at the panel.
        Vector3 travel = to - from;
        Vector3 arc = new Vector3(-travel.y, travel.x, 0f).normalized * _sweepArcHeight;

        float duration = Mathf.Max(0.05f, _gustDurationSeconds);
        float elapsed = 0f;
        while (elapsed < duration && instance != null)
        {
            float t = Mathf.Clamp01(elapsed / duration);

            // A quadratic bow: zero displacement at both ends, full arc height at the midpoint.
            float bow = 4f * t * (1f - t);
            instance.transform.position = Vector3.Lerp(from, to, t) + (arc * bow);

            float scale = Mathf.Lerp(_startScale, _endScale, t);
            instance.transform.localScale = new Vector3(scale, scale, 1f);

            ApplyFade(spriteRenderers, ResolveFadeAlpha(t));

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (instance != null)
            Destroy(instance);
    }

    /// <summary>
    /// Full opacity until the fade window opens, then a linear ramp to nothing. Written as a
    /// fraction measured from the end so re-tuning the duration does not also re-tune the fade.
    /// </summary>
    private float ResolveFadeAlpha(float progress)
    {
        if (_fadeOutFraction <= 0f)
            return 1f;

        float fadeStart = 1f - _fadeOutFraction;
        if (progress <= fadeStart)
            return 1f;

        return Mathf.Clamp01(1f - ((progress - fadeStart) / _fadeOutFraction));
    }

    /// <summary>
    /// Re-colours everything the prefab renders. Both renderer families are handled because the
    /// authored ash variant may be swapped from the current sprite burst to a particle emitter
    /// without touching this class.
    /// </summary>
    private void ApplyTint(GameObject instance, List<SpriteRenderer> sprites)
    {
        for (int i = 0; i < sprites.Count; i++)
        {
            sprites[i].color = _ashTint;
            sprites[i].sortingOrder = _gustSortingOrder;
        }

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            main.startColor = _ashTint;

            var particleRenderer = particleSystems[i].GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
                particleRenderer.sortingOrder = _gustSortingOrder;
        }
    }

    private void ApplyFade(List<SpriteRenderer> sprites, float alpha)
    {
        for (int i = 0; i < sprites.Count; i++)
        {
            SpriteRenderer renderer = sprites[i];
            if (renderer == null)
                continue;

            Color color = renderer.color;
            color.a = _ashTint.a * alpha;
            renderer.color = color;
        }
    }

    /// <summary>
    /// Where the gust is aimed. An authored anchor wins; otherwise the panel's screen position is
    /// projected back into the world, so a level that never wired an anchor still aims at the HUD
    /// instead of at the origin.
    /// </summary>
    private Vector3 ResolveClueWorldPoint()
    {
        if (_cluePanelAnchor != null)
            return _cluePanelAnchor.position;

        Camera camera = Camera.main;
        if (camera == null)
            return transform.position;

        Vector3 viewportPoint = new Vector3(
            _cluePanelViewportPoint.x, _cluePanelViewportPoint.y, _cluePanelViewportDepth);
        Vector3 world = camera.ViewportToWorldPoint(viewportPoint);
        world.z = 0f;
        return world;
    }
}
