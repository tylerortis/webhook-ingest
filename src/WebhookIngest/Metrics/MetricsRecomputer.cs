namespace WebhookIngest.Metrics;

/// <summary>
/// Single consumer of <see cref="IMetricsQueue"/>. Being the only writer of campaign_metrics
/// removes upsert races. On startup it reconciles every campaign, which covers ids that were
/// still in the in-memory queue when the process last stopped.
/// </summary>
public sealed class MetricsRecomputer(
    IMetricsQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<MetricsRecomputer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReconcileAllAsync(stoppingToken);

        await foreach (var batch in queue.ReadBatchesAsync(stoppingToken))
        {
            foreach (var campaignId in batch)
            {
                await RecomputeAsync(campaignId, stoppingToken);
            }
        }
    }

    private async Task ReconcileAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            List<string> campaignIds;
            await using (var scope = scopeFactory.CreateAsyncScope())
            {
                campaignIds = await scope.ServiceProvider.GetRequiredService<CampaignMetricsService>()
                    .CampaignIdsWithEventsAsync(cancellationToken);
            }

            foreach (var campaignId in campaignIds)
            {
                await RecomputeAsync(campaignId, cancellationToken);
            }

            logger.LogInformation("Startup reconciliation recomputed {Count} campaigns", campaignIds.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Startup reconciliation failed; continuing with live events");
        }
    }

    private async Task RecomputeAsync(string campaignId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CampaignMetricsService>().RecomputeAsync(campaignId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The next event for this campaign (or the next restart) will recompute it again.
            logger.LogError(ex, "Failed to recompute metrics for campaign {CampaignId}", campaignId);
        }
    }
}
