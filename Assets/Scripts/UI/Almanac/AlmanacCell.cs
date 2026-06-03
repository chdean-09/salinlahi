using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One grid cell in the Almanac (character or enemy). Mirrors LevelButton's Setup pattern.
/// Revealed cells show the thumbnail + glow frame and are tappable; locked cells show a dim
/// frame + '?' and are non-interactable. Boss cells gain a red-glow border only once revealed —
/// a locked boss reads as a plain '?', preserving the reveal.
/// </summary>
public class AlmanacCell : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _thumbnail;

    [Header("State Visuals")]
    [Tooltip("Glow frame shown when revealed.")]
    [SerializeField] private GameObject _glowFrame;
    [Tooltip("Dim frame + '?' shown when locked.")]
    [SerializeField] private GameObject _lockedFrame;
    [Tooltip("Red-glow border; shown only when isBoss && isRevealed.")]
    [SerializeField] private GameObject _bossBorder;

    private Action _onSelect;

    /// <summary>Configures this cell. Safe to call repeatedly — the click listener is deduplicated.</summary>
    public void Setup(Sprite thumbnail, bool isRevealed, bool isBoss, Action onSelect)
    {
        _onSelect = onSelect;

        if (_thumbnail != null)
        {
            _thumbnail.sprite = thumbnail;
            _thumbnail.enabled = isRevealed && thumbnail != null;
        }
        if (_glowFrame != null) _glowFrame.SetActive(isRevealed);
        if (_lockedFrame != null) _lockedFrame.SetActive(!isRevealed);
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
