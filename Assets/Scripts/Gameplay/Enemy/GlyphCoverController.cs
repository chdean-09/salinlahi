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

    public bool IsCovered => _covered;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        _initialized = false;
        _covered = false;
    }

    private void OnDisable()
    {
        SetCovered(false);
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
        if (data == null || !data.coversOwnGlyph || _enemy.IsDying)
        {
            if (_covered)
                SetCovered(false);
            return;
        }

        if (!_initialized)
        {
            _initialized = true;
            _phaseTimer = Mathf.Max(0f, data.glyphCoverInitialRevealSeconds);
            SetCovered(false);
        }

        _phaseTimer -= Mathf.Max(0f, deltaTime);
        if (_phaseTimer > 0f)
            return;

        SetCovered(!_covered);
        _phaseTimer = Mathf.Max(MinPhaseSeconds,
            _covered ? data.glyphCoverHiddenSeconds : data.glyphCoverRevealSeconds);
    }

    private void SetCovered(bool covered)
    {
        _covered = covered;
        if (_enemy != null && _enemy.GlyphBadge != null)
            _enemy.GlyphBadge.SetCovered(covered);
    }
}
