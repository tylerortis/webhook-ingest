using System.Text.Json.Serialization;

namespace WebhookIngest.Ingestion;

public sealed record EmailWebhookPayload(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("data")] EmailWebhookData? Data);

public sealed record EmailWebhookData(
    [property: JsonPropertyName("campaign_id")] string? CampaignId,
    [property: JsonPropertyName("recipient")] string? Recipient);
