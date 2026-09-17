using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebhookIngest.Data;
using WebhookIngest.Metrics;

namespace WebhookIngest.Ingestion;

public enum IngestOutcome
{
    Stored,
    Duplicate,
    Invalid,
}

public sealed class EventIngestor(IngestDbContext db, IMetricsQueue metricsQueue, TimeProvider time, ILogger<EventIngestor> logger)
{
    public async Task<IngestOutcome> IngestAsync(string messageId, byte[] body, CancellationToken cancellationToken)
    {
        if (TryParse(body) is not { Data: { } data } payload)
        {
            return IngestOutcome.Invalid;
        }

        if (await db.Events.AnyAsync(e => e.ProviderEventId == messageId, cancellationToken))
        {
            return IngestOutcome.Duplicate;
        }

        var campaignId = string.IsNullOrWhiteSpace(data.CampaignId) ? null : data.CampaignId;
        db.Events.Add(new EmailEvent
        {
            ProviderEventId = messageId,
            Type = payload.Type!,
            CampaignId = campaignId,
            Recipient = data.Recipient!.Trim().ToLowerInvariant(),
            OccurredAt = payload.CreatedAt!.Value,
            ReceivedAt = time.GetUtcNow(),
            Payload = Encoding.UTF8.GetString(body),
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two deliveries of the same message raced past the AnyAsync check;
            // the unique index let exactly one of them in.
            db.ChangeTracker.Clear();
            if (await db.Events.AnyAsync(e => e.ProviderEventId == messageId, cancellationToken))
            {
                return IngestOutcome.Duplicate;
            }

            throw;
        }

        logger.LogInformation("Stored {EventType} event {MessageId} for campaign {CampaignId}", payload.Type, messageId, campaignId);
        if (campaignId is not null)
        {
            metricsQueue.Enqueue(campaignId);
        }

        return IngestOutcome.Stored;
    }

    private static EmailWebhookPayload? TryParse(byte[] body)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<EmailWebhookPayload>(body);
            var isComplete = payload is { CreatedAt: not null }
                && !string.IsNullOrWhiteSpace(payload.Type)
                && !string.IsNullOrWhiteSpace(payload.Data?.Recipient);
            return isComplete ? payload : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
