using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Salinlahi/Enemy Data")]
public class EnemyDataSO : ScriptableObject
{
    [Header("Identity")]
    public string enemyID;             // "standard", "fast", "chain"

    [Tooltip("Almanac display name (e.g. \"Soldado\"). Optional — falls back to nothing if blank.")]
    public string displayName;

    [Tooltip("Discovery overlay subtitle (e.g. \"The Conscripted Shadows\"). Optional — appended after the display name as \"Name - Subtitle\".")]
    public string discoverySubtitle;

    [TextArea]
    [Tooltip("Almanac detail copy. The discovery overlay splits this on \"Power:\" into lore and power text. Optional — the detail view omits empty text.")]
    public string description;

    [Header("Stats")]
    [Tooltip("World units per second the enemy moves toward the base")]
    public float moveSpeed = 1.5f;

    [Header("Health")]
    [Tooltip("1 for regular enemies. 2 for shielded (Capitan, Shokan).")]
    public int maxHealth = 1;

    [Header("Visuals")]
    public Sprite[] walkFrames;
    public RuntimeAnimatorController animatorController;

    [Tooltip("Almanac grid thumbnail + detail picture. Optional — falls back to walkFrames[0].")]
    public Sprite portraitSprite;

    [Header("Character")]
    [Tooltip("The Baybayin character this enemy actually requires to be defeated.")]
    public BaybayinCharacterSO assignedCharacter;

    [Header("Decoy")]
    [Tooltip("If true, this enemy is a decoy variant and drawing its character applies a penalty.")]
    public bool isDecoy;

    [Header("Phaser (Fraile)")]
    [Tooltip("If true, this enemy toggles between visible and invisible states.")]
    public bool isPhaser;
    [Tooltip("Fallback seconds between visibility toggles for Phaser enemies when randomized ranges are not configured.")]
    public float phaserInterval = 0.5f;
    [Tooltip("Minimum visible time after spawn/enable before the first invisibility cycle. <= 0 uses phaserInterval.")]
    public float phaserInitialVisibleDelayMin = 0f;
    [Tooltip("Maximum visible time after spawn/enable before the first invisibility cycle. <= 0 falls back through the same phaserInterval rule as the minimum.")]
    public float phaserInitialVisibleDelayMax = 0f;
    [Tooltip("Optional randomized visible hold minimum in seconds. <= 0 falls back to phaserInterval.")]
    public float phaserVisibleHoldMin = 0f;
    [Tooltip("Optional randomized visible hold maximum in seconds. <= 0 falls back to phaserInterval.")]
    public float phaserVisibleHoldMax = 0f;
    [Tooltip("Optional randomized invisible hold minimum in seconds. <= 0 falls back to phaserInterval.")]
    public float phaserInvisibleHoldMin = 0f;
    [Tooltip("Optional randomized invisible hold maximum in seconds. <= 0 falls back to phaserInterval.")]
    public float phaserInvisibleHoldMax = 0f;
    [Tooltip("Seconds spent fading/pulsing from visible to invisible before full invisibility.")]
    public float phaserFadeOutDuration = 0.3f;
    [Tooltip("Seconds spent fading from invisible to visible.")]
    public float phaserFadeInDuration = 0.2f;
    [Tooltip("Pulse cycles during fade-out telegraph.")]
    public int phaserFadeOutPulseCount = 3;
    [Range(0f, 1f)]
    [Tooltip("Pulse intensity during fade-out. 0 = plain fade, 1 = strongest pulse.")]
    public float phaserFadeOutPulseAmplitude = 0.2f;

    [Header("Contact Behavior")]
    [Tooltip("If false, reaching the Shrine collision zone will despawn this enemy without damaging the Shrine.")]
    public bool dealsContactDamage = true;

    [Header("Variant Era")]
    [Tooltip("Chapter / faction grouping. Used by GeneralAura to limit its buff to American-era allies.")]
    public Era era = Era.Spanish;

    [Header("Glyph Badge Override")]
    [Tooltip("If true, glyphBadgeOffsetOverride replaces GlyphBadgeConfigSO.defaultWorldOffset for this enemy. Use sparingly — boss and other large-sprite variants only.")]
    public bool overrideBadgeOffset;

    [Tooltip("Local-space offset from the enemy root for the GlyphBadge child. Consulted only when overrideBadgeOffset is true.")]
    public Vector2 glyphBadgeOffsetOverride;

    [Tooltip("If true, glyphBadgeScaleOverride replaces GlyphBadgeConfigSO.defaultWorldScale for this enemy.")]
    public bool overrideBadgeScale;

    [Tooltip("World-stable scale multiplier for the GlyphBadge child. Consulted only when overrideBadgeScale is true.")]
    public float glyphBadgeScaleOverride = 1f;

