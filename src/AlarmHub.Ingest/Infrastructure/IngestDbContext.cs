using AlarmHub.Ingest.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Ingest.Infrastructure;

/// <summary>
/// EF Core context of the ingest service: raw device events plus the transactional outbox.
/// </summary>
/// <remarks>
/// Ingest never reads data owned by other services; it validates panels through the internal API
/// endpoint and publishes integration events, keeping the database boundary intact.
/// </remarks>
public sealed class IngestDbContext(DbContextOptions<IngestDbContext> options) : DbContext(options)
{
    /// <summary>Append-only store of accepted device events (also the idempotency table).</summary>
    public DbSet<DeviceEventRecord> DeviceEvents => Set<DeviceEventRecord>();

    /// <summary>Pending and published integration events.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DeviceEventRecord>(entity =>
        {
            entity.ToTable("device_events");
            entity.HasKey(record => record.EventId);

            entity.Property(record => record.EventType).HasMaxLength(64).IsRequired();
            entity.Property(record => record.PayloadJson).HasColumnType("jsonb").IsRequired();

            // Panels are polled by support and by the processing service during replays.
            entity.HasIndex(record => new { record.PanelId, record.OccurredAt });
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(message => message.Id);

            entity.Property(message => message.Type).HasMaxLength(200).IsRequired();
            entity.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
            entity.Property(message => message.LastError).HasMaxLength(2000);

            // The dispatcher always reads the oldest pending rows, so the index matches that access path.
            entity.HasIndex(message => message.PublishedAt);
        });
    }
}
