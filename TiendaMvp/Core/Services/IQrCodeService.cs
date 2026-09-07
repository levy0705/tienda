namespace TiendaMvp.Core.Services;

public interface IQrCodeService
{
    string CreateValue(string productId);
    Task<string> GenerateImageAsync(string value, string fileKey, CancellationToken cancellationToken = default);
}
