using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One grid cell in the Almanac. Mirrors LevelButton's Setup pattern. Revealed cells show the
/// thumbnail in full colour behind a glow frame and are tappable; locked cells show the same
/// thumbnail flattened to a silhouette behind a dim frame and are non-interactable, so an
/// undiscovered enemy reads as a shape to be identified rather than a blank. The '?' is the
/// fallback for a locked entry with no art at all. Boss cells gain a red-glow border only once
/// revealed — a locked boss is just another silhouette, preserving the reveal.
/// </summary>
public class AlmanacCell : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _thumbnail;

    [Header("State Visuals")]
    [Tooltip("Glow frame shown when revealed.")]
    [SerializeField] private GameObject _glowFrame;
    [Tooltip("Dim frame shown when locked.")]
    [SerializeField] private GameObject _lockedFrame;
    [Tooltip("'?' shown only when a locked cell has no art to silhouette.")]
    [SerializeField] private GameObject _questionMark;
    [Tooltip("Red-glow border; shown only when isBoss && isRevealed.")]
    [SerializeField] private GameObject _bossBorder;
    [Tooltip("Tint applied to a locked cell's thumbnail to flatten it into a silhouette.")]
    [SerializeField] private Color _silhouetteTint = new Color(0.10f, 0.08f, 0.07f, 1f);

    private Action _onSelect;

    /// <summary>Configures this cell. Safe to call repeatedly — the click listener is deduplicated.</summary>
    public void Setup(Sprite thumbnail, bool isRevealed, bool isBoss, Action onSelect)
    {
        _onSelect = onSelect;
        bool silhouetted = ShouldShowSilhouette(isRevealed, thumbnail);

        if (_thumbnail != null)
        {
            _thumbnail.sprite = thumbnail;
            _thumbnail.enabled = thumbnail != null;
            _thumbnail.color = isRevealed ? Color.white : _silhouetteTint;
        }
        if (_glowFrame != null) _glowFrame.SetActive(isRevealed);
        if (_lockedFrame != null) _lockedFrame.SetActive(!isRevealed);
        if (_questionMark != null) _questionMark.SetActive(!isRevealed && !silhouetted);
        if (_bossBorder != null) _bossBorder.SetActive(ShouldShowBossBorder(isBoss, isRevealed));

        if (_button != null)
        {
            _button.interactable = ShouldBeInteractable(isRevealed);
            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
        }
    }

    // Pure decisions (EditMode-tested).
    public static bool ShouldShowBossBorder(bool isBoss, bool isRevealed) => isBoss && isRevealed;
    public static bool ShouldBeInteractable(bool isRevealed) => isRevealed;

    /// <summary>A locked cell silhouettes its art; with no art there is nothing to silhouette, so '?' stands in.</summary>
    public static bool ShouldShowSilhouette(bool isRevealed, Sprite thumbnail) => !isRevealed && thumbnail != null;

    private void HandleClick()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        _onSelect?.Invoke();
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(HandleClick);
    }
}
