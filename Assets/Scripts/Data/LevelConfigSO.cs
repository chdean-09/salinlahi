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

    [Tooltip("Visual theme for this level's era (background, decorations, etc.)")]
    public EraThemeSO eraTheme;

    [Tooltip("Baked-in numbered scroll sprite displayed on this level's Level Select button.")]
    public Sprite numberSprite;

    [Header("Waves")]
    [Tooltip("Waves played in order from index 0")]
    public List<WaveConfigSO> waves;

    [Tooltip("TEMP during migration: embedded waves. Renamed to 'waves' in finalize step.")]
    public List<WaveDefinition> embeddedWaves = new();

    [Header("Characters")]
    [Tooltip("Master list of characters allowed in this level. WaveConfigs draw from this.")]
    public List<BaybayinCharacterSO> allowedCharacters;

    [Tooltip("Master list of enemy types allowed in this level. Waves draw from this.")]
    public List<EnemyDataSO> allowedEnemyTypes = new();

    [Header("Boss")]
    [Tooltip("If set, this level is a boss encounter. Waves list is ignored.")]
    public BossConfigSO bossConfig;

    [Header("Build Flags")]
    public bool isAvailableInLite = true;

    [Header("Flow")]
    [Tooltip("Optional tutorial phase played before this level's waves. Level 1 uses this for embedded onboarding.")]
    public Level1TutorialSequenceSO tutorialSequence;

    [Tooltip("Dialogue played before waves begin. Null = skip intro.")]
    public DialogueSO introDialogue;

    [Tooltip("Dialogue played after level complete (before victory screen). Null = skip outro.")]
    public DialogueSO outroDialogue;

    [Tooltip("Background music for this level. Null = no BGM change.")]
    public AudioClip bgmClip;

    [Header("Protagonist")]
    [Tooltip("If true, spawns a protagonist during this level.")]
    public bool hasProtagonist = false;

    [Tooltip("If true, protagonist walks in from below. If false, appears instantly at final position.")]
    public bool protagonistWalksIn = false;

    public void ReconcileWavesToRoster()
    {
        if (embeddedWaves == null)
            return;

        for (int i = 0; i < embeddedWaves.Count; i++)
        {
            WaveDefinition wave = embeddedWaves[i];
            if (wave == null)
                continue;

            PruneToRoster(wave.characters, allowedCharacters);
            PruneToRoster(wave.enemyTypes, allowedEnemyTypes);
        }
    }

    private static void PruneToRoster<T>(List<T> subset, List<T> roster) where T : Object
    {
        if (subset == null)
            return;

        subset.RemoveAll(item => item == null || roster == null || !roster.Contains(item));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ReconcileWavesToRoster();
    }
#endif
}