    [Header("Zigzag Mover (Pensionado)")]
    [Tooltip("Horizontal sine amplitude in world units. 0 disables zigzag.")]
    public float zigzagAmplitude = 0f;
    [Tooltip("Sine frequency in Hz. 0 disables zigzag.")]
    public float zigzagFrequency = 0f;

    [Header("Base Speed Modifier (General)")]
    [Tooltip("Multiplier applied on top of moveSpeed. 1.0 = default.")]
    public float baseSpeedMultiplier = 1f;

    [Header("Aura (General)")]
    [Tooltip("Radius in world units. 0 disables aura.")]
    public float auraRadius = 0f;
    [Tooltip("Speed multiplier applied to affected same-era non-boss enemies.")]
    public float auraSpeedMultiplier = 1.3f;

    [Header("Death Animation (optional)")]
    [Tooltip("Frames played in sequence on Defeat() before the enemy returns to the pool. Empty = no death animation; the enemy disappears immediately (existing fast-path behaviour).")]
    public Sprite[] deathFrames;
    [Tooltip("Playback FPS for deathFrames. 0 falls back to the walk animation FPS on Enemy.cs (default 8).")]
    public float deathAnimationFps = 8f;

    [Header("Hurt Feedback (multi-HP enemies)")]
    [Tooltip("Master toggle. If false, no hurt feedback runs even if EnemyHurtFeedback is on the prefab. HP=1 enemies never trigger hurt feedback regardless of this value (they die on the first hit).")]
    public bool useHurtFeedback = true;

    [Header("Hurt Feedback — Movement Pause")]
    [Tooltip("If true, the enemy stops descending for hurtPauseDuration seconds after a non-lethal hit.")]
    public bool hurtPausesMovement = true;
    [Tooltip("Seconds the enemy stays frozen on hit. 0 disables the pause without touching the toggle.")]
    public float hurtPauseDuration = 0.25f;

    [Header("Hurt Feedback — Sprite Shake")]
    [Tooltip("If true, the sprite jitters around its current position for hurtShakeDuration seconds after a non-lethal hit.")]
    public bool hurtShakesSprite = true;
    [Tooltip("Maximum shake offset per axis in world units. 0.08 ~= 1/12th of a 1x1 sprite.")]
    public float hurtShakeMagnitude = 0.08f;
    [Tooltip("Total seconds the shake plays. Should usually be <= hurtPauseDuration so the shake ends inside the freeze window.")]
    public float hurtShakeDuration = 0.2f;
    [Tooltip("Shake oscillations per second. Higher = more frantic. 30 reads as a sharp jolt; 10 reads as a softer wobble.")]
    public float hurtShakeFrequency = 30f;

    [Header("Hurt Feedback — Character Swap")]
    [Tooltip("If true, the carried character changes to postHurtCharacter on the first non-lethal hit. Leave off for variants that should keep their original glyph (e.g. General).")]
    public bool hurtSwapsCharacter = false;
    [Tooltip("The character the enemy demands after the first non-lethal hit. Only consulted when hurtSwapsCharacter is true.")]
    public BaybayinCharacterSO postHurtCharacter;

    [Header("Hurt Feedback — Hurt Animation (optional)")]
    [Tooltip("Frames played in sequence on a non-lethal hit. Empty = no animation; the sprite stays on the current walk frame. Plug in the artist's hurt sheet here when it arrives — no code change required.")]
    public Sprite[] hurtFrames;
    [Tooltip("Playback FPS for hurtFrames. 0 falls back to the walk animation FPS on Enemy.cs (default 8).")]
    public float hurtAnimationFps = 12f;

    [Header("Kisha Charge")]
    [Tooltip("Variant-specific: used only by KishaMover. Speed multiplier applied after Kisha enters Charge state.")]
    public float chargeMultiplier = 2.5f;

    [Range(0f, 1f)]
    [Tooltip("Variant-specific: used only by KishaMover. Viewport Y threshold that starts Kisha's pause/charge sequence. 0 is bottom, 1 is top.")]
    public float chargeTriggerYNormalized = 0.5f;

    [Tooltip("Variant-specific: used only by KishaMover. Seconds Kisha waits between walking and charging.")]
    public float pauseDuration = 0.35f;

    [Header("Kempei Censor")]
    [Tooltip("Variant-specific: used only by KempeiScrambleController. World-space radius around Kempei that receives visual-only label scrambling.")]
    public float scrambleRadius = 3f;

    [Tooltip("Variant-specific: used only by KempeiScrambleController. Minimum seconds a stained badge holds one WRONG face while churning. Floored at GlyphStainCycle.MinimumFalseGlyphInterval so the churn reads as a blur rather than a hard strobe.")]
    public float scrambleMinGlitchInterval = GlyphStainCycle.DefaultFalseMinInterval;

    [Tooltip("Variant-specific: used only by KempeiScrambleController. Maximum seconds a stained badge holds one WRONG face while churning.")]
    public float scrambleMaxGlitchInterval = GlyphStainCycle.DefaultFalseMaxInterval;

