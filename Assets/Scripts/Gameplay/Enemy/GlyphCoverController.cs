using UnityEngine;

/// <summary>
/// Takip's signature ability: "It hides correct answers and covers important objects."
/// The enemy's own glyph badge stays covered and is revealed only for short windows, so the
/// player has to remember the glyph they glimpsed (or recall it from the discovery panel).
/// Data-driven through <see cref="EnemyDataSO.coversOwnGlyph"/>; Enemy.Initialize attaches this
/// component on the shared corruption shell and toggles it per spawn.
/// </summary>
[RequireComponent(typeof(Enemy))]
public sealed class GlyphCoverController : MonoBehaviour
{
    private const float MinPhaseSeconds = 0.05f;

    private Enemy _enemy;
    private bool _covered;
    private bool _initialized;
    private float _phaseTimer;

    /// <summary>
    /// True while this spawn is the one that introduced its type, in which case the cover must do
    /// nothing at all. See <see cref="SetSuppressedForIntroductionSpawn"/>.
    /// </summary>
    private bool _suppressedForIntroductionSpawn;

    public bool IsCovered => _covered;

    /// <summary>True while this spawn is suppressed as its type's introduction spawn. Test/diagnostic seam, mirroring <see cref="AshFirstSlotController.IsSuppressedForIntroductionSpawn"/>.</summary>
    public bool IsSuppressedForIntroductionSpawn => _suppressedForIntroductionSpawn;

    /// <summary>
    /// Makes this ability inert for one spawn — the spawn on which the enemy's introduction card
    /// plays — and arms it again on every later spawn of the type.
    ///
    /// <para>
    /// <b>Why the cover must not fire on the spawn that introduces it.</b> Takip's badge blinks out
    /// on a cycle. A player who has never been told that happens, and whose only glimpse of the
    /// badge is the one the card is framing, reads a glyph that vanishes as the badge failing to
    /// render — and the card's own portrait is naming an enemy whose symbol the player cannot read.
    /// The card states the hiding first; the next Takip performs it, against a badge the player has
    /// already read cleanly.
    /// </para>
    ///
    /// <para>
    /// Suppression withdraws the effect as well as preventing it: a badge already covered is
    /// uncovered immediately rather than waiting out a hidden window that will now never end, and
    /// the cycle is de-initialised so lifting suppression restarts from the authored initial reveal
    /// rather than resuming a phase timer the player never saw begin.
    /// </para>
    ///
    /// <para>
    /// <b>Pooling.</b> Suppression is per spawn, never per shell. <c>Enemy.Initialize</c> restates it
    /// on every spawn and <see cref="OnEnable"/> clears it, so a recycled shell always comes back
    /// unsuppressed — a stuck flag here would silently disable Takip's cover for the rest of the
    /// run, on a shell that looks identical to a working one.
    /// </para>
    /// </summary>
    public void SetSuppressedForIntroductionSpawn(bool suppressed)
    {
        if (_suppressedForIntroductionSpawn == suppressed)
            return;

        _suppressedForIntroductionSpawn = suppressed;
        if (suppressed)
        {
            SetCovered(false, animateRemoval: false);
            _initialized = false;
        }
    }

    /// <summary>Clear the previous pooled occupant's phase and establish this spawn's suppression.</summary>
    public void ResetForSpawn(bool suppressed)
    {
        SetCovered(false, animateRemoval: false);
        _initialized = false;
        _phaseTimer = 0f;
        _suppressedForIntroductionSpawn = suppressed;
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        _initialized = false;
        _covered = false;
        // A pooled shell must not inherit the previous occupant's suppression.
        _suppressedForIntroductionSpawn = false;
    }

    private void OnDisable()
    {
        SetCovered(false, animateRemoval: false);
        _initialized = false;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            return;

        Tick(Time.deltaTime);
    }

    /// <summary>Advances the cover cycle. Public so the cycle can be driven without frames in tests.</summary>
    public void Tick(float deltaTime)
    {
        if (_enemy == null)
            _enemy = GetComponent<Enemy>();

        EnemyDataSO data = _enemy != null ? _enemy.Data : null;
        // The introduction-spawn suppression joins the same gate as the data flag rather than
        // getting its own early return, so both ways of being inert uncover the badge through one
        // path and a suppressed Takip can never be left holding its glyph hidden.
        if (_suppressedForIntroductionSpawn || data == null || !data.coversOwnGlyph || _enemy.IsDying)
        {
            if (_covered)
                SetCovered(false, animateRemoval: false);
            return;
        }

        if (!_initialized)
        {
            _initialized = true;
            _phaseTimer = Mathf.Max(0f, data.glyphCoverInitialRevealSeconds);
            SetCovered(false);
        }

        // The configured hidden/reveal durations describe time spent in the settled state, not
        // the cover's closing/opening one-shot. Keep the phase timer paused through either motion
        // so the player still gets the full authored covered and readable windows.
        EnemyAbilityVisualPresenter presenter = _enemy.AbilityVisuals;
        bool transitionPlaying = presenter != null
            && (_covered
                ? presenter.IsActivationPlaying(EnemyAbilityVisualId.GlyphCover)
                : presenter.IsExitPlaying(EnemyAbilityVisualId.GlyphCover));
        if (transitionPlaying)
        {
            return;
        }

        _phaseTimer -= Mathf.Max(0f, deltaTime);
        if (_phaseTimer > 0f)
            return;

        SetCovered(!_covered);
        _phaseTimer = Mathf.Max(MinPhaseSeconds,
            _covered ? data.glyphCoverHiddenSeconds : data.glyphCoverRevealSeconds);
    }

    private void SetCovered(bool covered, bool animateRemoval = true)
    {
        bool wasCovered = _covered;
        _covered = covered;

        EnemyAbilityVisualPresenter presenter = _enemy != null ? _enemy.AbilityVisuals : null;
        bool hasCoverVisual = presenter != null && presenter.HasVisual(EnemyAbilityVisualId.GlyphCover);
        if (_enemy != null && _enemy.GlyphBadge != null)
            _enemy.GlyphBadge.SetCovered(hasCoverVisual ? false : covered);

        if (!hasCoverVisual)
            return;

        if (covered)
            presenter.SetActive(EnemyAbilityVisualId.GlyphCover, true);
        else if (wasCovered && animateRemoval)
            presenter.PlayExit(EnemyAbilityVisualId.GlyphCover);
        else
            presenter.SetActive(EnemyAbilityVisualId.GlyphCover, false);
    }
}
