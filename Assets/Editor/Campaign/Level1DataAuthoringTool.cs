using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authors every piece of Level 1 data the v3 design plan settles, in one re-runnable pass:
/// the level's <see cref="SpawnAssignmentPolicy"/>, its drawing-accuracy override, the four
/// introduction-card ability lines, and the glyph confusion-pair table Iligaw's false copy reads.
///
/// <para>
/// <b>Why a tool rather than hand-edited YAML.</b> These assets are open in the running Editor,
/// which holds its own deserialized copy and rewrites the file on any save — so a YAML edit is
/// silently clobbered. Running the authoring through the AssetDatabase lets Unity own the
/// serialization, the same reason <see cref="SpawnAssignmentPolicyAuthoring"/> exists.
/// </para>
///
/// <para>
/// <b>Why it mutates and never recreates.</b> <c>AssetDatabase.CreateAsset</c> over a path that
/// already holds an asset reissues that asset's GUID, which unwires every reference to it without
/// reporting anything — a level config referenced by the campaign, an EnemyDataSO referenced by
/// prefabs and the pool. So every target here is loaded and mutated in place, and the one asset
/// this tool may have to bring into existence (the confusion-pair table) is created only after a
/// project-wide search has established that no such asset exists yet. That is also what makes a
/// second run safe: it compares before it writes, so a no-op run reports "already correct" for
/// every field and never marks an asset dirty.
/// </para>
///
/// <para>
/// A missing asset or a missing serialized field is reported as a warning and skipped rather than
/// thrown, because a partial authoring pass that names what it could not reach is more useful in a
/// live Editor than an exception that abandons the fields it had already resolved.
/// </para>
///
/// Design source: the Level 1 design plan v3, §4 (spawn-assignment deltas), §3 (introduction
/// template, ability lines), §7 (tuning table) and open item A (the decoy's confusion pair).
/// Schedule values defer to docs/design/spawn-assignment-system.md, which authored them.
/// </summary>
public static class Level1DataAuthoringTool
{
    private const string LevelPath = "Assets/ScriptableObjects/Levels/Level1_Config.asset";
    private const string EnemyFolder = "Assets/ScriptableObjects/Enemies";

    /// <summary>
    /// Where the confusion table is created when the project has none. Root of ScriptableObjects
    /// beside the other campaign-wide lookups (GameConfig_Default, GlyphBadgeConfig_Default,
    /// RecognitionConfig_Default) rather than under Levels/, because the table is not Level 1 data:
    /// Level 1 merely contributes its first row.
    /// </summary>
    private const string ConfusionPairsPath =
        "Assets/ScriptableObjects/GlyphConfusionPairs_Default.asset";

    // ---------------------------------------------------------------------------------------
    // §7 tuning table. Every value below is authored there; none is a bare literal in a method.
    // The scalars restate what the shipped spawn-assignment doc already derived for Level 1 --
    // they are written explicitly because Level1_Config was authored before the policy field
    // existed, so any value left unwritten is a C# default that silently tracks a code edit.
    // ---------------------------------------------------------------------------------------

    /// <summary>§7: anti-rush floor 4, derived from Level 1's 130s target over its 24-enemy budget.</summary>
    private const int MinSpawnsBeforeNeeded = 4;

    /// <summary>§7: P(the spawn carries the needed symbol) once the floor is satisfied.</summary>
    private const float NeededWeight = 0.5f;

    /// <summary>§7: seconds before the needed symbol is force-spawned (supply failure).</summary>
    private const float StarvationTimeout = 30f;

    /// <summary>§7: seconds before escalation (comprehension failure, filler suppressed).</summary>
    private const float HardStarvationTimeout = 50f;

    /// <summary>§7: load-bearing in Level 1 -- below 2 distinct later-needed symbols, filler also draws restored slots.</summary>
    private const int MinFillerVariety = 2;

    /// <summary>§7: 0 because Level 1's cumulative pool is exactly its target text.</summary>
    private const float OffTargetFillerWeight = 0f;

    /// <summary>§7: 1 restores strict left-to-right restoration order.</summary>
    private const int ActiveSlotWindow = 1;

    /// <summary>
    /// §7 / §2 B7: the NA slot of INA. Zero-based flattened index 1; the design prose calls the
    /// same slot "slot 2" because it counts from one.
    /// </summary>
    private const int ChoiceMomentSlotIndex = 1;

