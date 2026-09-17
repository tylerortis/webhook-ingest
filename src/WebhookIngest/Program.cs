using WebhookIngest.Data;
using WebhookIngest.Ingestion;
using WebhookIngest.Metrics;
using WebhookIngest.Signatures;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
}

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<WebhookSigningOptions>()
    .Bind(builder.Configuration.GetSection(WebhookSigningOptions.SectionName))
    .Validate(o => WebhookSigningOptions.IsValidSecret(o.SigningSecret),
        "WebhookSigning:SigningSecret must be a base64 secret, optionally prefixed with whsec_.")
    .Validate(o => o.ToleranceSeconds is > 0 and <= 3600,
        "WebhookSigning:ToleranceSeconds must be between 1 and 3600.")
    .ValidateOnStart();

builder.Services.AddIngestDatabase();
builder.Services.AddSingleton<SvixSignatureVerifier>();
builder.Services.AddSingleton<IMetricsQueue, MetricsQueue>();
builder.Services.AddScoped<EventIngestor>();

builder.Services.AddHealthChecks().AddDbContextCheck<IngestDbContext>();
builder.Services.AddOpenApi();

var app = builder.Build();

// Demo-friendly schema bootstrap. See README "Design decisions" for why this isn't migrations.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<IngestDbContext>().Database.EnsureCreatedAsync();
}

app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapWebhookEndpoints();

app.Run();
