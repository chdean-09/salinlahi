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
    public string chapterName = "Ugat";

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

    [Tooltip("Restores focus-word syllables as active clues are accepted during combat. "
        + "The restoration phase uses this shared path instead of a post-wave board.")]
    public bool activeClueRestorationEnabled;

    [Tooltip("Presentation channels used to cue the active clue.")]
    public ClueChannels clueChannels = ClueChannels.Glyph;

    [Tooltip("Visual channel added automatically when clueChannels is audio-only.")]
    public ClueChannels audioVisualFallback = ClueChannels.LatinText;

    [Tooltip("Schedules which syllable each spawning enemy carries, so the level's length and the "
        + "order the player meets its content are authored rather than left to a uniform random "
        + "draw. Applies only when activeClueCombatEnabled is true; other levels keep legacy "
        + "assignment. See docs/design/spawn-assignment-system.md.")]
    public SpawnAssignmentPolicy spawnAssignmentPolicy = new SpawnAssignmentPolicy();

    [Tooltip("Scales every enemy's walk speed on this level. 1 leaves the authored speed alone. "
             + "Lower it to give an early level more reaction time without slowing the same enemy "
             + "on the later levels it also appears in.")]
    [Min(0.1f)] public float enemySpeedMultiplier = 1f;

    [Header("Drawing Accuracy Override")]
    [Tooltip("If true, drawingAccuracyThresholdOverride replaces RecognitionConfigSO.minimumConfidence "
             + "for this level only. Follows the same shape as EnemyDataSO's badge overrides: an "
             + "explicit opt-in flag beside the value, so a level that never authors one keeps the "
             + "global default byte-identically rather than depending on a sentinel value.")]
    public bool overrideDrawingAccuracyThreshold;

    [Tooltip("Minimum recognizer score (0-1) this level accepts. Consulted only when "
             + "overrideDrawingAccuracyThreshold is true. Lower it to make an early teaching level "
             + "forgiving without loosening recognition for the whole campaign. Level 1 uses 0.45 "
             + "against the 0.60 global default.")]
    [Range(0f, 1f)] public float drawingAccuracyThresholdOverride = 0.45f;

    [Header("Tutorial")]
    [Tooltip("Defeats the one-shot \"seen\" gate on this level's onboarding tutorial, so the "
             + "sequence plays every time the level is entered rather than only the first time. "
             + "Level 1 authors this true: it teaches the core draw-to-defend loop, and returning "
             + "players were being dropped straight into a wave with no reminder.\n\n"
             + "Defaults FALSE deliberately, even though Level 1 is the reason the field exists. "
             + "Unity fills a newly added field from this initializer on every asset at once, so a "
             + "true default would silently switch all fifteen levels — including Level 2's "
             + "advanced tutorial, which nobody asked to replay. Opt-in keeps the change to the "
             + "one level that was asked for and leaves the other fourteen exactly as authored.\n\n"
             + "Completion is still recorded either way — the flag makes the gate ignore the seen "
             + "record, it does not stop the record being written, so anything asking whether the "
             + "player has ever finished this tutorial keeps its answer.")]
    public bool alwaysShowTutorial;

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
    [Tooltip("If false, matching multiple enemies resolves as a normal closest-target kill instead of a multi-kill chain.")]
    public bool multiKillChainEnabled = true;

    [Header("Flow")]
    [Tooltip("Optional legacy tutorial phase played before this level's waves.")]
    public Level1TutorialSequenceSO tutorialSequence;

    [Tooltip("Optional onboarding sequence played before waves. Level 1 uses basic onboarding; Level 2 uses advanced combat onboarding.")]
    public OnboardingSequenceSO onboardingSequence;

    [Tooltip("Enemy introduction lessons authored for this level. Level 1 carries one (Abo ng "
        + "Simula); every other level leaves this empty and uses the four-step introduction card.")]
    public EnemyLessonSO[] enemyLessons = System.Array.Empty<EnemyLessonSO>();

    [Tooltip("Enables the generalized challenge sequence for this level. Legacy onboarding remains the fallback when disabled.")]
    public bool challengePrototypeEnabled;

    [Tooltip("Optional generalized challenge sequence. It is used only when challengePrototypeEnabled is true.")]
    public ChallengeSequenceSO challengeSequence;
    [Tooltip("Difficulty tier overlay for the context challenge (SALIN-181). Tier 0 = legacy per-unit behavior.")]
    public ChallengeTierPolicy challengePolicy = new ChallengeTierPolicy();

    [Tooltip("Optional alternating defense/restoration segments (SALIN-226). Empty = one Defense "
        + "pass then one ContextChallenge pass, exactly as before. Each segment consumes the next "
        + "waveCount waves from the waves list above and then plays the named challenge units.")]
    public List<LevelFlowSegment> flowSegments = new();

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

    /// <summary>
    /// The recognizer score a drawing must reach on <paramref name="level"/>, given the campaign-wide
    /// <paramref name="globalThreshold"/> from <c>RecognitionConfigSO.minimumConfidence</c>.
    /// </summary>
    /// <remarks>
    /// Static and null-tolerant on purpose. The threshold is read on the recognition hot path, where
    /// there may be no level at all (Tracing Dojo, boss sandbox, a test that never called
    /// GameManager), and in every one of those cases the answer must be the global value unchanged.
    /// Making that the fallback here rather than at the call site is what keeps "no override authored
    /// behaves exactly as today" a property of the type instead of a habit of its callers.
    /// </remarks>
    public static float ResolveDrawingAccuracyThreshold(LevelConfigSO level, float globalThreshold)
    {
        if (level == null || !level.overrideDrawingAccuracyThreshold)
            return globalThreshold;

        return Mathf.Clamp01(level.drawingAccuracyThresholdOverride);
    }

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
