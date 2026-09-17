namespace WebhookIngest.Metrics;

public sealed record EventFact(string Type, string Recipient, DateTimeOffset OccurredAt);

public sealed record MetricsSnapshot(
    int Sent,
    int Delivered,
    int UniqueOpens,
    int TotalOpens,
    int UniqueClicks,
    int TotalClicks,
    int Bounced,
    int Complained,
    int Unsubscribed,
    DateTimeOffset? LastEventAt);

/// <summary>
/// Derives campaign metrics from the full set of stored events. Because it always starts
/// from scratch, the result is independent of arrival order and repairs itself after a
/// missed or failed update.
/// </summary>
public static class MetricsCalculator
{
    public static MetricsSnapshot Compute(IEnumerable<EventFact> events)
    {
        var facts = events as IReadOnlyCollection<EventFact> ?? events.ToList();

        int Distinct(string type) => facts
            .Where(e => e.Type == type)
            .Select(e => e.Recipient)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        int Total(string type) => facts.Count(e => e.Type == type);

        return new MetricsSnapshot(
            Sent: Distinct(EmailEventTypes.Sent),
            Delivered: Distinct(EmailEventTypes.Delivered),
            UniqueOpens: Distinct(EmailEventTypes.Opened),
            TotalOpens: Total(EmailEventTypes.Opened),
            UniqueClicks: Distinct(EmailEventTypes.Clicked),
            TotalClicks: Total(EmailEventTypes.Clicked),
            Bounced: Distinct(EmailEventTypes.Bounced),
            Complained: Distinct(EmailEventTypes.Complained),
            Unsubscribed: Distinct(EmailEventTypes.Unsubscribed),
            LastEventAt: facts.Count == 0 ? null : facts.Max(e => e.OccurredAt));
    }
}
