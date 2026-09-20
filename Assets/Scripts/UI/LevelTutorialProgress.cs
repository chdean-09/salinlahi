using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides whether a level's onboarding tutorial runs, and records that it has run.
///
/// <para>
/// Showing and having-been-seen are deliberately two separate questions here. The gate
/// (<see cref="ShouldShowForLevel"/> / <see cref="ShouldShowForLevelNumber"/>) answers "play the
/// sequence now?", and on a level whose config sets <c>alwaysShowTutorial</c> it answers yes
/// forever. <see cref="HasSeenTutorialForLevel"/> answers "has the player ever finished this?",
/// and it keeps answering truthfully, because the onboarding uses it to decide whether a replay
/// may offer a skip and other surfaces may come to depend on it. Suppressing the record to make
/// the gate replay would have been the smaller edit and the worse one: it would silently break
/// every other reader of "ever seen" to express something the gate can express on its own.
/// </para>
/// </summary>
public static class LevelTutorialProgress
{
    public const int TutorialLevelNumber = ProgressManager.Level1FtueTutorialLevelNumber;
    public const int Level1TutorialLevelNumber = ProgressManager.Level1FtueTutorialLevelNumber;
    public const int Level2TutorialLevelNumber = ProgressManager.Level2AdvancedTutorialLevelNumber;
    public const string Level1FtueSeenKey = ProgressManager.Level1FtueSeenKey;
    public const string Level2AdvancedSeenKey = ProgressManager.Level2AdvancedSeenKey;

    /// <summary>
    /// Answer used when no <see cref="LevelConfigSO"/> can be resolved for a level number — no
    /// campaign is wired yet, or the gate is being asked from an EditMode test with no scene.
    ///
    /// It tracks <c>LevelConfigSO.alwaysShowTutorial</c>'s own default, so an unresolvable config
    /// and a default-authored one behave identically. That default is FALSE: the flag is opt-in,
    /// authored on Level 1 alone, because a true default would have switched all fifteen levels
    /// at once.
    ///
    /// Which also settles the direction a failed lookup should fail in. A replay is the LOOSER
    /// behaviour — it defeats the seen gate — so defaulting it on would let a lookup failure
    /// silently force a tutorial the level never asked to replay. That is what it did: with this
    /// at true, asking the gate for an already-seen Level 2 from an EditMode test returned "play
    /// it again", breaking ShouldShowForLevelNumber_WhenLevelTwoSeen_ReturnsFalse. A fallback
    /// should never grant behaviour the authored data did not.
    /// </summary>
    /// <remarks>
    /// It mirrors the AUTHORED data rather than the C# field default. The field defaults false
    /// because it is opt-in, but Level 1 is the one level that authors it true, so a flat false
    /// would make an unresolvable lookup contradict the only level the flag exists for — and
    /// Level 1's tutorial silently stops replaying, which is the exact bug this whole change was
    /// asked to fix. A flat true is equally wrong in the other direction: it granted an
    /// already-seen Level 2 a replay nobody authored.
    /// </remarks>
    private static bool FallbackAlwaysShowTutorial(int levelNumber) =>
        levelNumber == Level1TutorialLevelNumber;

    /// <summary>
    /// Levels whose stale resume index has already been discarded in this session. See
    /// <see cref="DiscardStaleResumeIndexOnce"/> — the discard has to happen once per level per
    /// run, not on every gate query, because the gate is also consulted while the tutorial is
    /// mid-flight and wiping the index then would fight the sequence that is writing it.
    /// </summary>
    private static readonly HashSet<int> ResumeIndexDiscarded = new();

    /// <summary>
    /// True when this level's onboarding sequence should play now. Prefers the flag authored on the
    /// config it is handed, which is also the only reliable answer before a campaign is wired.
    /// </summary>
    public static bool ShouldShowForLevel(LevelConfigSO levelConfig)
    {
        if (levelConfig == null)
            return false;

        return ShouldShowTutorial(levelConfig.levelNumber, levelConfig.alwaysShowTutorial);
    }

    /// <summary>
    /// True only when the level itself authored an onboarding source. A level may still use the
    /// historical level-number gate for its progress record, but a null sequence is an intentional
    /// "no onboarding" choice and must not produce a missing-asset warning at runtime.
    /// </summary>
    public static bool HasAuthoredOnboardingSequence(LevelConfigSO levelConfig)
    {
        return levelConfig != null
            && (levelConfig.onboardingSequence != null || levelConfig.tutorialSequence != null);
    }

    /// <summary>
    /// True when the given level's onboarding sequence should play now. Resolves the level's
    /// config from the active campaign to read its <c>alwaysShowTutorial</c> flag, so the two
    /// overloads agree; callers that already hold the config should prefer
    /// <see cref="ShouldShowForLevel"/> and save the lookup.
    /// </summary>
    public static bool ShouldShowForLevelNumber(int levelNumber)
    {
        return ShouldShowTutorial(levelNumber, AlwaysShowsTutorial(levelNumber));
    }

