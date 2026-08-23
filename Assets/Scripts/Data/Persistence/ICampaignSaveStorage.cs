using System;
using System.Collections.Generic;
using System.IO;

public enum CampaignSaveFileRole
{
    Primary,
    Temporary,
    Backup,
    LegacyArchive,
    PendingOutcome,
    PendingOutcomeTemporary,
}

public interface ICampaignSaveStorage
{
    bool Exists(CampaignSaveFileRole role);
    string ReadAllText(CampaignSaveFileRole role);
    void WriteAllTextFlushed(CampaignSaveFileRole role, string contents);
    void Copy(CampaignSaveFileRole source, CampaignSaveFileRole destination, bool overwrite);
    void Delete(CampaignSaveFileRole role);
    void PromoteTemporaryToPrimary();
    void PromotePendingOutcomeTemporary();
    void RestoreBackupToPrimary();
    string Quarantine(CampaignSaveFileRole role, string reason, DateTime utcNow);
}

public sealed class CampaignSaveFileStorage : ICampaignSaveStorage
{
    private readonly string _root;

    public CampaignSaveFileStorage() : this(UnityEngine.Application.persistentDataPath)
    {
    }

    public CampaignSaveFileStorage(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("A storage root is required.", nameof(root));
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

#if UNITY_EDITOR
    public static StorageFaultPoint EditorFailNextAt { get; set; }
#endif

    public bool Exists(CampaignSaveFileRole role) => File.Exists(GetPath(role));

    public string ReadAllText(CampaignSaveFileRole role)
    {
#if UNITY_EDITOR
        if (role == CampaignSaveFileRole.Primary)
            ThrowEditorFaultIf(StorageFaultPoint.PublishedReadBack);
#endif
        return File.ReadAllText(GetPath(role));
    }

    public void WriteAllTextFlushed(CampaignSaveFileRole role, string contents)
    {
#if UNITY_EDITOR
        if (role == CampaignSaveFileRole.PendingOutcomeTemporary)
            ThrowEditorFaultIf(StorageFaultPoint.JournalTemporaryWrite);
#endif
        string path = GetPath(role);
        Directory.CreateDirectory(_root);
        using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        using (StreamWriter writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
        {
            writer.Write(contents ?? string.Empty);
            writer.Flush();
            stream.Flush(true);
        }
    }

    public void Copy(CampaignSaveFileRole source, CampaignSaveFileRole destination, bool overwrite)
    {
        File.Copy(GetPath(source), GetPath(destination), overwrite);
    }

    public void Delete(CampaignSaveFileRole role)
    {
#if UNITY_EDITOR
        if (role == CampaignSaveFileRole.PendingOutcome || role == CampaignSaveFileRole.PendingOutcomeTemporary)
            ThrowEditorFaultIf(StorageFaultPoint.JournalDelete);
#endif
        string path = GetPath(role);
        if (File.Exists(path))
            File.Delete(path);
    }

    public void PromoteTemporaryToPrimary()
    {
#if UNITY_EDITOR
        ThrowEditorFaultIf(StorageFaultPoint.PromoteTemporary);
#endif
        string temporary = GetPath(CampaignSaveFileRole.Temporary);
        string primary = GetPath(CampaignSaveFileRole.Primary);
        if (File.Exists(primary))
            File.Replace(temporary, primary, null);
        else
            File.Move(temporary, primary);
    }

    public void PromotePendingOutcomeTemporary()
    {
#if UNITY_EDITOR
        ThrowEditorFaultIf(StorageFaultPoint.PromoteJournal);
#endif
        string temporary = GetPath(CampaignSaveFileRole.PendingOutcomeTemporary);
        string pending = GetPath(CampaignSaveFileRole.PendingOutcome);
        if (File.Exists(pending))
            File.Replace(temporary, pending, null);
        else
            File.Move(temporary, pending);
    }

    public void RestoreBackupToPrimary()
    {
#if UNITY_EDITOR
        ThrowEditorFaultIf(StorageFaultPoint.RestoreBackup);
#endif
        string backup = GetPath(CampaignSaveFileRole.Backup);
        string primary = GetPath(CampaignSaveFileRole.Primary);
        File.Copy(backup, primary, true);
    }

    public string Quarantine(CampaignSaveFileRole role, string reason, DateTime utcNow)
    {
        string source = GetPath(role);
        if (!File.Exists(source))
            return null;

        string token = NormalizeToken(reason);
        string timestamp = utcNow.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'");
        string destination = Path.Combine(_root,
            Path.GetFileNameWithoutExtension(source) + "." + timestamp + "." + token + ".quarantine" + Path.GetExtension(source));
        int suffix = 1;
        while (File.Exists(destination))
        {
            destination = Path.Combine(_root,
                Path.GetFileNameWithoutExtension(source) + "." + timestamp + "." + token + "." + suffix + ".quarantine" + Path.GetExtension(source));
            suffix++;
        }
        File.Move(source, destination);
        return destination;
    }

    private string GetPath(CampaignSaveFileRole role)
    {
        string filename;
        switch (role)
        {
            case CampaignSaveFileRole.Primary: filename = "campaign-save.json"; break;
            case CampaignSaveFileRole.Temporary: filename = "campaign-save.tmp"; break;
            case CampaignSaveFileRole.Backup: filename = "campaign-save.bak"; break;
            case CampaignSaveFileRole.LegacyArchive: filename = "legacy-progress-v0.json"; break;
            case CampaignSaveFileRole.PendingOutcome: filename = "campaign-outcome.pending.json"; break;
            case CampaignSaveFileRole.PendingOutcomeTemporary: filename = "campaign-outcome.pending.tmp"; break;
            default: throw new ArgumentOutOfRangeException(nameof(role));
        }
        return Path.Combine(_root, filename);
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        var chars = value.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-') chars[i] = '-';
        return new string(chars);
    }

#if UNITY_EDITOR
    private static void ThrowEditorFaultIf(StorageFaultPoint point)
    {
        if (EditorFailNextAt != point) return;
        EditorFailNextAt = StorageFaultPoint.None;
        throw new IOException("Injected one-shot Editor storage failure at " + point + ".");
    }
#endif
}

public enum StorageFaultPoint
{
    None,
    Read,
    TemporaryWrite,
    ArchiveWrite,
    PrimaryBackup,
    PromoteTemporary,
    Quarantine,
    JournalTemporaryWrite,
    PromoteJournal,
    JournalDelete,
    PublishedReadBack,
    RestoreBackup,
}

public sealed class InMemoryCampaignSaveStorage : ICampaignSaveStorage
{
    private readonly Dictionary<CampaignSaveFileRole, string> _files =
        new Dictionary<CampaignSaveFileRole, string>();
    public StorageFaultPoint FailAt { get; set; }
    public List<CampaignSaveFileRole> QuarantinedRoles { get; } = new List<CampaignSaveFileRole>();
    public Dictionary<string, string> QuarantinedContents { get; } = new Dictionary<string, string>();

    public bool Exists(CampaignSaveFileRole role) => _files.ContainsKey(role);

    public string ReadAllText(CampaignSaveFileRole role)
    {
        ThrowIf(StorageFaultPoint.Read);
        if (role == CampaignSaveFileRole.Primary)
            ThrowIf(StorageFaultPoint.PublishedReadBack);
        return _files[role];
    }

    public void WriteAllTextFlushed(CampaignSaveFileRole role, string contents)
    {
        if (role == CampaignSaveFileRole.Temporary) ThrowIf(StorageFaultPoint.TemporaryWrite);
        if (role == CampaignSaveFileRole.PendingOutcomeTemporary) ThrowIf(StorageFaultPoint.JournalTemporaryWrite);
        if (role == CampaignSaveFileRole.LegacyArchive) ThrowIf(StorageFaultPoint.ArchiveWrite);
        _files[role] = contents;
    }

    public void Copy(CampaignSaveFileRole source, CampaignSaveFileRole destination, bool overwrite)
    {
        ThrowIf(StorageFaultPoint.PrimaryBackup);
        if (!overwrite && Exists(destination)) throw new IOException("Destination exists.");
        _files[destination] = _files[source];
    }

    public void Delete(CampaignSaveFileRole role)
    {
        ThrowIf(StorageFaultPoint.JournalDelete);
        _files.Remove(role);
    }

    public void PromoteTemporaryToPrimary()
    {
        ThrowIf(StorageFaultPoint.PromoteTemporary);
        _files[CampaignSaveFileRole.Primary] = _files[CampaignSaveFileRole.Temporary];
        _files.Remove(CampaignSaveFileRole.Temporary);
    }

    public void PromotePendingOutcomeTemporary()
    {
        ThrowIf(StorageFaultPoint.PromoteJournal);
        _files[CampaignSaveFileRole.PendingOutcome] = _files[CampaignSaveFileRole.PendingOutcomeTemporary];
        _files.Remove(CampaignSaveFileRole.PendingOutcomeTemporary);
    }

    public void RestoreBackupToPrimary()
    {
        ThrowIf(StorageFaultPoint.RestoreBackup);
        _files[CampaignSaveFileRole.Primary] = _files[CampaignSaveFileRole.Backup];
    }

    public string Quarantine(CampaignSaveFileRole role, string reason, DateTime utcNow)
    {
        ThrowIf(StorageFaultPoint.Quarantine);
        if (!Exists(role)) return null;
        QuarantinedRoles.Add(role);
        string result = role + "." + utcNow.ToUniversalTime().ToString("yyyyMMddHHmmssfff") + ".quarantine";
        QuarantinedContents[result] = _files[role];
        _files.Remove(role);
        return result;
    }

    public void Set(CampaignSaveFileRole role, string contents) => _files[role] = contents;

    private void ThrowIf(StorageFaultPoint point)
    {
        if (FailAt != point) return;
        FailAt = StorageFaultPoint.None;
        throw new IOException("Injected storage failure at " + point + ".");
    }
}
