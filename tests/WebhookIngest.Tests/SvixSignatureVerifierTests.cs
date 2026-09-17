using System.Text;
using Microsoft.Extensions.Options;
using WebhookIngest.Signatures;

namespace WebhookIngest.Tests;

public class SvixSignatureVerifierTests
{
    // Public test vector from the Svix webhook verification docs.
    private const string DocsSecret = "whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw"; // gitleaks:allow
    private const string DocsMessageId = "msg_p5jXN8AQM9LWM0D4loKWxJek";
    private const string DocsTimestamp = "1614265330";
    private const string DocsBody = "{\"test\": 2432232314}";
    private const string DocsSignature = "g0hM9SsE+OTPJTGt/tmIKtSyZlE3uFJELVlNIOLJ1OE=";

    private static readonly DateTimeOffset DocsTime = DateTimeOffset.FromUnixTimeSeconds(1614265330);

    private static SvixSignatureVerifier CreateVerifier(string secret, DateTimeOffset now, int toleranceSeconds = 300) =>
        new(Options.Create(new WebhookSigningOptions { SigningSecret = secret, ToleranceSeconds = toleranceSeconds }), new FixedTimeProvider(now));

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static string SignatureHeaderFor(string secret, string id, string timestamp, string body) =>
        "v1," + Convert.ToBase64String(SvixSignatureVerifier.ComputeSignature(WebhookSigningOptions.DecodeSecret(secret), id, timestamp, Utf8(body)));

    [Fact]
    public void ComputeSignature_matches_the_published_svix_test_vector()
    {
        var signature = SvixSignatureVerifier.ComputeSignature(
            WebhookSigningOptions.DecodeSecret(DocsSecret), DocsMessageId, DocsTimestamp, Utf8(DocsBody));

        Assert.Equal(DocsSignature, Convert.ToBase64String(signature));
    }

    [Fact]
    public void Accepts_a_valid_signature()
    {
        var verifier = CreateVerifier(DocsSecret, DocsTime);

        var result = verifier.Verify(DocsMessageId, DocsTimestamp, "v1," + DocsSignature, Utf8(DocsBody));

        Assert.Equal(SignatureCheck.Valid, result);
    }

    [Fact]
    public void Rejects_a_tampered_body()
    {
        var verifier = CreateVerifier(DocsSecret, DocsTime);

        var result = verifier.Verify(DocsMessageId, DocsTimestamp, "v1," + DocsSignature, Utf8("{\"test\": 9}"));

        Assert.Equal(SignatureCheck.NoMatchingSignature, result);
    }

    [Fact]
    public void Rejects_a_signature_made_with_a_different_secret()
    {
        var otherSecret = "whsec_" + Convert.ToBase64String(Utf8("a-different-signing-key-for-tests"));
        var verifier = CreateVerifier(DocsSecret, DocsTime);

        var header = SignatureHeaderFor(otherSecret, DocsMessageId, DocsTimestamp, DocsBody);

        Assert.Equal(SignatureCheck.NoMatchingSignature, verifier.Verify(DocsMessageId, DocsTimestamp, header, Utf8(DocsBody)));
    }

    [Fact]
    public void Rejects_a_signature_bound_to_a_different_message_id()
    {
        var verifier = CreateVerifier(DocsSecret, DocsTime);

        var result = verifier.Verify("msg_someone_else", DocsTimestamp, "v1," + DocsSignature, Utf8(DocsBody));

        Assert.Equal(SignatureCheck.NoMatchingSignature, result);
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    public void Rejects_timestamps_outside_the_tolerance_window(int offsetSeconds)
    {
        var verifier = CreateVerifier(DocsSecret, DocsTime.AddSeconds(-offsetSeconds));

        var result = verifier.Verify(DocsMessageId, DocsTimestamp, "v1," + DocsSignature, Utf8(DocsBody));

        Assert.Equal(SignatureCheck.TimestampOutsideTolerance, result);
    }

    [Theory]
    [InlineData(-300)]
    [InlineData(300)]
    public void Accepts_timestamps_at_the_edge_of_the_tolerance_window(int offsetSeconds)
    {
        var verifier = CreateVerifier(DocsSecret, DocsTime.AddSeconds(offsetSeconds));

        var result = verifier.Verify(DocsMessageId, DocsTimestamp, "v1," + DocsSignature, Utf8(DocsBody));

        Assert.Equal(SignatureCheck.Valid, result);
    }

    [Fact]
    public void Accepts_when_any_of_several_signatures_matches()
    {
        var verifier = CreateVerifier(DocsSecret, DocsTime);
        var header = $"v1,bm90LWEtcmVhbC1zaWduYXR1cmU= v1,{DocsSignature}";

        Assert.Equal(SignatureCheck.Valid, verifier.Verify(DocsMessageId, DocsTimestamp, header, Utf8(DocsBody)));
    }

    [Theory]
    [InlineData("v2," + DocsSignature)]
    [InlineData(DocsSignature)]
    [InlineData("v1,not base64!!")]
    [InlineData("v1,")]
    public void Rejects_signature_headers_without_a_valid_v1_entry(string header)
    {
        var verifier = CreateVerifier(DocsSecret, DocsTime);

        Assert.Equal(SignatureCheck.NoMatchingSignature, verifier.Verify(DocsMessageId, DocsTimestamp, header, Utf8(DocsBody)));
    }

    [Theory]
    [InlineData(null, DocsTimestamp, "v1,x")]
    [InlineData(DocsMessageId, null, "v1,x")]
    [InlineData(DocsMessageId, DocsTimestamp, null)]
    [InlineData("", DocsTimestamp, "v1,x")]
    [InlineData(DocsMessageId, " ", "v1,x")]
    public void Rejects_missing_headers(string? id, string? timestamp, string? signature)
    {
        var verifier = CreateVerifier(DocsSecret, DocsTime);

        Assert.Equal(SignatureCheck.MissingHeaders, verifier.Verify(id, timestamp, signature, Utf8(DocsBody)));
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("-1614265330")]
    [InlineData("1614265330.5")]
    public void Rejects_malformed_timestamps(string timestamp)
    {
        var verifier = CreateVerifier(DocsSecret, DocsTime);

        Assert.Equal(SignatureCheck.MalformedTimestamp, verifier.Verify(DocsMessageId, timestamp, "v1," + DocsSignature, Utf8(DocsBody)));
    }

    [Theory]
    [InlineData(DocsSecret, true)]
    [InlineData("MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw", true)]
    [InlineData("", false)]
    [InlineData("whsec_", false)]
    [InlineData("whsec_***not-base64***", false)]
    public void IsValidSecret_requires_non_empty_base64(string secret, bool expected)
    {
        Assert.Equal(expected, WebhookSigningOptions.IsValidSecret(secret));
    }
}
