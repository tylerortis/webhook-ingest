namespace WebhookIngest.Data;

public sealed class CampaignMetrics
{
    public required string CampaignId { get; set; }

    public int Sent { get; set; }

    public int Delivered { get; set; }

    public int UniqueOpens { get; set; }

    public int TotalOpens { get; set; }

    public int UniqueClicks { get; set; }

    public int TotalClicks { get; set; }

    public int Bounced { get; set; }

    public int Complained { get; set; }

    public int Unsubscribed { get; set; }

    public DateTimeOffset? LastEventAt { get; set; }

    public DateTimeOffset RecomputedAt { get; set; }
}