    /// <summary>§7: seconds within which the choice pair's second member must spawn.</summary>
    private const float ChoicePairWindow = 1.5f;

    /// <summary>§7: concurrency ceiling the choice pair checks before it is issued.</summary>
    private const int MaxConcurrentEnemies = 8;

    /// <summary>§7: required, not optional -- enemyCount is a pacing target, not a hard cap.</summary>
    private const bool AllowFinalWaveOverflow = true;

    /// <summary>§7: 0 seeds from the clock. Non-zero only to make a specific playtest replayable.</summary>
    private const int AssignmentSeed = 0;

    /// <summary>
    /// §4 delta 1: the MA of AMA, zero-based flattened index 3 (the doc's 1-based "slot 4").
    /// Gating it on Abo's ash is what makes the level structurally incapable of completing before
    /// that ability has been shown; the slot list was shipping with an empty gate list, so the
    /// withhold has never actually been active.
    /// </summary>
    private const int GatedSlotIndex = 3;

    /// <summary>
    /// §4 delta 2: the E/I of INA, zero-based flattened index 0, floored at 1 instead of 4 so the
    /// tutorial's first drawing restores a slot on spawn 2 rather than spawn 5. Slots 1-3 keep the
    /// level-wide floor, so the anti-rush purpose survives for the rest of the level.
    /// </summary>
    private const int TutorialFirstSlotIndex = 0;

    /// <summary>§4 delta 2: the lowered floor for the tutorial's first fill.</summary>
    private const int TutorialFirstSlotFloor = 1;

    /// <summary>
    /// §4 delta 3 / §7: forces spawn 1 to carry A, which by the Level 1 bijection forces the first
    /// enemy the player ever meets to be Abo ng Simula -- the type B3's opening beat is written
    /// around. A spoken value id, not a symbol id, because the policy class holds no UnityEngine
    /// references.
    /// </summary>
    private const string OpeningSpawnSpokenValueId = "value.a";

    /// <summary>
    /// §7: Level 1 accepts 0.45 against the 0.60 campaign default, so a learner's first four
    /// drawings are not rejected for wobble. Authored as a per-level override; the shared
    /// RecognitionConfig_Default is deliberately untouched.
    /// </summary>
    private const float DrawingAccuracyThreshold = 0.45f;

    /// <summary>
    /// §3: one sentence per type stating what the enemy DOES, never how to beat it. Lifted from the
    /// authored descriptions, per open item C. The counter is the player's to derive -- an ability
    /// line that names it turns the introduction card into an instruction and retires the lesson.
    /// </summary>
    private static readonly (string Asset, string Line)[] AbilityLines =
    {
        ("EnemyData_AbongSimula",   "It covers the first symbol of a word with ash."),
        ("EnemyData_NawalangMukha", "It removes names from characters and dialogue boxes."),
        ("EnemyData_Iligaw",        "It makes a false copy of the character it carries."),
        ("EnemyData_Mantsa",        "It stains correct symbols and changes them into incorrect forms."),
    };

    /// <summary>Iligaw owns the confusion table, because its decoy is the table's only consumer.</summary>
    private const string DecoyEnemyAsset = "EnemyData_Iligaw";

    /// <summary>
    /// Open item A / §2 B8: Level 1's confusable pair is A and E/I, which differ by a single dot.
    /// Resolved by stableId rather than by path or GUID so a moved or re-created character asset
    /// still resolves, and so the tool fails loudly on a renamed id instead of quietly wiring the
    /// wrong glyph.
    /// </summary>
    private const string StableIdA = "symbol.a";

    /// <summary>The other end of Level 1's dot pair. See <see cref="StableIdA"/>.</summary>
    private const string StableIdEI = "symbol.ei";

    /// <summary>
    /// GUIDs Level1_Config's cumulativeSymbolPool points at today, used ONLY to confirm the
    /// stableId lookup landed on the assets the level actually plays with. A mismatch is a warning,
    /// not a lookup path: hardcoding GUIDs as the resolution mechanism is what makes an authoring
    /// tool silently wire a stale asset after someone re-creates a character.
    /// </summary>
    private const string ExpectedGuidA = "c2947f4f6a8e4db5a67d5c4d4586af79";

