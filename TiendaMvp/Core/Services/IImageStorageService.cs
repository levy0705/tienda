namespace TiendaMvp.Core.Services;

public interface IImageStorageService
{
    Task<string?> SaveAsync(FileResult? file, CancellationToken cancellationToken = default);
    Task DeleteAsync(string? relativePath);
    string? GetAbsolutePath(string? relativePath);
}
