using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Identity.Infrastructure;

/// <summary>
/// EF Core context of the identity service. Stores OpenIddict applications, scopes and tokens.
/// </summary>
/// <remarks>
/// The service is deliberately isolated: tokens and clients live in their own database, and the
/// resource services validate tokens either against this issuer (local mode) or against Auth0.
/// </remarks>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Registers the OpenIddict entity sets (applications, authorizations, scopes, tokens).
        modelBuilder.UseOpenIddict();
    }
}

