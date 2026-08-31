using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SALIN-163 AC2. Makes the trace-hint offer act on: tapping it shows a ghost of the character
/// the player is currently expected to draw.
///
/// This component lives on the hint prompt itself rather than on <see cref="DrawingFeedback"/>,
/// so it needs no cooperation from the feedback logic: DrawingFeedback already activates and
/// deactivates the prompt when the help threshold is crossed, which drives OnEnable/OnDisable
/// here. Keeping the two apart means the help *policy* (when to offer) stays in one place and the
/// help *affordance* (what tapping does) stays in another.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class TraceHintPresenter : MonoBehaviour
{
    [Tooltip("Translucent overlay that shows the expected glyph. Safe to leave unwired.")]
    [SerializeField] private Image _ghostImage;

    [Tooltip("How long the ghost stays on screen after a tap.")]
    [SerializeField, Min(0.1f)] private float _ghostSeconds = 2.5f;

    [SerializeField, Range(0f, 1f)] private float _ghostAlpha = 0.35f;

    private Button _button;
    private Coroutine _hideRoutine;

    /// <summary>The character the ghost last displayed. Held for the same reason DrawingFeedback
    /// holds LastMessage: the overlay is an optional scene reference, so this is the assertable
    /// record of what the player was actually shown.</summary>
    public BaybayinCharacterSO LastShown { get; private set; }

    /// <summary>True when a tap would have something to show. Drives the button's interactable
    /// state so the offer is never live when it cannot be honoured.</summary>
    public bool CanShowHint => ResolveGlyphSprite(ResolveExpectedCharacter()) != null;

    private void Awake()
    {
        _button = GetComponent<Button>();
        HideGhost();
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(ShowHint);

        // Offering a hint that resolves to nothing would be worse than not offering one, so the
        // button is only live when a character can actually be resolved. On legacy levels, which
        // never arm clue combat, there is no single expected character and this stays false.
        _button.interactable = CanShowHint;
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(ShowHint);
        HideGhost();
    }

    public void ShowHint()
    {
        BaybayinCharacterSO character = ResolveExpectedCharacter();
        Sprite glyph = ResolveGlyphSprite(character);
        if (character == null || glyph == null)
        {
            // Not an error worth logging every tap: a clue can resolve between the offer and the
            // tap. Failing quietly and leaving the ghost hidden is the correct outcome.
            HideGhost();
            return;
        }

        LastShown = character;

        if (_ghostImage != null)
        {
            _ghostImage.sprite = glyph;
            _ghostImage.color = new Color(1f, 1f, 1f, _ghostAlpha);
            _ghostImage.enabled = true;
        }

        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
        if (isActiveAndEnabled) _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        // Unscaled: the hint must still time out while the game is paused or slowed, otherwise a
        // ghost shown during a slow-motion combo would linger far longer than intended.
        yield return new WaitForSecondsRealtime(_ghostSeconds);
        HideGhost();
        _hideRoutine = null;
    }

    private void HideGhost()
    {
        if (_ghostImage != null) _ghostImage.enabled = false;
    }

    /// <summary>
    /// The expected character comes from the active clue. Clue combat is the only mode where a
    /// single "this one, now" answer exists; legacy levels have every on-screen enemy as a valid
    /// target, so there is nothing unambiguous to trace and this returns null.
    /// </summary>
    private static BaybayinCharacterSO ResolveExpectedCharacter()
    {
        ActiveClueDirector director = ActiveClueDirector.Instance;
        return director != null && director.CurrentClue != null
            ? director.CurrentClue.Character
            : null;
    }

    /// <summary>
    /// Prefers <c>badgeSprite</c>, the bare framed glyph.
    ///
    /// Deliberately NOT <c>displaySprite</c>, despite its tooltip calling itself the bare glyph:
    /// in the shipped data it points at the `Resources/[ID].png` learning card, which has the
    /// romanised syllable printed on it (`BA-VA.png` reads "ba, va"). Showing that as a drawing
    /// hint would hand the player the answer in Latin script, which is a larger giveaway than the
    /// recognizer score this whole ticket removed. Falls back only if no badge art exists.
    /// </summary>
    private static Sprite ResolveGlyphSprite(BaybayinCharacterSO character)
    {
        if (character == null) return null;
        return character.badgeSprite != null ? character.badgeSprite : character.almanacSprite;
    }
}
