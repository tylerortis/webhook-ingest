using Microsoft.EntityFrameworkCore;
using WebhookIngest.Data;

namespace WebhookIngest.Metrics;

public sealed class CampaignMetricsService(IngestDbContext db, TimeProvider time)
{
    public async Task RecomputeAsync(string campaignId, CancellationToken cancellationToken)
    {
        var facts = await db.Events
            .AsNoTracking()
            .Where(e => e.CampaignId == campaignId)
            .Select(e => new EventFact(e.Type, e.Recipient, e.OccurredAt))
            .ToListAsync(cancellationToken);

        var snapshot = MetricsCalculator.Compute(facts);

        var row = await db.Metrics.FindAsync([campaignId], cancellationToken);
        if (row is null)
        {
            row = new CampaignMetrics { CampaignId = campaignId };
            db.Metrics.Add(row);
        }

        row.Sent = snapshot.Sent;
        row.Delivered = snapshot.Delivered;
        row.UniqueOpens = snapshot.UniqueOpens;
        row.TotalOpens = snapshot.TotalOpens;
        row.UniqueClicks = snapshot.UniqueClicks;
        row.TotalClicks = snapshot.TotalClicks;
        row.Bounced = snapshot.Bounced;
        row.Complained = snapshot.Complained;
        row.Unsubscribed = snapshot.Unsubscribed;
        row.LastEventAt = snapshot.LastEventAt;
        row.RecomputedAt = time.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<string>> CampaignIdsWithEventsAsync(CancellationToken cancellationToken) =>
        db.Events
            .Where(e => e.CampaignId != null)
            .Select(e => e.CampaignId!)
            .Distinct()
            .ToListAsync(cancellationToken);
}
