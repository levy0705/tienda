using System.Security.Cryptography;
using System.Text;

namespace TiendaMvp.Core.Services;

public static class PinHasher
{
    public static string Hash(string pin)
    {
        ArgumentException.ThrowIfNullOrEmpty(pin);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(string pin, string hash) =>
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(Hash(pin)),
            Convert.FromHexString(hash));
}