    /// <summary>
    /// True when this level replays its tutorial on every play — the <c>alwaysShowTutorial</c>
    /// question on its own, with none of the seen gate <see cref="ShouldShowForLevel"/> layers on
    /// top of it.
    ///
    /// <para>
    /// It exists because the level's tutorial is not all in one place. The pre-combat onboarding
    /// sequence asks <see cref="ShouldShowForLevel"/>, but the enemy lesson embedded in the same
    /// level's combat (<c>EnemyIntroductionBeat</c>) is gated by a different, campaign-wide
    /// one-shot and cannot use that gate: its answer is per-level-number and it would report false
    /// for any level with no authored onboarding sequence. What the lesson needs is only this half
    /// of the rule. Exposing it here rather than letting the beat read <c>alwaysShowTutorial</c>
    /// itself is deliberate — two places deriving the same rule independently is how Level 1 came
    /// to replay its framing beats while silently dropping the lesson between them.
    /// </para>
    /// </summary>
    public static bool AlwaysShowsTutorialForLevel(LevelConfigSO levelConfig)
    {
        // A level with no config cannot be asserted to replay anything. A replay is the LOOSER
        // behaviour — see FallbackAlwaysShowTutorial — so an unresolvable level fails closed.
        return levelConfig != null && levelConfig.alwaysShowTutorial;
    }

    /// <summary>
    /// <see cref="AlwaysShowsTutorialForLevel"/> for a caller that holds only a level number.
    /// Resolves the config from the active campaign and falls back to
    /// <see cref="FallbackAlwaysShowTutorial"/> when it cannot be resolved, so the two overloads
    /// agree; callers that already hold the config should prefer the other and save the lookup.
    /// </summary>
    public static bool AlwaysShowsTutorialForLevelNumber(int levelNumber) =>
        AlwaysShowsTutorial(levelNumber);

    /// <summary>
    /// True when this level's tutorial is about to play again even though the player has already
    /// completed it — that is, the replay exists only because <c>alwaysShowTutorial</c> defeated
    /// the seen gate. A forced replay must start at the first beat rather than resume, so the
    /// onboarding's resume logic needs to be able to tell the two cases apart: an interrupted
    /// first run genuinely should resume where it stopped.
    /// </summary>
    public static bool IsForcedTutorialReplay(int levelNumber)
    {
        if (levelNumber != Level1TutorialLevelNumber && levelNumber != Level2TutorialLevelNumber)
            return false;

        return AlwaysShowsTutorial(levelNumber) && HasSeenTutorialForLevel(levelNumber);
    }

    /// <summary>
    /// Resolves the beat index a run of this level's tutorial must actually start from, given the
    /// last-completed index the onboarding persisted.
    /// <para>
    /// This exists because "always show" and "resume where you left off" contradict each other. A
    /// completed sequence leaves the last-completed index sitting at the final beat, so resuming
    /// from index + 1 starts past the end of the beat order and the tutorial plays nothing at all
    /// — it is gated open and then runs zero beats, which reads exactly like the gate being
    /// broken. On a forced replay the stored index is stale by definition and the run starts at
    /// zero; on a genuine first run it is honoured, so quitting midway still resumes.
    /// </para>
    /// </summary>
    /// <param name="levelNumber">Level whose tutorial is starting.</param>
    /// <param name="persistedLastCompletedBeatIndex">
    /// Last completed beat index as stored by the onboarding, or -1 when none has completed.
    /// </param>
    /// <returns>The beat index to begin the sequence from.</returns>
    public static int ResolveTutorialStartBeatIndex(int levelNumber, int persistedLastCompletedBeatIndex)
    {
        if (IsForcedTutorialReplay(levelNumber))
            return 0;

        return persistedLastCompletedBeatIndex < 0 ? 0 : persistedLastCompletedBeatIndex + 1;
    }

    public static bool HasSeenLevel1Tutorial()
    {
        if (UsesRevisedProgress())
            return HasSeenRevisedLevel(Level1TutorialLevelNumber);
        return PlayerPrefs.GetInt(Level1FtueSeenKey, 0) == 1;
    }

    public static bool HasSeenLevel2Tutorial()
    {
        if (UsesRevisedProgress())
            return HasSeenRevisedLevel(Level2TutorialLevelNumber);
        return PlayerPrefs.GetInt(Level2AdvancedSeenKey, 0) == 1;
    }

    public static bool HasSeenTutorialForLevel(int levelNumber)
    {
        if (levelNumber == Level1TutorialLevelNumber)
            return HasSeenLevel1Tutorial();

        if (levelNumber == Level2TutorialLevelNumber)
            return HasSeenLevel2Tutorial();

        return true;
    }

    // The three Mark* methods below keep writing their record even on a level that always replays.
    // The write is redundant for the gate and load-bearing for everything else: the onboarding
    // only offers its skip to a player who has finished the sequence before, and that judgement
    // comes from the same record. A replay that recorded nothing would offer no skip and force
    // every returning player through the whole sequence — the opposite of the intent. The revised
    // path's repository setter is a monotonic ratchet, so these writes are also idempotent.

