using System;
using System.Collections.Generic;

public sealed class CampaignProgressRepository
{
    private readonly CampaignSaveService _service;
    private readonly CampaignConfigSO _campaign;

    public CampaignProgressRepository(CampaignSaveService service, CampaignConfigSO campaign)
    {
        _service = service;
        _campaign = campaign;
    }

    public string ActiveLevelId => _service.Current?.progress?.activeLevelId;
    public bool IsEndlessModeUnlocked => _service.Current?.progress?.endlessModeUnlocked == true;

    public bool TrySetActiveLevel(string levelId)
    {
        LevelProgressRecord record = FindLevel(levelId);
        if (record == null || !record.unlocked || string.Equals(ActiveLevelId, levelId, StringComparison.Ordinal))
            return record != null && record.unlocked;
        return _service.TryUpdate(document => document.progress.activeLevelId = levelId);
    }

    public bool IsLevelUnlocked(string levelId) => FindLevel(levelId)?.unlocked == true;

    public bool IsSymbolUnlocked(string symbolId) => _service.Current.progress.unlockedSymbolIds.Contains(symbolId);
    public bool IsEnemyDiscovered(string enemyId) => _service.Current.progress.discoveredEnemyIds.Contains(enemyId);
    public bool IsBossDiscovered(string bossId) => _service.Current.progress.discoveredBossIds.Contains(bossId);

    public int GetBestStars(string levelId) => FindLevel(levelId)?.bestStars ?? 0;

    public bool TryUnlockLevel(string levelId)
    {
        if (!IsKnownLevel(levelId)) return false;
        LevelProgressRecord record = FindLevel(levelId);
        if (record.unlocked) return true;
        int index = LevelIds().IndexOf(levelId);
        if (index > 0 && !FindLevel(LevelIds()[index - 1]).completed) return false;
        return _service.TryUpdate(document => FindLevel(document, levelId).unlocked = true);
    }

    public bool TryCompleteLevel(string levelId, int stars)
    {
        if (!IsKnownLevel(levelId) || stars < 1 || stars > 3) return false;
        LevelProgressRecord record = FindLevel(levelId);
        if (!record.unlocked) return false;
        if (record.completed && record.bestStars >= stars &&
            (LevelIds().IndexOf(levelId) == LevelIds().Count - 1 ||
             FindLevel(LevelIds()[LevelIds().IndexOf(levelId) + 1]).unlocked))
            return true;
        return _service.TryUpdate(document =>
        {
            LevelProgressRecord current = FindLevel(document, levelId);
            current.completed = true;
            current.unlocked = true;
            current.bestStars = Math.Max(current.bestStars, stars);
            int index = LevelIds().IndexOf(levelId);
            if (index + 1 < LevelIds().Count)
                FindLevel(document, LevelIds()[index + 1]).unlocked = true;
            else
                document.progress.endlessModeUnlocked = true;
        });
    }

    public bool TryUnlockSymbol(string symbolId) => TryAddId(symbolId, document => document.progress.unlockedSymbolIds,
        IsKnownSymbol);
    public bool TryDiscoverEnemy(string enemyId) => TryAddId(enemyId, document => document.progress.discoveredEnemyIds,
        IsKnownEnemy);
    public bool TryDiscoverBoss(string bossId) => TryAddId(bossId, document => document.progress.discoveredBossIds,
        IsKnownBoss);
    public bool TryUnlockMemory(string memoryId) => TryAddId(memoryId, document => document.progress.unlockedMemoryIds,
        value => ContentIdentity.IsCanonical(value));
    public bool TryClaimReward(string rewardId) => TryAddId(rewardId, document => document.progress.claimedRewardIds,
        value => ContentIdentity.IsCanonical(value));

    public TutorialProgressRecord GetTutorialProgress(string levelId)
    {
        if (_service.Current?.progress?.tutorialProgress == null) return null;
        for (int i = 0; i < _service.Current.progress.tutorialProgress.Count; i++)
        {
            TutorialProgressRecord record = _service.Current.progress.tutorialProgress[i];
            if (record != null && string.Equals(record.levelId, levelId, StringComparison.Ordinal)) return record;
        }
        return null;
    }

    public bool TryRecordTutorialProgress(string levelId, bool seen, int lastCompletedBeatIndex)
    {
        if (!IsKnownLevel(levelId) || lastCompletedBeatIndex < -1) return false;
        TutorialProgressRecord existing = GetTutorialProgress(levelId);
        if (existing != null && existing.seen == seen && existing.lastCompletedBeatIndex >= lastCompletedBeatIndex) return true;
        return _service.TryUpdate(document =>
        {
            TutorialProgressRecord record = FindTutorial(document, levelId);
            if (record == null)
            {
                record = new TutorialProgressRecord { levelId = levelId };
                document.progress.tutorialProgress.Add(record);
            }
            record.seen |= seen;
            record.lastCompletedBeatIndex = Math.Max(record.lastCompletedBeatIndex, lastCompletedBeatIndex);
            document.progress.tutorialProgress.Sort((a, b) => string.CompareOrdinal(a.levelId, b.levelId));
        });
    }

