using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WebhookIngest.Signatures;

public enum SignatureCheck
{
    Valid,
    MissingHeaders,
    MalformedTimestamp,
    TimestampOutsideTolerance,
    NoMatchingSignature,
}

/// <summary>
/// Verifies Svix-style webhook signatures: base64 HMAC-SHA256 over "{id}.{timestamp}.{body}".
/// </summary>
public sealed class SvixSignatureVerifier
{
    private const string SignatureVersion = "v1,";

    private readonly byte[] _key;
    private readonly long _toleranceSeconds;
    private readonly TimeProvider _time;

    public SvixSignatureVerifier(IOptions<WebhookSigningOptions> options, TimeProvider time)
    {
        _key = WebhookSigningOptions.DecodeSecret(options.Value.SigningSecret);
        _toleranceSeconds = options.Value.ToleranceSeconds;
        _time = time;
    }

    public SignatureCheck Verify(string? messageId, string? timestamp, string? signatureHeader, ReadOnlySpan<byte> body)
    {
        if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(timestamp) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return SignatureCheck.MissingHeaders;
        }

        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var sentAt))
        {
            return SignatureCheck.MalformedTimestamp;
        }

        // Replay protection: reject anything too old or too far in the future.
        var now = _time.GetUtcNow().ToUnixTimeSeconds();
        if (Math.Abs(now - sentAt) > _toleranceSeconds)
        {
            return SignatureCheck.TimestampOutsideTolerance;
        }

        var expected = ComputeSignature(_key, messageId, timestamp, body);
        Span<byte> candidate = stackalloc byte[64];

        // The header can carry several space-separated signatures (e.g. during secret rotation).
        foreach (var entry in signatureHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!entry.StartsWith(SignatureVersion, StringComparison.Ordinal))
            {
                continue;
            }

            if (Convert.TryFromBase64Chars(entry.AsSpan(SignatureVersion.Length), candidate, out var written)
                && CryptographicOperations.FixedTimeEquals(candidate[..written], expected))
            {
                return SignatureCheck.Valid;
            }
        }

        return SignatureCheck.NoMatchingSignature;
    }

    public static byte[] ComputeSignature(byte[] key, string messageId, string timestamp, ReadOnlySpan<byte> body)
    {
        var prefix = Encoding.UTF8.GetBytes($"{messageId}.{timestamp}.");
        var signedContent = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signedContent, 0);
        body.CopyTo(signedContent.AsSpan(prefix.Length));
        return HMACSHA256.HashData(key, signedContent);
    }
}
