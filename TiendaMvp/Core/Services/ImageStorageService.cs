namespace TiendaMvp.Core.Services;

public sealed class ImageStorageService : IImageStorageService
{
    private const string ImagesFolder = "product-images";

    public async Task<string?> SaveAsync(FileResult? file, CancellationToken cancellationToken = default)
    {
        if (file is null)
            return null;

        var directory = Path.Combine(FileSystem.AppDataDirectory, ImagesFolder);
        Directory.CreateDirectory(directory);
        var extension = Path.GetExtension(file.FileName);
        var relativePath = Path.Combine(ImagesFolder, $"{Guid.NewGuid():N}{extension}");
        var absolutePath = Path.Combine(FileSystem.AppDataDirectory, relativePath);

        await using var source = await file.OpenReadAsync();
        await using var destination = File.Create(absolutePath);
        await source.CopyToAsync(destination, cancellationToken);
        return relativePath;
    }

    public Task DeleteAsync(string? relativePath)
    {
        if (!string.IsNullOrWhiteSpace(relativePath))
        {
            var absolutePath = GetAbsolutePath(relativePath);
            if (absolutePath is not null && File.Exists(absolutePath))
                File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    public string? GetAbsolutePath(string? relativePath) =>
        string.IsNullOrWhiteSpace(relativePath)
            ? null
            : Path.Combine(FileSystem.AppDataDirectory, relativePath);
}