    /// <summary>See <see cref="ExpectedGuidA"/>.</summary>
    private const string ExpectedGuidEI = "d292afcc22b84c0fbdfd9b9f60f8b13b";

    /// <summary>Authoring note stored on the confusion row. Never shown to the player.</summary>
    private const string PairNote = "One dot apart: A has no vowel dot, E/I carries it above.";

    /// <summary>
    /// Floats are compared with a tolerance rather than for equality, so a value that round-trips
    /// through serialization one bit off does not report as a change on every run.
    /// </summary>
    private const float FloatTolerance = 0.0001f;

    /// <summary>
    /// Authors Level 1's spawn policy, accuracy override, ability lines and confusion-pair table,
    /// then prints a per-field report separating what it changed from what was already correct.
    /// Safe to run repeatedly: a second run writes nothing and says so.
    /// </summary>
    [MenuItem("Salinlahi/Campaign/Author Level 1 Data")]
    public static void Author()
    {
        var report = new Report();

        LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
        if (level == null)
        {
            report.Warn($"Level config not found at {LevelPath}. Skipped the spawn policy and the accuracy override.");
        }
        else
        {
            bool policyChanged = AuthorSpawnPolicy(level, report);
            bool thresholdChanged = AuthorAccuracyThreshold(level, report);
            if (policyChanged || thresholdChanged)
            {
                EditorUtility.SetDirty(level);
                report.MarkDirty();
            }
        }

        AuthorAbilityLines(report);
        AuthorConfusionPairs(report);

        if (report.AnyAssetDirtied)
            AssetDatabase.SaveAssets();

        report.Emit();
    }

    // ---------------------------------------------------------------------------------------
    // A -- the spawn assignment policy.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Writes every policy field explicitly, including the ones whose target equals the current C#
    /// default. Level1_Config predates the policy field, so anything left unwritten is not "the
    /// authored value" but "whatever the class happens to default to" -- which moves the moment
    /// someone retunes the defaults for another level.
    /// </summary>
    private static bool AuthorSpawnPolicy(LevelConfigSO level, Report report)
    {
        bool replacedMissingPolicy = level.spawnAssignmentPolicy == null;
        if (replacedMissingPolicy)
        {
            level.spawnAssignmentPolicy = new SpawnAssignmentPolicy();
            report.Changed("Level1.spawnAssignmentPolicy", "(missing)", "new policy instance");
        }

        SpawnAssignmentPolicy policy = level.spawnAssignmentPolicy;

        // Counted as a change so an asset that had no policy at all is saved even in the
        // impossible case where every one of its fields already matched.
        int changes = replacedMissingPolicy ? 1 : 0;

        changes += SetInt(report, "policy.minSpawnsBeforeNeeded", policy.minSpawnsBeforeNeeded,
            MinSpawnsBeforeNeeded, v => policy.minSpawnsBeforeNeeded = v);
        changes += SetFloat(report, "policy.neededWeight", policy.neededWeight,
            NeededWeight, v => policy.neededWeight = v);
        changes += SetFloat(report, "policy.starvationTimeout", policy.starvationTimeout,
            StarvationTimeout, v => policy.starvationTimeout = v);
        changes += SetFloat(report, "policy.hardStarvationTimeout", policy.hardStarvationTimeout,
            HardStarvationTimeout, v => policy.hardStarvationTimeout = v);
        changes += SetInt(report, "policy.minFillerVariety", policy.minFillerVariety,
            MinFillerVariety, v => policy.minFillerVariety = v);
        changes += SetFloat(report, "policy.offTargetFillerWeight", policy.offTargetFillerWeight,
            OffTargetFillerWeight, v => policy.offTargetFillerWeight = v);
        changes += SetInt(report, "policy.activeSlotWindow", policy.activeSlotWindow,
            ActiveSlotWindow, v => policy.activeSlotWindow = v);
        changes += SetInt(report, "policy.choiceMomentSlotIndex", policy.choiceMomentSlotIndex,
            ChoiceMomentSlotIndex, v => policy.choiceMomentSlotIndex = v);
        changes += SetFloat(report, "policy.choicePairWindow", policy.choicePairWindow,
            ChoicePairWindow, v => policy.choicePairWindow = v);
        changes += SetInt(report, "policy.maxConcurrentEnemies", policy.maxConcurrentEnemies,
            MaxConcurrentEnemies, v => policy.maxConcurrentEnemies = v);
        changes += SetBool(report, "policy.allowFinalWaveOverflow", policy.allowFinalWaveOverflow,
            AllowFinalWaveOverflow, v => policy.allowFinalWaveOverflow = v);
        changes += SetInt(report, "policy.assignmentSeed", policy.assignmentSeed,
            AssignmentSeed, v => policy.assignmentSeed = v);
        changes += SetString(report, "policy.openingSpawnSpokenValueId", policy.openingSpawnSpokenValueId,
            OpeningSpawnSpokenValueId, v => policy.openingSpawnSpokenValueId = v);

        changes += AuthorSlotGate(policy, report);
        changes += AuthorSlotFloor(policy, report);

        // Sanitize only on a real write. It is idempotent for these values, but calling it on a
        // no-op run would still touch the instance the Editor is holding, and the point of this
        // tool is that a second run leaves the asset untouched.
        if (changes > 0)
            policy.Sanitize();

        return changes > 0;
    }

