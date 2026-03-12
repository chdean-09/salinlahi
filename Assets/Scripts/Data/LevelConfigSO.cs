using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelConfig", menuName = "Salinlahi/Level Config")]
public class LevelConfigSO : ScriptableObject
{
    [Header("Identity")]
    public string levelName;
    public int levelNumber;

    [Header("Waves")]
    [Tooltip("Waves played in order from index 0")]
    public List<WaveConfigSO> waves;

    [Header("Characters")]
    [Tooltip("Master list of characters allowed in this level. WaveConfigs draw from this.")]
    public List<BaybayanCharacterSO> allowedCharacters;

    [Header("Build Flags")]
    public bool isAvailableInLite = true;
}
