using UnityEditor;

public static class CampaignOutcomeFaultMenu
{
    [MenuItem("Salinlahi/Debug/SALIN-174/Fail Next Save Promotion")]
    private static void FailNextSavePromotion()
    {
        CampaignSaveFileStorage.EditorFailNextAt = StorageFaultPoint.PromoteTemporary;
    }

    [MenuItem("Salinlahi/Debug/SALIN-174/Clear Injected Save Fault")]
    private static void ClearInjectedSaveFault()
    {
        CampaignSaveFileStorage.EditorFailNextAt = StorageFaultPoint.None;
    }

    // SALIN-220 AC6. Forces one objective flag false on the next level completion, so the unlock
    // gate can be demonstrated withholding the successor's unlock. Every level that can be
    // completed today satisfies all five, so without an injected fault the gate is unobservable
    // in normal play. The forced value survives until cleared.

    [MenuItem("Salinlahi/Debug/SALIN-220/Force Missing Objective: Story Viewed")]
    private static void ForceMissingStoryViewed()
    {
        LevelObjectiveFlagResolver.EditorForceUnsatisfied = LevelObjectives.StoryViewed;
    }

    [MenuItem("Salinlahi/Debug/SALIN-220/Force Missing Objective: Symbols Practiced")]
    private static void ForceMissingSymbolsPracticed()
    {
        LevelObjectiveFlagResolver.EditorForceUnsatisfied = LevelObjectives.SymbolsPracticed;
    }

    [MenuItem("Salinlahi/Debug/SALIN-220/Force Missing Objective: Words Restored")]
    private static void ForceMissingWordsRestored()
    {
        LevelObjectiveFlagResolver.EditorForceUnsatisfied = LevelObjectives.WordsRestored;
    }

    [MenuItem("Salinlahi/Debug/SALIN-220/Force Missing Objective: Context Passed")]
    private static void ForceMissingContextPassed()
    {
        LevelObjectiveFlagResolver.EditorForceUnsatisfied = LevelObjectives.ContextPassed;
    }

    [MenuItem("Salinlahi/Debug/SALIN-220/Force Missing Objective: Final Syllable Restored")]
    private static void ForceMissingFinalSyllableRestored()
    {
        LevelObjectiveFlagResolver.EditorForceUnsatisfied = LevelObjectives.FinalSyllableRestored;
    }

    [MenuItem("Salinlahi/Debug/SALIN-220/Clear Forced Missing Objective")]
    private static void ClearForcedMissingObjective()
    {
        LevelObjectiveFlagResolver.EditorForceUnsatisfied = null;
    }
}
