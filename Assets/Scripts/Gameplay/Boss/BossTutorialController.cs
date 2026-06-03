using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the upfront boss tutorial: shows the BossTutorialScroll and waits for the player
/// to close it with the red X. LevelFlowController yields on Play() after the character-unlock
/// reveal and before the boss encounter. Mirrors CharacterUnlockRevealController's shape:
/// scene-wired, suppresses drawing input while open, no-ops gracefully when unconfigured.
/// </summary>
public class BossTutorialController : MonoBehaviour
{
    [Tooltip("The paged boss tutorial scroll overlay in the Gameplay scene.")]
    [SerializeField] private BossTutorialScroll _scroll;

    public IEnumerator Play(BossTutorialSO tutorial)
    {
        if (tutorial == null)
            yield break; // No tutorial assigned — silent no-op.

        if (!tutorial.HasPages)
        {
            DebugLogger.LogWarning("BossTutorialController: BossTutorialSO has no pages — skipping boss tutorial.");
            yield break;
        }

        if (_scroll == null)
        {
            DebugLogger.LogWarning("BossTutorialController: No BossTutorialScroll wired — skipping boss tutorial.");
            yield break;
        }

        bool closed = false;
        void OnClosed() => closed = true;

        GameManager.Instance?.SuppressDrawingInput(true);
        _scroll.OnClosed += OnClosed;
        try
        {
            _scroll.Show(tutorial.pages);
            yield return new WaitUntil(() => closed);
            // Let the close animation finish (scroll deactivates itself at the end).
            yield return new WaitUntil(() => _scroll == null || !_scroll.gameObject.activeSelf);
        }
        finally
        {
            if (_scroll != null) _scroll.OnClosed -= OnClosed;
            GameManager.Instance?.SuppressDrawingInput(false);
        }
    }
}
