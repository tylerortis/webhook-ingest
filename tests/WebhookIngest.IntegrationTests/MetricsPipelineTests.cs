using System.Net;
using System.Net.Http.Json;

namespace WebhookIngest.IntegrationTests;

public class MetricsPipelineTests(IngestApiFactory factory) : IClassFixture<IngestApiFactory>
{
    private sealed record Metrics(
        string CampaignId, int Sent, int Delivered, int UniqueOpens, int TotalOpens, int UniqueClicks,
        int TotalClicks, int Bounced, int Complained, int Unsubscribed, DateTimeOffset? LastEventAt);

    private static async Task<Metrics?> GetMetricsWhen(HttpClient client, string campaignId, Func<Metrics, bool> condition)
    {
        var response = await client.GetAsync($"/campaigns/{campaignId}/metrics");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        var metrics = await response.Content.ReadFromJsonAsync<Metrics>();
        return metrics is not null && condition(metrics) ? metrics : null;
    }

    [Fact]
    public async Task Signed_events_end_to_end_update_campaign_metrics()
    {
        var client = factory.CreateClient();
        const string campaign = "cmp_pipeline_end_to_end";
        // Whole seconds, because the payload's created_at has second precision.
        var t0 = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds());

        // Deliberately out of order: the open arrives before the delivery.
        (string Type, string Recipient, int Minute)[] events =
        [
            ("email.opened", "ada@example.com", 5),
            ("email.sent", "ada@example.com", 0),
            ("email.sent", "grace@example.com", 0),
            ("email.delivered", "ada@example.com", 1),
            ("email.opened", "ada@example.com", 9),
            ("email.clicked", "ada@example.com", 6),
            ("email.bounced", "grace@example.com", 1),
        ];

        foreach (var (type, recipient, minute) in events)
        {
            var json = WebhookClient.EventJson(type, campaign, recipient, t0.AddMinutes(minute));
            var response = await WebhookClient.SendAsync(client, $"msg_{Guid.NewGuid():N}", json);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var metrics = await Eventually.GetAsync(() => GetMetricsWhen(client, campaign, m => m.TotalOpens == 2 && m.Bounced == 1 && m.UniqueClicks == 1));

        Assert.Equal(new Metrics(campaign, Sent: 2, Delivered: 1, UniqueOpens: 1, TotalOpens: 2, UniqueClicks: 1,
            TotalClicks: 1, Bounced: 1, Complained: 0, Unsubscribed: 0, LastEventAt: t0.AddMinutes(9)), metrics);
    }

    [Fact]
    public async Task Duplicate_delivery_does_not_change_metrics()
    {
        var client = factory.CreateClient();
        const string campaign = "cmp_pipeline_duplicate";
        var id = $"msg_{Guid.NewGuid():N}";
        var json = WebhookClient.EventJson("email.clicked", campaign, "linus@example.com", DateTimeOffset.UtcNow);

        await WebhookClient.SendAsync(client, id, json);
        await WebhookClient.SendAsync(client, id, json);
        await WebhookClient.SendAsync(client, id, json);

        var metrics = await Eventually.GetAsync(() => GetMetricsWhen(client, campaign, m => m.TotalClicks > 0));
        Assert.Equal(1, metrics.TotalClicks);
        Assert.Equal(1, metrics.UniqueClicks);
    }

    [Fact]
    public async Task Unknown_campaign_returns_404()
    {
        var response = await factory.CreateClient().GetAsync("/campaigns/cmp_does_not_exist/metrics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
