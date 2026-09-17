namespace WebhookIngest.Signatures;

public sealed class WebhookSigningOptions
{
    public const string SectionName = "WebhookSigning";
    private const string SecretPrefix = "whsec_";

    /// <summary>Base64 signing secret, optionally prefixed with <c>whsec_</c>.</summary>
    public string SigningSecret { get; set; } = "";

    /// <summary>Maximum allowed clock difference between the sender's timestamp and now.</summary>
    public int ToleranceSeconds { get; set; } = 300;

    public static byte[] DecodeSecret(string secret)
    {
        var encoded = secret.StartsWith(SecretPrefix, StringComparison.Ordinal) ? secret[SecretPrefix.Length..] : secret;
        return Convert.FromBase64String(encoded);
    }

    public static bool IsValidSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        try
        {
            return DecodeSecret(secret).Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
