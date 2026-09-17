using Microsoft.AspNetCore.Http.Features;
using WebhookIngest.Signatures;

namespace WebhookIngest.Ingestion;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/email", async (HttpContext http, EventIngestor ingestor, CancellationToken cancellationToken) =>
            {
                var webhook = http.Features.GetRequiredFeature<VerifiedWebhook>();
                var outcome = await ingestor.IngestAsync(webhook.MessageId, webhook.Body, cancellationToken);

                return outcome switch
                {
                    IngestOutcome.Stored => Results.Ok(new { status = "stored" }),
                    IngestOutcome.Duplicate => Results.Ok(new { status = "duplicate" }),
                    _ => Results.BadRequest(new { status = "invalid_payload" }),
                };
            })
            .AddEndpointFilter<SvixSignatureFilter>()
            .Accepts<EmailWebhookPayload>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("ReceiveEmailWebhook")
            .WithSummary("Receive a signed email event (Svix signature headers required).");

        return app;
    }
}
