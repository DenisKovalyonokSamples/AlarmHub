using AlarmHub.Processing.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Processing.Infrastructure;

/// <summary>
/// EF Core context of the processing service: incidents, the inbox and the outbox.
/// </summary>
/// <remarks>
/// The context is intentionally small. Processing derives everything it needs from integration
/// events and the API's internal directory endpoint, so it never reaches into another service's tables.
/// </remarks>
public sealed class ProcessingDbContext(DbContextOptions<ProcessingDbContext> options) : DbContext(options)
{
    /// <summary>Incidents opened by the rules engine.</summary>
    public DbSet<Incident> Incidents => Set<Incident>();

    /// <summary>Inbox used for idempotent consumption.</summary>
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    /// <summary>Integration events waiting to be published.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.ToTable("incidents");
            entity.HasKey(incident => incident.Id);

            entity.Property(incident => incident.Title).HasMaxLength(200).IsRequired();
            entity.Property(incident => incident.Severity).HasConversion<string>().HasMaxLength(16);
            entity.Property(incident => incident.Status).HasConversion<string>().HasMaxLength(16);

            // Operators open the console by site and by "still open", so the index matches that query.
            entity.HasIndex(incident => new { incident.SiteId, incident.Status });
            entity.HasIndex(incident => incident.TriggerEventId);
        });

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.ToTable("processed_events");
            entity.HasKey(processed => processed.EventId);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Type).HasMaxLength(200).IsRequired();
            entity.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
            entity.Property(message => message.LastError).HasMaxLength(2000);
            entity.HasIndex(message => message.PublishedAt);
        });
    }
}
