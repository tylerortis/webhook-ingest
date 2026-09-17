using System.Globalization;
using System.Text;
using WebhookIngest.Signatures;

namespace WebhookIngest.IntegrationTests;

internal static class WebhookClient
{
    public static string EventJson(string type, string campaignId, string recipient, DateTimeOffset occurredAt) =>
        $$$"""{"type":"{{{type}}}","created_at":"{{{occurredAt.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}}}","data":{"campaign_id":"{{{campaignId}}}","recipient":"{{{recipient}}}"}}""";

    public static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string messageId, string json, DateTimeOffset? signedAt = null, string? secret = null)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var timestamp = (signedAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var key = WebhookSigningOptions.DecodeSecret(secret ?? IngestApiFactory.SigningSecret);
        var signature = Convert.ToBase64String(SvixSignatureVerifier.ComputeSignature(key, messageId, timestamp, body));

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/email")
        {
            Content = new ByteArrayContent(body) { Headers = { { "Content-Type", "application/json" } } },
        };
        request.Headers.Add("svix-id", messageId);
        request.Headers.Add("svix-timestamp", timestamp);
        request.Headers.Add("svix-signature", $"v1,{signature}");
        return client.SendAsync(request);
    }
}