    /// <summary>
    /// Level 1 wants exactly one gate: slot 3 withheld until Abo's ash has been shown. Rebuilt only
    /// when the list is not already exactly that, so a re-run neither duplicates the entry nor
    /// reorders a list someone else has extended.
    /// </summary>
    private static int AuthorSlotGate(SpawnAssignmentPolicy policy, Report report)
    {
        policy.slotGates ??= new List<SpawnSlotGate>();

        bool correct = policy.slotGates.Count == 1
            && policy.slotGates[0] != null
            && policy.slotGates[0].slotIndex == GatedSlotIndex
            && string.Equals(policy.slotGates[0].gateToken, SpawnGateRegistry.AboAshShown, StringComparison.Ordinal);

        if (correct)
        {
            report.Unchanged("policy.slotGates",
                $"slot {GatedSlotIndex} gated on {SpawnGateRegistry.AboAshShown}");
            return 0;
        }

        string before = DescribeGates(policy.slotGates);
        policy.slotGates.Clear();
        policy.slotGates.Add(new SpawnSlotGate
        {
            slotIndex = GatedSlotIndex,
            gateToken = SpawnGateRegistry.AboAshShown,
        });

        report.Changed("policy.slotGates", before,
            $"slot {GatedSlotIndex} gated on {SpawnGateRegistry.AboAshShown}");
        return 1;
    }

    /// <summary>
    /// Level 1 wants exactly one per-slot floor override: slot 0 lowered to 1. Same
    /// rebuild-only-if-wrong rule as the gate list, for the same re-runnability reason.
    /// </summary>
    private static int AuthorSlotFloor(SpawnAssignmentPolicy policy, Report report)
    {
        policy.slotFloors ??= new List<SpawnSlotFloor>();

        bool correct = policy.slotFloors.Count == 1
            && policy.slotFloors[0] != null
            && policy.slotFloors[0].slotIndex == TutorialFirstSlotIndex
            && policy.slotFloors[0].minSpawnsBeforeNeeded == TutorialFirstSlotFloor;

        if (correct)
        {
            report.Unchanged("policy.slotFloors",
                $"slot {TutorialFirstSlotIndex} floor {TutorialFirstSlotFloor}");
            return 0;
        }

        string before = DescribeFloors(policy.slotFloors);
        policy.slotFloors.Clear();
        policy.slotFloors.Add(new SpawnSlotFloor
        {
            slotIndex = TutorialFirstSlotIndex,
            minSpawnsBeforeNeeded = TutorialFirstSlotFloor,
        });

        report.Changed("policy.slotFloors", before,
            $"slot {TutorialFirstSlotIndex} floor {TutorialFirstSlotFloor}");
        return 1;
    }

    // ---------------------------------------------------------------------------------------
    // B -- the drawing-accuracy override.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Sets the opt-in flag and its value together. Both halves matter: the value alone is inert,
    /// and the flag alone would accept whatever the value field happens to hold.
    /// </summary>
    private static bool AuthorAccuracyThreshold(LevelConfigSO level, Report report)
    {
        int changes = 0;

        changes += SetBool(report, "Level1.overrideDrawingAccuracyThreshold",
            level.overrideDrawingAccuracyThreshold, true,
            v => level.overrideDrawingAccuracyThreshold = v);
        changes += SetFloat(report, "Level1.drawingAccuracyThresholdOverride",
            level.drawingAccuracyThresholdOverride, DrawingAccuracyThreshold,
            v => level.drawingAccuracyThresholdOverride = v);

        return changes > 0;
    }

