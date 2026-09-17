using Microsoft.EntityFrameworkCore;

namespace WebhookIngest.Data;

public sealed class IngestDbContext(DbContextOptions<IngestDbContext> options) : DbContext(options)
{
    public DbSet<EmailEvent> Events => Set<EmailEvent>();

    public DbSet<CampaignMetrics> Metrics => Set<CampaignMetrics>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailEvent>(entity =>
        {
            entity.ToTable("email_events");
            entity.Property(e => e.ProviderEventId).HasMaxLength(128);
            entity.Property(e => e.Type).HasMaxLength(64);
            entity.Property(e => e.CampaignId).HasMaxLength(128);
            entity.Property(e => e.Recipient).HasMaxLength(320);
            entity.HasIndex(e => e.ProviderEventId).IsUnique();
            entity.HasIndex(e => e.CampaignId);
        });

        modelBuilder.Entity<CampaignMetrics>(entity =>
        {
            entity.ToTable("campaign_metrics");
            entity.HasKey(m => m.CampaignId);
            entity.Property(m => m.CampaignId).HasMaxLength(128);
        });
    }
}
