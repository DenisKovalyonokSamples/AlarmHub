using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AlarmHub.Identity.Infrastructure;

/// <summary>
/// Creates the demo OAuth client and scopes used by local development and integration tests.
/// </summary>
/// <remarks>
/// Seeding is idempotent: running the service twice does not duplicate clients or scopes.
/// Credentials come from configuration so they never live in source control.
/// </remarks>
internal static class IdentitySeeder
{
    /// <summary>Client id used by the console and by the smoke-test scripts.</summary>
    public const string DemoClientId = "alarmhub-console";

    /// <summary>Creates scopes and the demo client when they are missing.</summary>
    /// <param name="services">Scoped service provider.</param>
    /// <param name="configuration">Configuration holding the demo client secret.</param>
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        IOpenIddictScopeManager scopeManager = services.GetRequiredService<IOpenIddictScopeManager>();
        IOpenIddictApplicationManager applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();

        foreach (string scopeName in new[] { "alarms.read", "alarms.write", "devices.ingest" })
        {
            if (await scopeManager.FindByNameAsync(scopeName) is null)
            {
                await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
                {
                    Name = scopeName,
                    DisplayName = scopeName,
                    Resources = { "alarmhub-api" }
                });
            }
        }

        // FindByClientIdAsync returns the stored application instance (or null), not a string.
        object? existing = await applicationManager.FindByClientIdAsync(DemoClientId);

        if (existing is null)
        {
            string secret = configuration["Identity:DemoClientSecret"] ?? "dev-secret-change-me";

            await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = DemoClientId,
                ClientSecret = secret,
                DisplayName = "AlarmHub console (demo)",
                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.ClientCredentials,
                    Permissions.Prefixes.Scope + "alarms.read",
                    Permissions.Prefixes.Scope + "alarms.write",
                    Permissions.Prefixes.Scope + "devices.ingest"
                }
            });
        }
    }
}
