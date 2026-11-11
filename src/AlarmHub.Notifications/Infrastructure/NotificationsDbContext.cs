using AlarmHub.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Notifications.Infrastructure;

/// <summary>
/// EF Core context of the notification service.
/// </summary>
/// <remarks>
/// The service only stores delivery records; it never reads incidents or panels from other databases.
/// Everything it needs arrives inside <c>IncidentOpenedV1</c>.
/// </remarks>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    /// <summary>Delivery history.</summary>
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationRecord>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(record => record.Id);

            entity.Property(record => record.Channel).HasMaxLength(32).IsRequired();
            entity.Property(record => record.Recipient).HasMaxLength(320).IsRequired();
            entity.Property(record => record.Subject).HasMaxLength(200).IsRequired();
            entity.Property(record => record.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(record => record.LastError).HasMaxLength(2000);

            // Idempotency: one notification per incident per channel.
            entity.HasIndex(record => new { record.IncidentId, record.Channel }).IsUnique();
        });
    }
}
