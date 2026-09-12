using System;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// SALIN-225 removal guards. A removal ticket's suite can go green simply because the tests that
/// asserted the removed behaviour were deleted along with it. These tests assert absence
/// POSITIVELY, so they only pass once the removal is actually complete, and they fail if anyone
/// reinstates the cut mechanics.
/// </summary>
/// <remarks>
/// Scope fence, per ruling Q15: the cut list is exactly combo powers, Focus Mode and Endless Mode.
/// The AOE mass-clear (<c>multiKillChainEnabled</c>, <c>EventBus.OnAOETriggered</c>,
/// <c>MassClearBadge</c>) SURVIVES and is deliberately not asserted absent here. Do not "tidy" it
/// into this list -- <c>ComboTeachBeat</c> was named for combos but taught the AOE draw, which is
/// why that trap is worth naming in the guard itself.
/// </remarks>
public class ComboRemovalGuardTests
{
    // Resolved off a type that certainly lives in Salinlahi.Runtime, so the lookup below searches
    // the same assembly the deleted types used to live in.
    private static readonly System.Reflection.Assembly RuntimeAssembly = typeof(CombatResolver).Assembly;

    private static readonly string[] RemovedTypeNames =
    {
        "ComboManager",
        "ComboPower",
        "ComboPowerResolver",
        "ComboTeachBeat",
        "FocusModeTeachBeat",
        "ComboDisplay",
        "FocusModeIndicator",
    };

    /// <summary>G1 — the cut types are gone from the runtime assembly, not merely unreferenced.</summary>
    [Test]
    public void RemovedComboAndFocusTypes_AreAbsentFromTheRuntimeAssembly()
    {
        foreach (string typeName in RemovedTypeNames)
        {
            Assert.That(RuntimeAssembly.GetType(typeName), Is.Null,
                $"SALIN-225 removed {typeName}; finding it back in {RuntimeAssembly.GetName().Name} "
                + "means combo powers or Focus Mode were reinstated.");
        }
    }

    /// <summary>
    /// G1b — the surviving AOE mass-clear is the near-homograph this ticket must NOT remove
    /// (ruling Q15 cuts combo powers, Focus Mode and Endless Mode; mass-clear is not on that list).
    /// </summary>
    [Test]
    public void AoeMassClear_SurvivesTheRemoval()
    {
        Assert.That(RuntimeAssembly.GetType("MassClearBadge"), Is.Not.Null,
            "The AOE mass-clear survives ruling Q15 and must not be removed with the combo powers.");

        Assert.That(
            typeof(LevelConfigSO).GetField("multiKillChainEnabled"), Is.Not.Null,
            "multiKillChainEnabled gates the surviving AOE draw. It sat one line below "
            + "focusModeEnabled and must not have gone with it.");
        Assert.That(
            typeof(LevelConfigSO).GetField("focusModeEnabled"), Is.Null,
            "focusModeEnabled is the field SALIN-225 actually removes.");
    }

    /// <summary>G2 — the beat types are gone from the enum AND from Level 2's authored beat order.</summary>
    [Test]
    public void Level2Onboarding_NoLongerDefinesOrSchedulesTheTeachBeats()
    {
        Assert.That(Enum.IsDefined(typeof(OnboardingBeatType), "ComboTeach"), Is.False,
            "OnboardingBeatType.ComboTeach went with the beat it scheduled.");
        Assert.That(Enum.IsDefined(typeof(OnboardingBeatType), "FocusModeTeach"), Is.False,
            "OnboardingBeatType.FocusModeTeach went with the beat it scheduled.");

#if UNITY_EDITOR
        OnboardingSequenceSO sequence = AssetDatabase.LoadAssetAtPath<OnboardingSequenceSO>(
            "Assets/ScriptableObjects/Tutorial/Level2AdvancedOnboardingSequence.asset");

        Assert.That(sequence, Is.Not.Null,
            "Level 2's onboarding asset stays in the project so SALIN-241 authors into an "
            + "existing, wired slot rather than re-creating one.");
        // SALIN-241 authored Level 2's replacement, so this no longer pins [Release] exactly.
        // The removal fence is what matters and it is kept: whatever Level 2 now schedules, it must
        // be a real order that still ends in Release, and it must not be the cut teach beats coming
        // back. Their absence from the enum is already asserted above, which is the stronger guard.
        Assert.That(sequence.beatOrder, Is.Not.Null.And.Not.Empty,
            "Level 2 must schedule at least one beat; an empty order is how the level silently "
            + "stopped onboarding anything.");
        Assert.That(sequence.beatOrder[sequence.beatOrder.Length - 1],
            Is.EqualTo(OnboardingBeatType.Release),
            "Release must stay last: it is the sole caller of MarkTutorialSeen, so dropping it "
            + "would make Level 2's onboarding replay on every entry.");
        foreach (OnboardingBeatType beat in sequence.beatOrder)
        {
            Assert.That(beat.ToString(), Is.Not.EqualTo("ComboTeach").And.Not.EqualTo("FocusModeTeach"),
                "Level 2 must not re-schedule a beat SALIN-225 removed.");
        }
#endif
    }

    /// <summary>G2b — the endless unlock flag is gone from the save document, and nothing writes it.</summary>
    [Test]
    public void EndlessModeUnlocked_IsRemovedFromTheSaveDocument_AndNothingWritesIt()
    {
        // SALIN-225 kept the field and this assertion pinned the "keep" half of that decision.
        // SALIN-227 removed it at save schema v4, so the assertion is INVERTED rather than deleted:
        // it now pins the "removed" half. Re-adding the field without moving CurrentSaveSchemaVersion
        // would make old and new saves indistinguishable on disk, which is what this still guards.
        Assert.That(typeof(CampaignProgressData).GetField("endlessModeUnlocked"), Is.Null,
            "SALIN-227 removed this field at save schema v4; it must not come back unversioned.");
        Assert.That(CampaignSaveDocument.CurrentSaveSchemaVersion, Is.GreaterThanOrEqualTo(4),
            "The field removal is only safe because the schema version moved with it.");

        Assert.That(typeof(CampaignProgressRepository).GetMethod("TryUnlockEndlessMode"), Is.Null,
            "SALIN-225 removed the writer; completing the final level must set no endless flag.");
        Assert.That(typeof(ProgressManager).GetMethod("UnlockEndlessMode"), Is.Null,
            "SALIN-225 removed the legacy-path writer too.");
        Assert.That(typeof(ProgressManager).GetMethod("IsEndlessModeUnlocked"), Is.Null,
            "Nothing reads the flag at runtime any more.");
    }
}
