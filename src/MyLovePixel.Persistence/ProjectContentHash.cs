using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace MyLovePixel.Persistence;

internal static class ProjectContentHash
{
    private const string Prefix = "sha256:";

    public static string Compute(IEnumerable<KeyValuePair<string, byte[]>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        using var aggregate = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        Span<byte> entryHash = stackalloc byte[32];

        foreach (var pair in entries.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var nameBytes = Encoding.UTF8.GetBytes(pair.Key);
            BinaryPrimitives.WriteInt32LittleEndian(length, nameBytes.Length);
            aggregate.AppendData(length);
            aggregate.AppendData(nameBytes);

            SHA256.HashData(pair.Value, entryHash);
            aggregate.AppendData(entryHash);
        }

        return Prefix + Convert.ToHexString(aggregate.GetHashAndReset()).ToLowerInvariant();
    }

    public static bool Matches(string expected, IEnumerable<KeyValuePair<string, byte[]>> entries)
    {
        if (string.IsNullOrWhiteSpace(expected)) return false;
        var actual = Compute(entries);
        var expectedBytes = Encoding.ASCII.GetBytes(expected.Trim().ToLowerInvariant());
        var actualBytes = Encoding.ASCII.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
