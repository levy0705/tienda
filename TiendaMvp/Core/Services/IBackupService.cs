using TiendaMvp.Core.Entities;

namespace TiendaMvp.Core.Services;

public interface IBackupService
{
    IReadOnlyList<BackupInfo> GetBackups();
    Task<BackupInfo> CreateBackupAsync(string administratorPin, bool preventive = false, CancellationToken cancellationToken = default);
    Task<BackupValidationResult> ValidateAsync(string filePath, string administratorPin, CancellationToken cancellationToken = default);
    Task<BackupRestoreResult> RestoreAsync(string filePath, string administratorPin, CancellationToken cancellationToken = default);
}
