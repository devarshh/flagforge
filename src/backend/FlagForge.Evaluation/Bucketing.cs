using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FlagForge.Evaluation;

/// <summary>
/// Deterministic percentage bucketing: <c>BigEndianUInt32(SHA256("{flagKey}.{salt}.{value}")[0..4]) % 100000</c>.
/// SDKs that evaluate locally must reproduce this exactly.
/// </summary>
public static class Bucketing
{
    public const int BucketCount = 100_000;

    private const int StackBufferSize = 256;

    public static int ComputeBucket(string flagKey, string salt, string bucketValue)
    {
        ArgumentNullException.ThrowIfNull(flagKey);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(bucketValue);

        var length = Encoding.UTF8.GetByteCount(flagKey) + Encoding.UTF8.GetByteCount(salt) + Encoding.UTF8.GetByteCount(bucketValue) + 2;
        byte[]? rented = null;
        var buffer = length <= StackBufferSize
            ? stackalloc byte[StackBufferSize]
            : (rented = ArrayPool<byte>.Shared.Rent(length));
        try
        {
            var written = Encoding.UTF8.GetBytes(flagKey, buffer);
            buffer[written++] = (byte)'.';
            written += Encoding.UTF8.GetBytes(salt, buffer[written..]);
            buffer[written++] = (byte)'.';
            written += Encoding.UTF8.GetBytes(bucketValue, buffer[written..]);

            Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(buffer[..written], hash);
            return (int)(BinaryPrimitives.ReadUInt32BigEndian(hash) % BucketCount);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// The string that is hashed for an attribute: strings as-is, numbers in invariant culture without trailing
    /// zeros (so 31 and 31.0 land in the same bucket). Booleans, arrays, and missing values have no bucket value.
    /// </summary>
    public static string? GetBucketValue(AttributeValue? value) => value?.Kind switch
    {
        AttributeValueKind.String => value.StringValue,
        AttributeValueKind.Number => FormatNumber(value.NumberValue),
        _ => null,
    };

    /// <summary>The bucket for a context: 0 when the bucket-by attribute has no bucket value.</summary>
    public static int ComputeBucket(string flagKey, string salt, EvaluationContext context, string bucketBy)
    {
        ArgumentNullException.ThrowIfNull(context);
        var bucketValue = GetBucketValue(context.GetAttribute(bucketBy));
        return bucketValue is null ? 0 : ComputeBucket(flagKey, salt, bucketValue);
    }

    internal static string FormatNumber(decimal value)
    {
        // Dividing by 1 with 28 decimal places yields the same value at its minimal scale (drops trailing zeros).
        var normalized = value / 1.0000000000000000000000000000m;
        return normalized.ToString(CultureInfo.InvariantCulture);
    }
}
