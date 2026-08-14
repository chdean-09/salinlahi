using System;

public sealed class CandidateInspection
{
    public CampaignSaveFileRole Role { get; set; }
    public bool Exists { get; set; }
    public CampaignSaveDocument Document { get; set; }
    public CampaignSaveFailureCode FailureCode { get; set; }
    public string ReasonCode { get; set; }

    public static CandidateInspection Missing(CampaignSaveFileRole role) => new CandidateInspection
    {
        Role = role,
        Exists = false,
        FailureCode = CampaignSaveFailureCode.Missing,
    };
}

public enum RecoveryDecisionKind
{
    UsePrimary,
    PromoteTemporary,
    RestoreBackup,
    NoRevisedSave,
    CorruptRevisedData,
    Blocked,
}

public sealed class RecoveryDecision
{
    public RecoveryDecisionKind Kind { get; private set; }
    public CampaignSaveFileRole Role { get; private set; }
    public string ReasonCode { get; private set; }

    public static RecoveryDecision Create(RecoveryDecisionKind kind, CampaignSaveFileRole role, string reason)
    {
        return new RecoveryDecision { Kind = kind, Role = role, ReasonCode = reason };
    }
}

public static class CampaignSaveRecoveryResolver
{
    public static RecoveryDecision Resolve(
        CandidateInspection primary,
        CandidateInspection temporary,
        CandidateInspection backup)
    {
        CandidateInspection[] candidates = { primary, temporary, backup };
        for (int i = 0; i < candidates.Length; i++)
        {
            CandidateInspection candidate = candidates[i];
            if (candidate != null && candidate.Exists && IsBlocking(candidate.FailureCode))
                return RecoveryDecision.Create(RecoveryDecisionKind.Blocked, candidate.Role, Reason(candidate));
        }

        bool primaryValid = IsValid(primary);
        bool temporaryValid = IsValid(temporary);
        if (primaryValid && temporaryValid)
        {
            if (temporary.Document.revision > primary.Document.revision)
                return RecoveryDecision.Create(RecoveryDecisionKind.PromoteTemporary,
                    CampaignSaveFileRole.Temporary, "newer-temporary");
            return RecoveryDecision.Create(RecoveryDecisionKind.UsePrimary,
                CampaignSaveFileRole.Primary, "primary-wins");
        }
        if (primaryValid)
            return RecoveryDecision.Create(RecoveryDecisionKind.UsePrimary,
                CampaignSaveFileRole.Primary, "valid-primary");
        if (temporaryValid)
            return RecoveryDecision.Create(RecoveryDecisionKind.PromoteTemporary,
                CampaignSaveFileRole.Temporary, "temporary-only-valid");
        if (IsValid(backup))
            return RecoveryDecision.Create(RecoveryDecisionKind.RestoreBackup,
                CampaignSaveFileRole.Backup, "backup-only-valid");

        bool anyEvidence = HasExistingEvidence(candidates);
        return RecoveryDecision.Create(
            anyEvidence ? RecoveryDecisionKind.CorruptRevisedData : RecoveryDecisionKind.NoRevisedSave,
            CampaignSaveFileRole.Primary,
            anyEvidence ? "corrupt-revised-data" : "no-revised-save");
    }

    private static bool IsValid(CandidateInspection candidate)
    {
        return candidate != null && candidate.Exists && candidate.FailureCode == CampaignSaveFailureCode.None &&
            candidate.Document != null;
    }

    private static bool HasExistingEvidence(CandidateInspection[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
            if (candidates[i] != null && candidates[i].Exists) return true;
        return false;
    }

    private static bool IsBlocking(CampaignSaveFailureCode code)
    {
        return code == CampaignSaveFailureCode.UnsupportedSchema ||
            code == CampaignSaveFailureCode.WrongIdentity ||
            code == CampaignSaveFailureCode.InvalidCampaign ||
            code == CampaignSaveFailureCode.IoFailure;
    }

    private static string Reason(CandidateInspection candidate)
    {
        return string.IsNullOrEmpty(candidate.ReasonCode) ? candidate.FailureCode.ToString() : candidate.ReasonCode;
    }
}
