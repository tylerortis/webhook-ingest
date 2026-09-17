namespace WebhookIngest.Data;

public sealed class EmailEvent
{
    public long Id { get; set; }

    /// <summary>The sender's message id (svix-id). Unique: retries of the same delivery reuse it.</summary>
    public required string ProviderEventId { get; set; }

    public required string Type { get; set; }

    public string? CampaignId { get; set; }

    public required string Recipient { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>Raw verified JSON, kept so metrics can be re-derived if the rules change.</summary>
    public required string Payload { get; set; }
}
