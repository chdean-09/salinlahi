using System;

/// <summary>
/// Read-only learning projection over the revised campaign save. Consumers receive snapshots and
/// never reach into the save document directly.
/// </summary>
public sealed class LearningProgressRepository
{
    private readonly CampaignSaveService _service;
    private readonly CampaignConfigSO _campaign;

    public LearningProgressRepository(CampaignSaveService service, CampaignConfigSO campaign)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
    }

    public LearningStateSnapshot Snapshot
    {
        get
        {
            CampaignProgressData progress = _service.Current?.progress ?? new CampaignProgressData();
            return new LearningStateSnapshot(progress, _campaign);
        }
    }
}
