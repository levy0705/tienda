namespace TiendaMvp.Core.Entities;

public sealed class BackupInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public long SizeBytes { get; set; }
    public bool IsPreventive { get; set; }

    [SQLite.Ignore]
    public string CreatedLabel => CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    [SQLite.Ignore]
    public string SizeLabel => SizeBytes < 1024 * 1024
        ? $"{Math.Max(1, SizeBytes / 1024d):N0} KB"
        : $"{SizeBytes / (1024d * 1024d):N1} MB";

    [SQLite.Ignore]
    public string TypeLabel => IsPreventive ? "Copia preventiva" : "Copia manual";
}

public sealed class BackupValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public BackupInfo? Backup { get; set; }
    public int ImageCount { get; set; }
    public int SchemaVersion { get; set; }
}

public sealed class BackupRestoreResult
{
    public BackupInfo? PreventiveBackup { get; set; }
    public BackupInfo RestoredBackup { get; set; } = new();
    public int ImageCount { get; set; }
}
