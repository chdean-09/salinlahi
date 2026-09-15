using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The screen surface for the enemy introduction beat: the card that slides in beside a newly met
/// enemy type (walk sprite, display name, optional subtitle, ability line) and the one-line banner
/// that persists for that enemy's lifetime after the card leaves.
///
/// <para>
/// <b>This view owns pixels; <see cref="EnemyIntroductionBeat"/> owns time.</b> Every animated state
/// is driven from outside through a normalized 0..1 progress value rather than a coroutine in here.
/// The reason is that the beat runs while <c>Time.timeScale</c> is dropped to a per-level value and
/// its four step durations are deliberately wall-clock, so any timing this view kept for itself
/// would either have to duplicate that unscaled-time decision or silently stretch by 6x. One clock,
/// in one place, and the durations stay the beat's serialized fields.
/// </para>
///
/// <para>
/// <b>Nothing here may take input.</b> The introduction beat keeps player input enabled — the card
/// is an overlay the player can draw straight through — so every graphic is forced to
/// <c>raycastTarget = false</c> on wake. A card that swallowed a touch would turn the level's most
/// input-sensitive moment into a dead zone, and it would do it only on the one spawn that can never
/// be retried.
/// </para>
///
/// <para>
/// The card and the banner are independent surfaces: the banner outlives the card by design (it
/// stays up for the introduced enemy's whole lifetime), so hiding the card must not touch it.
/// </para>
/// </summary>
public sealed class EnemyIntroductionCardView : MonoBehaviour
{
    [Header("Card")]
    [Tooltip("Fades and blocks the whole card. Required — with no card group the beat declines to run rather than halting the game behind an invisible card.")]
    [SerializeField] private CanvasGroup _cardGroup;

    [Tooltip("The card's own rect, slid in from _cardSlideOffset as it fades up. Optional: leave null for a fade with no motion.")]
    [SerializeField] private RectTransform _cardRect;

    [Tooltip("Anchored-position offset the card starts at and returns to, in canvas units. Negative X slides in from the left.")]
    [SerializeField] private Vector2 _cardSlideOffset = new Vector2(-160f, 0f);

    [Header("Card Content")]
    [Tooltip("Shows the enemy's first walk frame. Scaled by _portraitScaleMultiplier so the card reads as a portrait rather than a gameplay-sized sprite.")]
    [SerializeField] private Image _portrait;

    [Tooltip("Scale applied to the portrait rect. The design calls for the walk sprite at 2x so the player can study a silhouette they have only seen in motion.")]
    [SerializeField] private float _portraitScaleMultiplier = 2f;

    [Tooltip("EnemyDataSO.displayName.")]
    [SerializeField] private TMP_Text _nameText;

    [Tooltip("EnemyDataSO.discoverySubtitle. Its GameObject is hidden when the subtitle is blank, so an unauthored subtitle leaves no gap.")]
    [SerializeField] private TMP_Text _subtitleText;

    [Tooltip("EnemyDataSO.abilityLine. Hidden until the beat's ability step, so the name lands before the behaviour does.")]
    [SerializeField] private TMP_Text _abilityText;

    [Header("Lifetime Banner")]
    [Tooltip("Persists after the card leaves, for the introduced enemy's lifetime only. Optional: with no banner group the beat runs its four steps and skips the banner.")]
    [SerializeField] private CanvasGroup _bannerGroup;

    [Tooltip("The banner's single line of copy.")]
    [SerializeField] private TMP_Text _bannerText;

    /// <summary>
    /// True when this view has enough wiring to show a card at all. The beat checks this
    /// <b>before</b> claiming a type's one-shot introduction: a claim consumed against an unwired
    /// card would spend the type's only introduction on nothing, permanently, and would suppress its
    /// ability on that spawn for no visible reason.
    /// </summary>
    public bool CanPresent => _cardGroup != null;

    private Vector2 _cardRestAnchoredPosition;
    private bool _capturedRestPosition;

    private void Awake()
    {
        CaptureRestPosition();
        ApplyPortraitScale();
        DisableRaycastsOnEveryGraphic();
        HideCardImmediate();
        HideBanner();
    }

