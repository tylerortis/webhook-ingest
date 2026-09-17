using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebhookIngest.Data;

namespace WebhookIngest.IntegrationTests;

public class WebhookEndpointTests(IngestApiFactory factory) : IClassFixture<IngestApiFactory>
{
    private static string NewMessageId() => $"msg_{Guid.NewGuid():N}";

    private async Task<int> CountStoredAsync(string messageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IngestDbContext>();
        return await db.Events.CountAsync(e => e.ProviderEventId == messageId);
    }

    [Fact]
    public async Task Signed_event_is_accepted_and_stored()
    {
        var client = factory.CreateClient();
        var id = NewMessageId();
        var json = WebhookClient.EventJson("email.delivered", "cmp_endpoint_stored", "ada@example.com", DateTimeOffset.UtcNow);

        var response = await WebhookClient.SendAsync(client, id, json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("stored", (await response.Content.ReadFromJsonAsync<StatusBody>())!.Status);
        Assert.Equal(1, await CountStoredAsync(id));
    }

    [Fact]
    public async Task Duplicate_delivery_returns_200_and_is_stored_once()
    {
        var client = factory.CreateClient();
        var id = NewMessageId();
        var json = WebhookClient.EventJson("email.opened", "cmp_endpoint_duplicate", "grace@example.com", DateTimeOffset.UtcNow);

        var first = await WebhookClient.SendAsync(client, id, json);
        var second = await WebhookClient.SendAsync(client, id, json);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("duplicate", (await second.Content.ReadFromJsonAsync<StatusBody>())!.Status);
        Assert.Equal(1, await CountStoredAsync(id));
    }

    [Fact]
    public async Task Wrong_secret_returns_401_and_stores_nothing()
    {
        var client = factory.CreateClient();
        var id = NewMessageId();
        var json = WebhookClient.EventJson("email.clicked", "cmp_endpoint_forged", "linus@example.com", DateTimeOffset.UtcNow);
        var forgedSecret = "whsec_" + Convert.ToBase64String(Encoding.UTF8.GetBytes("not-the-configured-secret"));

        var response = await WebhookClient.SendAsync(client, id, json, secret: forgedSecret);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountStoredAsync(id));
    }

    [Fact]
    public async Task Stale_timestamp_returns_401()
    {
        var client = factory.CreateClient();
        var json = WebhookClient.EventJson("email.sent", "cmp_endpoint_stale", "ada@example.com", DateTimeOffset.UtcNow);

        var response = await WebhookClient.SendAsync(client, NewMessageId(), json, signedAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unsigned_request_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/webhooks/email", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"type":"email.sent","data":{"campaign_id":"cmp_x","recipient":"ada@example.com"}}""")]
    [InlineData("""{"type":"email.sent","created_at":"2026-03-02T14:00:00Z","data":{"campaign_id":"cmp_x"}}""")]
    public async Task Signed_but_invalid_payload_returns_400(string json)
    {
        var client = factory.CreateClient();

        var response = await WebhookClient.SendAsync(client, NewMessageId(), json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    private sealed record StatusBody(string Status);
}
