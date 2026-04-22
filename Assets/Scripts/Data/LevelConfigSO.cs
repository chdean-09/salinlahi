using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelConfig", menuName = "Salinlahi/Level Config")]
public class LevelConfigSO : ScriptableObject
{
    [Header("Identity")]
    public string levelName;
    public int levelNumber;
    public int chapterNumber = 1;
    public string chapterName = "Chapter 1";

    [Header("Waves")]
    [Tooltip("Waves played in order from index 0")]
    public List<WaveConfigSO> waves;

    [Header("Characters")]
    [Tooltip("Master list of characters allowed in this level. WaveConfigs draw from this.")]
    public List<BaybayinCharacterSO> allowedCharacters;

    [Header("Boss")]
    [Tooltip("True if this level is a boss encounter")]
    public bool isBossLevel;

    [Tooltip("Boss configuration (only used if isBossLevel is true)")]
    public BossConfigSO bossConfig;

    [Header("Build Flags")]
    public bool isAvailableInLite = true;
}
