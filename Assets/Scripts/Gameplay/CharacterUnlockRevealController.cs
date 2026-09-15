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
///
/// The interstitial itself is switched OFF by default at the call site (LevelFlowController's
/// "Play Character Unlock Reveal"). That switch governs the PRESENTATION only; the unlock DATA
/// still has to be written, so the data half of <see cref="Play"/> is also available on its own as
/// <see cref="RegisterUnlocksWithoutReveal"/>. Nothing here is deleted, so the reveal can be
/// authored back on without restoring code.
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
    /// Registers every character in <paramref name="toReveal"/> as unlocked without showing the
    /// scroll, and returns how many were newly marked. This is the unlock DATA half of
    /// <see cref="Play"/> standing on its own, for when the reveal presentation is switched off:
    /// unlocked characters must still be unlocked and must still appear in the Almanac, which reads
    /// CharacterUnlockProgress. <see cref="Play"/> is the only production writer of that progress,
    /// so skipping the interstitial without calling this would leave the Almanac permanently empty.
    ///
    /// Deliberately does NOT raise EventBus.OnCharacterUnlocked. That event accompanies the reveal:
    /// AudioManager plays the unlock reward sting on it, and with the scroll suppressed a whole
    /// level's characters register inside one frame — a stack of reward stings with nothing on
    /// screen to explain them. The event's only other listener rebuilds an Almanac grid that is
    /// already open, which cannot happen while the Gameplay scene is running the pre-wave beats,
    /// and that grid rebuilds from CharacterUnlockProgress.HasUnlocked when the player opens it.
    /// </summary>
    public static int RegisterUnlocksWithoutReveal(IReadOnlyList<BaybayinCharacterSO> toReveal)
    {
        if (toReveal == null) return 0;

        int marked = 0;
        foreach (BaybayinCharacterSO c in toReveal)
        {
            if (c == null) continue;
            if (CharacterUnlockProgress.TryMarkUnlocked(c, out _)) marked++;
        }
        return marked;
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