    [Tooltip("Variant-specific: used only by KempeiScrambleController. Minimum seconds the badge rests on its TRUE face between churns. This is the window the player reads and acts in, so it is floored at GlyphStainCycle.MinimumReadableInterval.")]
    public float scrambleTrueGlyphMinDwell = GlyphStainCycle.DefaultTrueMinInterval;

    [Tooltip("Variant-specific: used only by KempeiScrambleController. Maximum seconds the badge rests on its TRUE face between churns.")]
    public float scrambleTrueGlyphMaxDwell = GlyphStainCycle.DefaultTrueMaxInterval;

    [Tooltip("Variant-specific: used only by KempeiScrambleController. How many wrong faces scroll past before the true face returns. 1 would be a slow alternation rather than a scroll.")]
    public int scrambleFalseBurstCount = GlyphStainCycle.DefaultFalseBurstCount;

    [Header("Corruption Signature Abilities")]
    [Tooltip("Mantsa (and Kempei): while this enemy lives, nearby enemies' badges are stained into their scrambled, incorrect form. Uses the Kempei Censor radius and glitch intervals above. Enemy.Initialize attaches KempeiScrambleController on the shared shell when set.")]
    public bool stainsNearbyGlyphs;

    [Tooltip("Takip: this enemy's own glyph badge stays covered and is only revealed briefly. Enemy.Initialize attaches GlyphCoverController when set.")]
    public bool coversOwnGlyph;

    [Tooltip("Takip: seconds the badge stays visible right after spawning, before the first cover.")]
    public float glyphCoverInitialRevealSeconds = 1.2f;

    [Tooltip("Takip: seconds the badge stays covered per cycle.")]
    public float glyphCoverHiddenSeconds = 2.4f;

    [Tooltip("Takip: seconds the badge is revealed per cycle.")]
    public float glyphCoverRevealSeconds = 0.8f;

    [Tooltip("Iligaw: spawns one mirrored decoy copy beside itself carrying the same glyph. Drawing the copy while it is the closest match costs a heart (decoy penalty); the copy leaves with its source. Pair with the zigzag fields for 'changes directions'.")]
    public bool spawnsMirrorDecoy;

    [Tooltip("Iligaw: horizontal world offset of the mirrored copy from its source.")]
    public float mirrorDecoyOffsetX = 1.4f;

    [Tooltip("Bakod: while this enemy lives, every non-boss enemy behind it (higher Y, further from the base) is held under a resolution block — drawing that enemy's symbol has no effect until Bakod falls. A targeting constraint only: no lanes, no movement geometry. Enemy.Initialize attaches BakodShieldController when set.")]
    public bool blocksEnemiesBehind;

    [Tooltip("Abo ng Simula: while this enemy lives, the active clue's incomplete-word text ashes over the word's FIRST slot as well as the target one, so the first symbol cannot be read. Display only — acceptance is unaffected and a correct draw still resolves. Enemy.Initialize attaches AshFirstSlotController when set.")]
    public bool ashesFirstSlot;

    [Tooltip("Nawalang Mukha: while this enemy lives, hide character and speaker names from labels and dialogue boxes. Dialogue text and essential combat objectives remain readable. Enemy.Initialize attaches NawalangMukhaNameLossController when set.")]
    public bool removesNames;

    [Tooltip("Kadena: on spawn this enemy chains the nearest other enemy and holds it under a resolution block — it can be neither marked nor damaged — until Kadena is defeated. One target, acquired once, never re-chained. Enemy.Initialize attaches KadenaChainController when set.")]
    public bool chainsNearestEnemy;

    [Tooltip("Hati: when this enemy is defeated it splits into splitCount pieces of splitSpawnData around its position, each carrying this enemy's glyph. The pieces are real enemies that never split again. Enemy.Initialize attaches HatiSplitController when set; Enemy.Defeat invokes it.")]
    public bool splitsOnDefeat;

    [Tooltip("Hati: the enemy data each piece spawns with (the minion). Must not itself split.")]
    public EnemyDataSO splitSpawnData;

    [Tooltip("Hati: how many pieces spawn on defeat.")]
    public int splitCount = 2;

    [Tooltip("Hati: horizontal world distance from the source to the outermost pieces.")]
    public float splitOffsetX = 0.9f;

    [Tooltip("Multiplier on the shared shell's authored transform scale, applied on spawn. 1 keeps the shell's scale; Hati's pieces use a smaller value so they read as the 'smaller enemies' in Hati's description.")]
    public float spriteScale = 1f;

    [HideInInspector]
    [Tooltip("Runtime-only: set on generated decoy copies so they never raise their own discovery event.")]
    public bool suppressDiscovery;
}

public enum Era
{
    Spanish,
    American,
    Japanese
}
