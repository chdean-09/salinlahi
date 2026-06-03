using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual effect applied to a tutorial page's art, mimicking actual boss battle states.
/// Designers pick the effect per page; <see cref="BossTutorialScroll"/> applies it to the UI Image.
/// </summary>
public enum BossTutorialArtEffect
{
    [Tooltip("No special effect — static or frame-animated art only.")]
    None,

    [Tooltip("Sinusoidal Y-bob + red tint. Mimics the boss's WindingDown / exhausted panting state.")]
    Panting,

    [Tooltip("Y-scale squash + downward offset + half-amplitude bob + red tint. Mimics the boss's Vulnerable collapsed state.")]
    Collapsed,
}

[System.Serializable]
public struct BossTutorialPage
{
    [Tooltip("Page heading. Page 1 is typically the boss name; later pages, the mechanic name.")]
    public string title;

    [TextArea(2, 6)]
    [Tooltip("Lore (page 1) or mechanic explanation (later pages).")]
    public string body;

    [Tooltip("Sprite frames for this page's art. Single frame = static; multiple frames = animated at animationFps. Empty hides the art frame.")]
    public Sprite[] frames;

    [Tooltip("Frames per second for the art animation. 0 or negative = static (shows frames[0] only).")]
    public float animationFps;

    [Tooltip("Visual effect applied to the art, mimicking actual boss battle state visuals.")]
    public BossTutorialArtEffect effect;

    /// <summary>Returns true when the page has at least one non-null frame to show.</summary>
    public bool HasArt
    {
        get
        {
            if (frames == null || frames.Length == 0) return false;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] != null) return true;
            return false;
        }
    }
}

[CreateAssetMenu(fileName = "BossTutorial", menuName = "Salinlahi/Boss Tutorial")]
public class BossTutorialSO : ScriptableObject
{
    [Tooltip("Ordered pages. Page 1 = boss name + lore; later pages = mechanics.")]
    public List<BossTutorialPage> pages = new();

    public int PageCount => pages != null ? pages.Count : 0;
    public bool HasPages => PageCount > 0;
}
