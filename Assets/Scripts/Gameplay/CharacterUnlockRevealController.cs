using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the level-start "New Character Unlocked!" reveal: shows each character one at a time in a
/// reused <see cref="AlmanacDetailScroll"/>, and on each ✕ registers the unlock
/// (CharacterUnlockProgress + EventBus.RaiseCharacterUnlocked) before advancing. Drawing input is
/// suppressed while any scroll is open. Lives in the Gameplay scene; LevelFlowController drives it
/// via <see cref="Play"/>. The queue/filter logic is the pure, testable <see cref="BuildRevealQueue"/>.
/// </summary>
public class CharacterUnlockRevealController : MonoBehaviour
{
    [Tooltip("The reused 'New Character Unlocked!' scroll overlay in the Gameplay scene.")]
    [SerializeField] private AlmanacDetailScroll _scroll;

    /// <summary>
    /// Returns the characters in <paramref name="allowed"/>, in order, that are not yet unlocked
    /// (per <paramref name="isUnlocked"/>), skipping nulls. Null args yield an empty list.
    /// </summary>
    public static List<BaybayinCharacterSO> BuildRevealQueue(
        IReadOnlyList<BaybayinCharacterSO> allowed, Func<BaybayinCharacterSO, bool> isUnlocked)
    {
        var queue = new List<BaybayinCharacterSO>();
        if (allowed == null || isUnlocked == null) return queue;

        foreach (BaybayinCharacterSO c in allowed)
        {
            if (c == null) continue;
            if (isUnlocked(c)) continue;
            queue.Add(c);
        }
        return queue;
    }

    /// <summary>
    /// Shows each character in <paramref name="toReveal"/> one at a time, registering the unlock on
    /// each dismissal. No-op (yields immediately) when the scroll is unwired or the list is empty.
    /// Suppresses drawing input for the whole sequence and always releases it.
    /// </summary>
    public IEnumerator Play(IReadOnlyList<BaybayinCharacterSO> toReveal)
    {
        if (_scroll == null || toReveal == null || toReveal.Count == 0)
            yield break;

        bool dismissed = false;
        void OnHidden() => dismissed = true;

        GameManager.Instance?.SuppressDrawingInput(true);
        _scroll.OnHidden += OnHidden;
        try
        {
            foreach (BaybayinCharacterSO c in toReveal)
            {
                if (c == null) continue;

                dismissed = false;
                Sprite glyph = c.almanacSprite != null ? c.almanacSprite : c.displaySprite;
                _scroll.Show(glyph, $"\"{c.characterID}\"", c.description);

                // Wait for the player to press ✕ (Hide raises OnHidden immediately).
                yield return new WaitUntil(() => dismissed);

                // Acknowledged → persist the unlock and let any Almanac listener refresh.
                if (CharacterUnlockProgress.TryMarkUnlocked(c, out _))
                    EventBus.RaiseCharacterUnlocked(c);

                // Let the close animation finish before the next Show, so the scroll visibly closes
                // (the scroll deactivates its GameObject at the end of its close animation).
                yield return new WaitUntil(() => _scroll == null || !_scroll.gameObject.activeSelf);
            }
        }
        finally
        {
            if (_scroll != null) _scroll.OnHidden -= OnHidden;
            GameManager.Instance?.SuppressDrawingInput(false);
        }
    }
}
