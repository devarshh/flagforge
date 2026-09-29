using System.Buffers.Text;
using System.Security.Cryptography;

namespace FlagForge.Domain;

/// <summary>Cryptographically random identifiers and secrets.</summary>
public static class Generate
{
    private const string LowerAlphanumeric = "abcdefghijklmnopqrstuvwxyz0123456789";

    // Temporary passwords avoid look-alike characters (0/O, 1/l/I) because people copy them by hand.
    private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    /// <summary>16 lowercase hex characters.</summary>
    public static string Salt() => RandomNumberGenerator.GetHexString(16, lowercase: true);

    /// <summary><c>v_</c> plus 6 lowercase alphanumerics.</summary>
    public static string VariationId() => "v_" + RandomNumberGenerator.GetString(LowerAlphanumeric, 6);

    /// <summary><c>ffk_</c> plus 43 base64url characters (32 random bytes).</summary>
    public static string SdkKey() => SdkKeyFormat.Prefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>32 random bytes, base64url-encoded (43 characters).</summary>
    public static string RefreshToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>16 random characters.</summary>
    public static string TemporaryPassword() => RandomNumberGenerator.GetString(PasswordAlphabet, 16);
}