    // ---------------------------------------------------------------------------------------
    // C -- the four introduction-card ability lines.
    // ---------------------------------------------------------------------------------------

    private static void AuthorAbilityLines(Report report)
    {
        foreach ((string asset, string line) in AbilityLines)
        {
            string path = $"{EnemyFolder}/{asset}.asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
            if (data == null)
            {
                report.Warn($"Enemy data not found at {path}. Its abilityLine was not authored.");
                continue;
            }

            if (SetString(report, $"{asset}.abilityLine", data.abilityLine, line,
                    v => data.abilityLine = v) > 0)
            {
                EditorUtility.SetDirty(data);
                report.MarkDirty();
            }
        }
    }

    // ---------------------------------------------------------------------------------------
    // D -- the confusion-pair table, and Iligaw's reference to it.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Resolves the A and E/I character assets, makes sure a confusion table exists carrying that
    /// symmetric row, and points Iligaw's decoy at it. One authored row serves both directions, so
    /// an A source yields E/I and an E/I source yields A -- the table's own contract.
    /// </summary>
    private static void AuthorConfusionPairs(Report report)
    {
        BaybayinCharacterSO a = ResolveCharacter(StableIdA, "A", ExpectedGuidA, report);
        BaybayinCharacterSO ei = ResolveCharacter(StableIdEI, "EI", ExpectedGuidEI, report);
        if (a == null || ei == null)
        {
            report.Warn("Could not resolve both ends of Level 1's A/E-I pair. The confusion table "
                + "was left alone, and Iligaw's decoy keeps its previous behaviour.");
            return;
        }

        GlyphConfusionPairsSO table = FindOrCreateConfusionTable(report);
        if (table == null)
            return;

        EnsurePairRow(table, a, ei, report);
        AssignTableToDecoy(table, report);
    }

    /// <summary>
    /// Looks up the table by path first, then anywhere in the project, and only creates one when
    /// the project genuinely has none. The project-wide search is the important half: creating a
    /// second table at this tool's preferred path would leave two tables disagreeing, and the one
    /// Iligaw pointed at would be decided by whichever tool ran last.
    /// </summary>
    private static GlyphConfusionPairsSO FindOrCreateConfusionTable(Report report)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GlyphConfusionPairsSO>(ConfusionPairsPath);
        if (existing != null)
        {
            report.Unchanged("GlyphConfusionPairs asset", $"already at {ConfusionPairsPath}");
            return existing;
        }

        string[] guids = AssetDatabase.FindAssets("t:GlyphConfusionPairsSO");
        if (guids.Length > 0)
        {
            string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var found = AssetDatabase.LoadAssetAtPath<GlyphConfusionPairsSO>(foundPath);
            if (found != null)
            {
                report.Unchanged("GlyphConfusionPairs asset",
                    $"already exists at {foundPath}; reused instead of creating one at {ConfusionPairsPath}");
                if (guids.Length > 1)
                    report.Warn($"{guids.Length} GlyphConfusionPairsSO assets exist. Used {foundPath}.");
                return found;
            }
        }