    /// <summary>
    /// Loads the card with this enemy type's content and leaves it fully out of view, ready for
    /// <see cref="SetCardProgress"/> to bring it in. The ability line is withheld here even when
    /// authored: steps 2 and 3 of the beat are separate reads, and a card that arrives with both
    /// lines already on it collapses them into one.
    /// </summary>
    public void PrepareCard(Sprite walkSprite, string displayName, string subtitle)
    {
        CaptureRestPosition();
        ApplyPortraitScale();

        if (_portrait != null)
        {
            _portrait.sprite = walkSprite;
            // A null walk sprite would otherwise draw as a white box over the enemy it is meant to
            // be showing, which is worse than an absent portrait.
            _portrait.enabled = walkSprite != null;
            _portrait.preserveAspect = true;
        }

        SetTextOrHide(_nameText, displayName);
        SetTextOrHide(_subtitleText, subtitle);
        SetTextOrHide(_abilityText, null);

        SetCardProgress(0f);
        if (_cardGroup != null)
            _cardGroup.gameObject.SetActive(true);
    }

    /// <summary>
    /// Card presence, 0 = fully out, 1 = fully in. Drives alpha and the slide together so the beat
    /// can run the same call for the step-1 slide-in and the step-4 slide-out by inverting t.
    /// </summary>
    public void SetCardProgress(float t)
    {
        float clamped = Mathf.Clamp01(t);

        if (_cardGroup != null)
        {
            _cardGroup.alpha = clamped;
            // Never blocks raycasts: input stays live through the card for the whole beat.
            _cardGroup.blocksRaycasts = false;
            _cardGroup.interactable = false;
        }

        if (_cardRect != null)
            _cardRect.anchoredPosition = Vector2.Lerp(
                _cardRestAnchoredPosition + _cardSlideOffset,
                _cardRestAnchoredPosition,
                clamped);
    }

    /// <summary>
    /// Reveals step 3's line. Blank copy hides the row instead of showing an empty one, which is
    /// what lets a type with no ability use the same beat without authored filler.
    /// </summary>
    public void ShowAbilityLine(string abilityLine)
    {
        SetTextOrHide(_abilityText, abilityLine);
    }

    /// <summary>Drops the card out of view at once, without touching the banner.</summary>
    public void HideCardImmediate()
    {
        SetCardProgress(0f);
        if (_cardGroup != null)
            _cardGroup.gameObject.SetActive(false);
    }

    /// <summary>
    /// Raises the persistent one-line banner. Blank copy leaves it hidden rather than showing an
    /// empty bar for the enemy's whole lifetime.
    /// </summary>
    public void ShowBanner(string text)
    {
        if (_bannerGroup == null)
            return;

        bool hasCopy = !string.IsNullOrWhiteSpace(text);
        if (_bannerText != null)
            _bannerText.text = hasCopy ? text : string.Empty;

        _bannerGroup.alpha = hasCopy ? 1f : 0f;
        _bannerGroup.blocksRaycasts = false;
        _bannerGroup.interactable = false;
        _bannerGroup.gameObject.SetActive(hasCopy);
    }

    /// <summary>Takes the banner down. Called when the introduced enemy leaves the field.</summary>
    public void HideBanner()
    {
        if (_bannerGroup == null)
            return;

        _bannerGroup.alpha = 0f;
        _bannerGroup.gameObject.SetActive(false);
    }

    /// <summary>
    /// Remembers where the card sits when fully in, so the slide can be expressed as an offset from
    /// the authored layout instead of absolute coordinates that would fight a responsive canvas.
    /// Captured once: re-reading it mid-slide would latch a partway position as the rest pose.
    /// </summary>
    private void CaptureRestPosition()
    {
        if (_capturedRestPosition || _cardRect == null)
            return;

        _cardRestAnchoredPosition = _cardRect.anchoredPosition;
        _capturedRestPosition = true;
    }

    private void ApplyPortraitScale()
    {
        if (_portrait == null)
            return;

        float scale = Mathf.Max(0.01f, _portraitScaleMultiplier);
        _portrait.rectTransform.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>
    /// The card and banner are pure read surfaces over a live play field. Clearing raycastTarget on
    /// every graphic under them — rather than trusting each one to have been authored that way — is
    /// what guarantees a drawing stroke started on top of the card still reaches the play field.
    /// </summary>
    private void DisableRaycastsOnEveryGraphic()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(includeInactive: true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }
    }

    private static void SetTextOrHide(TMP_Text label, string copy)
    {
        if (label == null)
            return;

        bool hasCopy = !string.IsNullOrWhiteSpace(copy);
        label.text = hasCopy ? copy : string.Empty;
        label.gameObject.SetActive(hasCopy);
    }
}
