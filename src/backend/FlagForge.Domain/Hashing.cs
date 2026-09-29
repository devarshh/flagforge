using System.Security.Cryptography;
using System.Text;

namespace FlagForge.Domain;

public static class Hashing
{
    /// <summary>Lowercase hex SHA-256 of the UTF-8 bytes, used for SDK keys and refresh tokens at rest.</summary>
    public static string Sha256Hex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