        var table = ScriptableObject.CreateInstance<GlyphConfusionPairsSO>();
        AssetDatabase.CreateAsset(table, ConfusionPairsPath);
        report.Changed("GlyphConfusionPairs asset", "(none in project)", $"created {ConfusionPairsPath}");
        report.MarkDirty();
        return table;
    }

    /// <summary>
    /// Appends the A/E-I row only when no row already pairs those two glyphs in either order.
    /// Written through <see cref="SerializedObject"/> because the row list is a private serialized
    /// field with no public setter -- deliberately, since the table is read-only at runtime.
    /// </summary>
    private static void EnsurePairRow(
        GlyphConfusionPairsSO table,
        BaybayinCharacterSO a,
        BaybayinCharacterSO ei,
        Report report)
    {
        var so = new SerializedObject(table);
        SerializedProperty pairs = so.FindProperty("_pairs");
        if (pairs == null)
        {
            report.Warn("GlyphConfusionPairsSO has no serialized '_pairs' field. The A/E-I row was not authored.");
            return;
        }

        for (int i = 0; i < pairs.arraySize; i++)
        {
            SerializedProperty row = pairs.GetArrayElementAtIndex(i);
            UnityEngine.Object first = row.FindPropertyRelative("first").objectReferenceValue;
            UnityEngine.Object second = row.FindPropertyRelative("second").objectReferenceValue;

            bool matches = (first == a && second == ei) || (first == ei && second == a);
            if (matches)
            {
                report.Unchanged("GlyphConfusionPairs row", $"{a.name} <-> {ei.name} already authored");
                return;
            }
        }

        int index = pairs.arraySize;
        pairs.InsertArrayElementAtIndex(index);
        SerializedProperty added = pairs.GetArrayElementAtIndex(index);
        added.FindPropertyRelative("first").objectReferenceValue = a;
        added.FindPropertyRelative("second").objectReferenceValue = ei;
        added.FindPropertyRelative("note").stringValue = PairNote;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(table);
        report.MarkDirty();
        report.Changed("GlyphConfusionPairs row", "(absent)", $"{a.name} <-> {ei.name}");
    }

    private static void AssignTableToDecoy(GlyphConfusionPairsSO table, Report report)
    {
        string path = $"{EnemyFolder}/{DecoyEnemyAsset}.asset";
        var iligaw = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
        if (iligaw == null)
        {
            report.Warn($"Enemy data not found at {path}. mirrorDecoyConfusionPairs was not assigned.");
            return;
        }

        if (iligaw.mirrorDecoyConfusionPairs == table)
        {
            report.Unchanged($"{DecoyEnemyAsset}.mirrorDecoyConfusionPairs", table.name);
            return;
        }

        string before = iligaw.mirrorDecoyConfusionPairs == null
            ? "(none)"
            : iligaw.mirrorDecoyConfusionPairs.name;
        iligaw.mirrorDecoyConfusionPairs = table;
        EditorUtility.SetDirty(iligaw);
        report.MarkDirty();
        report.Changed($"{DecoyEnemyAsset}.mirrorDecoyConfusionPairs", before, table.name);
    }

    /// <summary>
    /// Finds a character by its revised-campaign stableId, falling back to the legacy characterID
    /// when no stableId matches. Both are content identity rather than location, so a character
    /// asset can be moved or re-created without this tool wiring a stale reference. The expected
    /// GUID is checked afterwards purely as a cross-reference against Level 1's symbol pool, and a
    /// mismatch warns rather than fails -- the id is the authority, not the GUID.
    /// </summary>
    private static BaybayinCharacterSO ResolveCharacter(
        string stableId,
        string characterId,
        string expectedGuid,
        Report report)
    {
        string[] guids = AssetDatabase.FindAssets("t:BaybayinCharacterSO");
        BaybayinCharacterSO byStableId = null;
        BaybayinCharacterSO byCharacterId = null;
        int stableIdMatches = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var candidate = AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(path);
            if (candidate == null)
                continue;

            if (string.Equals(candidate.stableId, stableId, StringComparison.Ordinal))
            {
                byStableId = candidate;
                stableIdMatches++;
            }
            else if (byCharacterId == null
                && string.Equals(candidate.characterID, characterId, StringComparison.OrdinalIgnoreCase))
            {
                byCharacterId = candidate;
            }
        }

        if (stableIdMatches > 1)
        {
            report.Warn($"{stableIdMatches} character assets claim stableId '{stableId}'. "
                + "Used the last one found; the duplicate should be retired.");
        }

        BaybayinCharacterSO resolved = byStableId ?? byCharacterId;
        if (resolved == null)
        {
            report.Warn($"No BaybayinCharacterSO with stableId '{stableId}' or characterID '{characterId}'.");
            return null;
        }

        if (byStableId == null)
        {
            report.Warn($"No character carries stableId '{stableId}'. Fell back to characterID "
                + $"'{characterId}' ({resolved.name}); the stableId should be repaired.");
        }

        string resolvedGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(resolved));
        if (!string.Equals(resolvedGuid, expectedGuid, StringComparison.OrdinalIgnoreCase))
        {
            report.Warn($"'{stableId}' resolved to {resolved.name} (guid {resolvedGuid}), which is not "
                + $"the asset Level 1's cumulativeSymbolPool references (guid {expectedGuid}). "
                + "The pair may be wired to a character the level does not play with.");
        }

        return resolved;
    }

    // ---------------------------------------------------------------------------------------
    // Compare-then-write helpers. Each reports the field either way, which is what lets a re-run
    // print an explicit "already correct" line per field instead of an empty summary.
    // ---------------------------------------------------------------------------------------

    private static int SetInt(Report report, string field, int current, int target, Action<int> write)
    {
        if (current == target)
        {
            report.Unchanged(field, target.ToString());
            return 0;
        }

        write(target);
        report.Changed(field, current.ToString(), target.ToString());
        return 1;
    }

    private static int SetFloat(Report report, string field, float current, float target, Action<float> write)
    {
        if (Mathf.Abs(current - target) <= FloatTolerance)
        {
            report.Unchanged(field, target.ToString("0.####"));
            return 0;
        }

        write(target);
        report.Changed(field, current.ToString("0.####"), target.ToString("0.####"));
        return 1;
    }

    private static int SetBool(Report report, string field, bool current, bool target, Action<bool> write)
    {
        if (current == target)
        {
            report.Unchanged(field, target.ToString());
            return 0;
        }

        write(target);
        report.Changed(field, current.ToString(), target.ToString());
        return 1;
    }

    private static int SetString(Report report, string field, string current, string target, Action<string> write)
    {
        if (string.Equals(current, target, StringComparison.Ordinal))
        {
            report.Unchanged(field, Quote(target));
            return 0;
        }

        write(target);
        report.Changed(field, Quote(current), Quote(target));
        return 1;
    }

    private static string Quote(string value) =>
        string.IsNullOrEmpty(value) ? "(empty)" : $"\"{value}\"";

    private static string DescribeGates(List<SpawnSlotGate> gates)
    {
        if (gates == null || gates.Count == 0)
            return "(empty -- the withhold was never active)";

        var parts = new List<string>(gates.Count);
        foreach (SpawnSlotGate gate in gates)
            parts.Add(gate == null ? "(null)" : $"slot {gate.slotIndex} -> {gate.gateToken}");

        return string.Join(", ", parts);
    }

    private static string DescribeFloors(List<SpawnSlotFloor> floors)
    {
        if (floors == null || floors.Count == 0)
            return "(empty -- every slot on the level-wide floor)";

        var parts = new List<string>(floors.Count);
        foreach (SpawnSlotFloor floor in floors)
            parts.Add(floor == null ? "(null)" : $"slot {floor.slotIndex} -> {floor.minSpawnsBeforeNeeded}");

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Collects the pass's outcome so the Console gets one message with three sections rather than
    /// a scroll of per-field logs. The changed/unchanged split is the point: it is what makes a
    /// second run visibly a no-op instead of merely a silent one.
    /// </summary>
    private sealed class Report
    {
        private readonly List<string> _changed = new List<string>();
        private readonly List<string> _unchanged = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        public bool AnyAssetDirtied { get; private set; }

        public void MarkDirty() => AnyAssetDirtied = true;

        public void Changed(string field, string before, string after) =>
            _changed.Add($"  {field}: {before} -> {after}");

        public void Unchanged(string field, string value) =>
            _unchanged.Add($"  {field}: {value}");

        public void Warn(string message) => _warnings.Add($"  {message}");

        public void Emit()
        {
            var log = new StringBuilder("Level1DataAuthoringTool: ");
            log.AppendLine(_changed.Count == 0
                ? "no changes needed -- every authored value was already correct."
                : $"{_changed.Count} field(s) changed, {_unchanged.Count} already correct.");

            if (_changed.Count > 0)
            {
                log.AppendLine("CHANGED:");
                log.AppendLine(string.Join("\n", _changed));
            }

            if (_unchanged.Count > 0)
            {
                log.AppendLine("ALREADY CORRECT:");
                log.AppendLine(string.Join("\n", _unchanged));
            }

            if (_warnings.Count > 0)
            {
                log.AppendLine("WARNINGS:");
                log.AppendLine(string.Join("\n", _warnings));
                DebugLogger.LogWarning(log.ToString());
                return;
            }

            DebugLogger.Log(log.ToString());
        }
    }
}
