using UnityEngine;

[System.Obsolete("GameOverUI is deprecated. Use DefeatScreenUI in Gameplay scene.")]
public sealed class GameOverUI : MonoBehaviour
{
    private void Awake()
    {
        // Legacy placeholder retained for backward compatibility with older scene references.
        gameObject.SetActive(false);
    }
}
