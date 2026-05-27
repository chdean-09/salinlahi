using UnityEngine;

public enum CutsceneTriggerType { BeforeLevel, AfterLevel }

[System.Serializable]
public struct LevelCutsceneEntry
{
    [Tooltip("Matches ProgressManager level number (1-15).")]
    public int levelNumber;
    public CutsceneSO cutscene;
    public CutsceneTriggerType triggerType;
}

[CreateAssetMenu(fileName = "LevelCutsceneMapping", menuName = "Salinlahi/Level Cutscene Mapping")]
public class LevelCutsceneMappingSO : ScriptableObject
{
    public LevelCutsceneEntry[] entries;
}
