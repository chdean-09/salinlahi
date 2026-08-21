using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "LevelConfig", menuName = "Salinlahi/Level Config")]
public class LevelConfigSO : ScriptableObject
{
    [Header("Identity")]
    public string levelName;
    public int levelNumber;
    public int chapterNumber = 1;
    public string chapterName = "Chapter 1";

    [Header("Revised Campaign Identity")]
    public string stableId;
    [Range(1, 5)] public int eraLocalOrder = 1;

    [Header("Revised Campaign Content")]
    public List<FocusWordDefinition> focusWords = new();
    public List<SymbolValueReference> cumulativeSymbolPool = new();
    public List<ContentRequirement> learningRequirements = new();
    public List<ContentRequirement> practiceRequirements = new();
    [Header("Active-Clue Combat")]
    [Tooltip("Arms active-clue combat for this level. Default false so existing levels keep legacy combat.")]
    public bool activeClueCombatEnabled;

    [Tooltip("Presentation channels used to cue the active clue.")]
    public ClueChannels clueChannels = ClueChannels.Glyph;

    [Tooltip("Visual channel added automatically when clueChannels is audio-only.")]
    public ClueChannels audioVisualFallback = ClueChannels.LatinText;

    public DefenseRules defenseRules = new();
    public ContentMediaReferences contextMedia = new();
    public SymbolValueReference finalRestorationValue = new();
    public List<string> rewardIds = new();
    public List<ContentRequirement> masteryRequirements = new();

    [Tooltip("Visual theme for this level's era (background, decorations, etc.)")]
    public EraThemeSO eraTheme;

    [Tooltip("Baked-in numbered scroll sprite displayed on this level's Level Select button.")]
    public Sprite numberSprite;

    [Header("Waves")]
    [FormerlySerializedAs("embeddedWaves")]
    [Tooltip("Waves played in order from index 0.")]
    public List<WaveDefinition> waves = new();

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

    [Header("Advanced Combat")]
    [Tooltip("If false, combo streaks may be tracked internally but cannot activate Focus Mode in this level.")]
    public bool focusModeEnabled = true;

    [Tooltip("If false, matching multiple enemies resolves as a normal closest-target kill instead of a multi-kill chain.")]
    public bool multiKillChainEnabled = true;

    [Header("Flow")]
    [Tooltip("Optional legacy tutorial phase played before this level's waves.")]
    public Level1TutorialSequenceSO tutorialSequence;

    [Tooltip("Optional onboarding sequence played before waves. Level 1 uses basic onboarding; Level 2 uses advanced combat onboarding.")]
    public OnboardingSequenceSO onboardingSequence;

    [Tooltip("Enables the generalized challenge sequence for this level. Legacy onboarding remains the fallback when disabled.")]
    public bool challengePrototypeEnabled;

    [Tooltip("Optional generalized challenge sequence. It is used only when challengePrototypeEnabled is true.")]
    public ChallengeSequenceSO challengeSequence;

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
        if (waves == null)
            return;

        for (int i = 0; i < waves.Count; i++)
        {
            WaveDefinition wave = waves[i];
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
