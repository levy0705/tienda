using System.IO.Compression;
using System.Text;
using ZXing;
using ZXing.QrCode;
using ZXing.Rendering;

namespace TiendaMvp.Core.Services;

public sealed class QrCodeService : IQrCodeService
{
    private const string QrFolder = "product-qrs";

    public string CreateValue(string productId) =>
        $"https://tienda.local/producto/{Uri.EscapeDataString(productId)}";

    public Task<string> GenerateImageAsync(string value, string fileKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);

        var directory = Path.Combine(FileSystem.AppDataDirectory, QrFolder);
        Directory.CreateDirectory(directory);
        var safeKey = string.Concat(fileKey.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_'));
        if (string.IsNullOrWhiteSpace(safeKey))
            safeKey = Guid.NewGuid().ToString("N");
        var path = Path.Combine(directory, $"{safeKey}.png");

        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Width = 640,
                Height = 640,
                // La zona blanca de cuatro módulos es la recomendada por el
                // estándar QR y ayuda a que otros teléfonos lo detecten.
                Margin = 4,
                PureBarcode = true
            }
        };
        var image = writer.Write(value);
        cancellationToken.ThrowIfCancellationRequested();
        File.WriteAllBytes(path, EncodePng(image));
        return Task.FromResult(path);
    }

    private static byte[] EncodePng(PixelData pixelData)
    {
        var rowSize = pixelData.Width * 4;
        var raw = new byte[(rowSize + 1) * pixelData.Height];

        for (var y = 0; y < pixelData.Height; y++)
        {
            var rowOffset = y * (rowSize + 1);
            raw[rowOffset] = 0; // filtro PNG: sin filtro
            for (var x = 0; x < pixelData.Width; x++)
            {
                var sourceOffset = (y * pixelData.Width + x) * 4;
                var targetOffset = rowOffset + 1 + x * 4;

                // PixelData usa BGRA; PNG se escribe como RGBA.
                raw[targetOffset] = pixelData.Pixels[sourceOffset + 2];
                raw[targetOffset + 1] = pixelData.Pixels[sourceOffset + 1];
                raw[targetOffset + 2] = pixelData.Pixels[sourceOffset];
                raw[targetOffset + 3] = pixelData.Pixels[sourceOffset + 3];
            }
        }

        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var header = new byte[13];
        WriteUInt32(header, 0, (uint)pixelData.Width);
        WriteUInt32(header, 4, (uint)pixelData.Height);
        header[8] = 8;  // profundidad por canal
        header[9] = 6;  // color RGBA
        header[10] = 0; // compresión
        header[11] = 0; // filtro
        header[12] = 0; // sin entrelazado
        WriteChunk(png, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(raw);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        WriteUInt32(stream, (uint)data.Length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        var crcInput = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcInput, 0, typeBytes.Length);
        Buffer.BlockCopy(data, 0, crcInput, typeBytes.Length, data.Length);
        WriteUInt32(stream, ComputeCrc32(crcInput));
    }

    private static uint ComputeCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }

        return ~crc;
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
