using Microsoft.EntityFrameworkCore;

namespace WebhookIngest.Data;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddIngestDatabase(this IServiceCollection services)
    {
        services.AddDbContext<IngestDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("Events")
                ?? throw new InvalidOperationException("ConnectionStrings:Events is not configured.");
            var provider = configuration["Database:Provider"] ?? "Sqlite";

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString);
            }
            else if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported Database:Provider '{provider}'. Use Sqlite or SqlServer.");
            }
        });

        return services;
    }
}
