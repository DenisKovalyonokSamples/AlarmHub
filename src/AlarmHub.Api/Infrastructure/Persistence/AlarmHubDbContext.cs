using AlarmHub.Api.Domain.Customers;
using AlarmHub.Api.Domain.Panels;
using AlarmHub.Api.Domain.Sensors;
using AlarmHub.Api.Domain.Sites;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Api.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the API service. Owns customers, sites, panels and sensors.
/// </summary>
/// <remarks>
/// Each microservice keeps its own database, so this context never references tables owned by
/// the ingest, processing or notification services. Cross-service data travels as integration
/// events from <c>AlarmHub.Contracts</c>, never as a shared database.
/// </remarks>
public sealed class AlarmHubDbContext(DbContextOptions<AlarmHubDbContext> options) : DbContext(options)
{
    /// <summary>Customers registered on the platform.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>Protected sites owned by customers.</summary>
    public DbSet<Site> Sites => Set<Site>();

    /// <summary>Alarm panels installed at sites.</summary>
    public DbSet<AlarmPanel> Panels => Set<AlarmPanel>();

    /// <summary>Sensors wired to panels.</summary>
    public DbSet<Sensor> Sensors => Set<Sensor>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(customer => customer.Id);
            entity.Property(customer => customer.Name).HasMaxLength(200).IsRequired();
            entity.Property(customer => customer.Email).HasMaxLength(320).IsRequired();

            // E-mail is the natural key used by support when looking a customer up.
            entity.HasIndex(customer => customer.Email).IsUnique();

            entity.HasMany(customer => customer.Sites)
                .WithOne()
                .HasForeignKey(site => site.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.ToTable("sites");
            entity.HasKey(site => site.Id);
            entity.Property(site => site.Name).HasMaxLength(200).IsRequired();
            entity.Property(site => site.TimeZoneId).HasMaxLength(64).IsRequired();

            // The address has no identity of its own, so it is stored as an owned type
            // in the same table (columns prefixed with Address_).
            entity.OwnsOne(site => site.Address, address =>
            {
                address.Property(a => a.Street).HasColumnName("address_street").HasMaxLength(200);
                address.Property(a => a.City).HasColumnName("address_city").HasMaxLength(120);
                address.Property(a => a.PostalCode).HasColumnName("address_postal_code").HasMaxLength(20);
                address.Property(a => a.CountryCode).HasColumnName("address_country_code").HasMaxLength(2);
            });

            entity.HasMany(site => site.Panels)
                .WithOne()
                .HasForeignKey(panel => panel.SiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AlarmPanel>(entity =>
        {
            entity.ToTable("panels");
            entity.HasKey(panel => panel.Id);
            entity.Property(panel => panel.SerialNumber).HasMaxLength(64).IsRequired();
            entity.Property(panel => panel.Model).HasMaxLength(64).IsRequired();
            entity.Property(panel => panel.ApiKeyHash).HasMaxLength(128).IsRequired();
            entity.Property(panel => panel.ApiKeySalt).HasMaxLength(64).IsRequired();

            // Storing enums as text keeps the database readable during incident investigations.
            entity.Property(panel => panel.Status).HasConversion<string>().HasMaxLength(16);

            // A serial number identifies exactly one physical device.
            entity.HasIndex(panel => panel.SerialNumber).IsUnique();

            entity.HasMany(panel => panel.Sensors)
                .WithOne()
                .HasForeignKey(sensor => sensor.PanelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Sensor>(entity =>
        {
            entity.ToTable("sensors");
            entity.HasKey(sensor => sensor.Id);
            entity.Property(sensor => sensor.Name).HasMaxLength(120).IsRequired();
            entity.Property(sensor => sensor.Type).HasConversion<string>().HasMaxLength(16);

            // Zone numbers are unique per panel, not globally.
            entity.HasIndex(sensor => new { sensor.PanelId, sensor.ZoneNumber }).IsUnique();
        });
    }
}
