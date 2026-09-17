using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace WebhookIngest.IntegrationTests;

public sealed class IngestApiFactory : WebApplicationFactory<Program>
{
    // Derived at runtime so no secret-looking literal lives in the repo.
    public static readonly string SigningSecret =
        "whsec_" + Convert.ToBase64String(Encoding.UTF8.GetBytes("integration-test-signing-key"));

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"webhook-ingest-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Events", $"Data Source={_databasePath}");
        builder.UseSetting("WebhookSigning:SigningSecret", SigningSecret);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }
}
