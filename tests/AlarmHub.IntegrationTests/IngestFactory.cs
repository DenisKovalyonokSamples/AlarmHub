using AlarmHub.Ingest.Domain;
using AlarmHub.Ingest.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace AlarmHub.IntegrationTests;

/// <summary>
/// Boots the ingest service with a real database and a stubbed panel directory.
/// </summary>
/// <remarks>
/// The panel directory is replaced because it belongs to the API service: the test verifies ingest
/// behaviour (idempotency, outbox), not the HTTP hop between services.
/// </remarks>
public sealed class IngestFactory : WebApplicationFactory<DeviceEventRecord>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("alarmhub_ingest")
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
                ["ConnectionStrings:Ingest"] = _postgres.GetConnectionString()
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPanelDirectory>();
            services.AddSingleton<IPanelDirectory, AlwaysValidPanelDirectory>();
        });
    }

    /// <summary>Runs an action with a scoped DbContext, used by assertions.</summary>
    /// <param name="action">Work to perform.</param>
    public async Task WithDbContextAsync(Func<IngestDbContext, Task> action)
    {
        using IServiceScope scope = Services.CreateScope();
        IngestDbContext dbContext = scope.ServiceProvider.GetRequiredService<IngestDbContext>();
        await action(dbContext);
    }

    /// <summary>Panel directory stub that accepts every credential.</summary>
    private sealed class AlwaysValidPanelDirectory : IPanelDirectory
    {
        public Task<bool> VerifyPanelKeyAsync(Guid panelId, string apiKey, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
