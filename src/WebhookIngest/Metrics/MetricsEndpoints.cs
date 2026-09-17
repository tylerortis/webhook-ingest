using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WebhookIngest.Data;

namespace WebhookIngest.Metrics;

public sealed record CampaignMetricsResponse(
    string CampaignId,
    int Sent,
    int Delivered,
    int UniqueOpens,
    int TotalOpens,
    int UniqueClicks,
    int TotalClicks,
    int Bounced,
    int Complained,
    int Unsubscribed,
    DateTimeOffset? LastEventAt,
    DateTimeOffset RecomputedAt);

public static class MetricsEndpoints
{
    public static IEndpointRouteBuilder MapMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/campaigns/{campaignId}/metrics", async Task<Results<Ok<CampaignMetricsResponse>, NotFound>> (
                string campaignId, IngestDbContext db, CancellationToken cancellationToken) =>
            {
                var m = await db.Metrics.AsNoTracking().FirstOrDefaultAsync(x => x.CampaignId == campaignId, cancellationToken);
                if (m is null)
                {
                    return TypedResults.NotFound();
                }

                return TypedResults.Ok(new CampaignMetricsResponse(
                    m.CampaignId, m.Sent, m.Delivered, m.UniqueOpens, m.TotalOpens, m.UniqueClicks,
                    m.TotalClicks, m.Bounced, m.Complained, m.Unsubscribed, m.LastEventAt, m.RecomputedAt));
            })
            .WithName("GetCampaignMetrics")
            .WithSummary("Current engagement metrics for one campaign.");

        return app;
    }
}
