using AlarmHub.Api.Domain.Customers;
using AlarmHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace AlarmHub.IntegrationTests;

/// <summary>
/// Boots the API against a throwaway PostgreSQL container.
/// </summary>
/// <remarks>
/// A real database is used on purpose: the interesting bugs live in the EF mapping (owned types,
/// unique indexes, enum conversions), and an in-memory provider would hide them.
/// </remarks>
public sealed class ApiFactory : WebApplicationFactory<Customer>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("alarmhub_api")
        .WithUsername("alarm")
        .WithPassword("alarm")
        .Build();

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    /// <inheritdoc />
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AlarmHub"] = _postgres.GetConnectionString()
            });
        });
    }

    /// <summary>Runs an action with a scoped DbContext, used by assertions.</summary>
    /// <param name="action">Work to perform.</param>
    public async Task WithDbContextAsync(Func<AlarmHubDbContext, Task> action)
    {
        using IServiceScope scope = Services.CreateScope();
        AlarmHubDbContext dbContext = scope.ServiceProvider.GetRequiredService<AlarmHubDbContext>();
        await action(dbContext);
    }
}