    public static void MarkLevel1TutorialSeen()
    {
        if (UsesRevisedProgress())
        {
            SaveManager.Instance.Repository.TryRecordTutorialProgress(GetStableLevelId(Level1TutorialLevelNumber), true, -1);
            return;
        }
        PlayerPrefs.SetInt(Level1FtueSeenKey, 1);
        PlayerPrefs.Save();
    }

    public static void MarkLevel2TutorialSeen()
    {
        if (UsesRevisedProgress())
        {
            SaveManager.Instance.Repository.TryRecordTutorialProgress(GetStableLevelId(Level2TutorialLevelNumber), true, -1);
            return;
        }
        PlayerPrefs.SetInt(Level2AdvancedSeenKey, 1);
        PlayerPrefs.Save();
    }

    public static void MarkTutorialSeen(int levelNumber)
    {
        if (levelNumber == Level1TutorialLevelNumber)
        {
            MarkLevel1TutorialSeen();
            return;
        }

        if (levelNumber == Level2TutorialLevelNumber)
            MarkLevel2TutorialSeen();
    }

    private static bool ShouldShowTutorial(int levelNumber, bool alwaysShowTutorial)
    {
        // The legacy progress record only covers Levels 1 and 2. The caller that owns level-flow
        // sequencing additionally checks HasAuthoredOnboardingSequence, so a null Level 2 asset
        // cannot turn this historical key into a missing-onboarding warning.
        if (levelNumber != Level1TutorialLevelNumber && levelNumber != Level2TutorialLevelNumber)
            return false;

        if (!alwaysShowTutorial)
            return !HasSeenTutorialForLevel(levelNumber);

        DiscardStaleResumeIndexOnce(levelNumber);
        return true;
    }

    private static bool AlwaysShowsTutorial(int levelNumber)
    {
        LevelConfigSO config = ResolveLevelConfig(levelNumber);
        return config != null ? config.alwaysShowTutorial : FallbackAlwaysShowTutorial(levelNumber);
    }

    private static LevelConfigSO ResolveLevelConfig(int levelNumber)
    {
        CampaignConfigSO campaign = SaveManager.Instance != null ? SaveManager.Instance.Campaign : null;
        if (campaign == null)
            return null;

        string stableLevelId = GetStableLevelId(levelNumber);
        if (stableLevelId == null)
            return null;

        return campaign.TryGetLevel(stableLevelId, out LevelConfigSO config) ? config : null;
    }

    /// <summary>
    /// Drops the legacy resume index for a forced replay, once per level per session.
    /// <para>
    /// The onboarding reads its start beat from a persisted last-completed index. On the legacy
    /// PlayerPrefs path that index survives a completed run — the sequence's own clear is issued
    /// from inside its final beat and then immediately overwritten, because the controller records
    /// that beat's completion after it returns — so a replay would resume past the last beat and
    /// play nothing. Deleting the key here is the one place a file that owns the gate can reach
    /// that store.
    /// </para>
    /// <para>
    /// Gated on the replay actually being forced, so an interrupted first run keeps its resume
    /// point, and gated to once per session because the gate is re-queried while the tutorial is
    /// running and a later query must not wipe the index the sequence is currently writing. The
    /// revised save path cannot be corrected from here at all: its index lives in the save
    /// document behind a monotonic ratchet that cannot lower a value, which is what
    /// <see cref="ResolveTutorialStartBeatIndex"/> is for.
    /// </para>
    /// </summary>
    private static void DiscardStaleResumeIndexOnce(int levelNumber)
    {
        if (!ResumeIndexDiscarded.Add(levelNumber))
            return;

        if (UsesRevisedProgress() || !IsForcedTutorialReplay(levelNumber))
            return;

        PlayerPrefs.DeleteKey(levelNumber == Level2TutorialLevelNumber
            ? ProgressManager.Level2AdvancedBeatIndexKey
            : ProgressManager.Level1FtueBeatIndexKey);
        PlayerPrefs.Save();
    }

    private static bool UsesRevisedProgress()
    {
        return SaveManager.Instance != null && SaveManager.Instance.Mode == SaveManagerMode.RevisedReady &&
            SaveManager.Instance.Repository != null;
    }

    private static string GetStableLevelId(int levelNumber)
    {
        return levelNumber >= 1 && levelNumber <= ContentIdentity.RevisedLevelIds.Count
            ? ContentIdentity.RevisedLevelIds[levelNumber - 1]
            : null;
    }

    private static bool HasSeenRevisedLevel(int levelNumber)
    {
        TutorialProgressRecord record = SaveManager.Instance.Repository.GetTutorialProgress(GetStableLevelId(levelNumber));
        return record != null && record.seen;
    }

#if UNITY_EDITOR
    public static void ResetLevel1TutorialForTests()
    {
        // The session-scoped discard set has to go with the keys, or a test that forced a replay
        // leaves the next test unable to discard and reading a resume index the first test wrote.
        ResumeIndexDiscarded.Clear();
        if (UsesRevisedProgress())
            return;
        PlayerPrefs.DeleteKey(Level1FtueSeenKey);
        PlayerPrefs.DeleteKey(Level2AdvancedSeenKey);
        PlayerPrefs.Save();
    }
#endif
}