    public bool TryUnlockEndlessMode()
    {
        if (_service.Current.progress.endlessModeUnlocked) return true;
        if (!FindLevel(LevelIds()[LevelIds().Count - 1]).completed) return false;
        return _service.TryUpdate(document => document.progress.endlessModeUnlocked = true);
    }

    public bool TryResetJourney()
    {
        CampaignSaveDocument current = _service.Current;
        if (current == null) return false;
        CampaignSaveDocument clean = CampaignProgressFactory.CreateClean(_campaign, DateTime.UtcNow);
        clean.migration = current.migration;
        clean.recovery = current.recovery;
        return _service.TryUpdate(document =>
        {
            document.progress = clean.progress;
            document.migration = clean.migration;
            document.recovery = clean.recovery;
        });
    }

    public CampaignSaveNotice GetPendingNotice()
    {
        if (_service.Current == null) return new CampaignSaveNotice();
        if (!string.IsNullOrEmpty(_service.Current.recovery.reasonCode) && !_service.Current.recovery.noticeAcknowledged)
            return new CampaignSaveNotice(CampaignSaveNoticeKind.Recovery, _service.Current.recovery.reasonCode);
        if (_service.Current.migration.state == CampaignMigrationState.Completed && !_service.Current.migration.noticeAcknowledged)
            return new CampaignSaveNotice(CampaignSaveNoticeKind.Migration, "migration-completed");
        return new CampaignSaveNotice();
    }

    public bool TryAcknowledgePendingNotice()
    {
        CampaignSaveNotice notice = GetPendingNotice();
        if (notice.kind == CampaignSaveNoticeKind.None) return true;
        return _service.TryUpdate(document =>
        {
            if (notice.kind == CampaignSaveNoticeKind.Recovery) document.recovery.noticeAcknowledged = true;
            if (notice.kind == CampaignSaveNoticeKind.Migration) document.migration.noticeAcknowledged = true;
        });
    }

    private bool TryAddId(string value, Func<CampaignSaveDocument, List<string>> selector, Func<string, bool> known)
    {
        if (!known(value)) return false;
        if (selector(_service.Current).Contains(value)) return true;
        return _service.TryUpdate(document =>
        {
            List<string> values = selector(document);
            if (!values.Contains(value)) values.Add(value);
            values.Sort(StringComparer.Ordinal);
        });
    }

    private bool IsKnownLevel(string value) => LevelIds().Contains(value);
    private bool IsKnownSymbol(string value) => _campaign.TryGetSymbol(value, out _);
    private bool IsKnownEnemy(string value) => KnownEnemyIds().Contains(value);
    private bool IsKnownBoss(string value) => KnownBossIds().Contains(value);

    private List<string> LevelIds() => CampaignSaveValidator.GetConfiguredLevelIds(_campaign);

    private HashSet<string> KnownEnemyIds()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        List<string> levels = LevelIds();
        for (int i = 0; i < levels.Count; i++)
            if (_campaign.TryGetLevel(levels[i], out LevelConfigSO level) && level.allowedEnemyTypes != null)
                for (int j = 0; j < level.allowedEnemyTypes.Count; j++)
                    if (level.allowedEnemyTypes[j] != null) result.Add(level.allowedEnemyTypes[j].enemyID);
        return result;
    }

    private HashSet<string> KnownBossIds()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        List<string> levels = LevelIds();
        for (int i = 0; i < levels.Count; i++)
            if (_campaign.TryGetLevel(levels[i], out LevelConfigSO level) && level.bossConfig != null)
                result.Add(level.bossConfig.bossID);
        return result;
    }

    private LevelProgressRecord FindLevel(string id) => FindLevel(_service.Current, id);

    private static LevelProgressRecord FindLevel(CampaignSaveDocument document, string id)
    {
        if (document?.progress?.levelProgress == null) return null;
        for (int i = 0; i < document.progress.levelProgress.Count; i++)
            if (document.progress.levelProgress[i] != null && document.progress.levelProgress[i].levelId == id)
                return document.progress.levelProgress[i];
        return null;
    }

    private static TutorialProgressRecord FindTutorial(CampaignSaveDocument document, string id)
    {
        for (int i = 0; i < document.progress.tutorialProgress.Count; i++)
            if (document.progress.tutorialProgress[i].levelId == id) return document.progress.tutorialProgress[i];
        return null;
    }
}
