using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-era visual + content bundle for the Level Select screen.
/// Holds the background sprite, baked-in banner sprite, era display name,
/// and the ordered list of LevelConfigSO entries shown in this era.
/// </summary>
[CreateAssetMenu(fileName = "EraConfig", menuName = "Salinlahi/Era Config")]
public class EraConfigSO : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Human-readable campaign name for logs/debugging, e.g. \"Ugat\".")]
    public string eraName;

    [Header("Revised Campaign Identity")]
    public string stableId;
    [Range(1, 3)] public int order = 1;
    public DialogueSO storyReference;
    public CutsceneSO memoryReference;

    [Header("Visuals")]
    [Tooltip("Full-screen background sprite shown when this era is selected.")]
    public Sprite backgroundSprite;

    [Tooltip("Baked-in scroll banner sprite for this campaign.")]
    public Sprite bannerSprite;

    [Header("Levels")]
    [Tooltip("Ordered list of levels in this era. Expected length matches the Level Select's slots-per-era (5).")]
    public List<LevelConfigSO> levels = new();
}
