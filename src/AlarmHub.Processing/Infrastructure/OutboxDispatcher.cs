using AlarmHub.Processing.Domain;
using AlarmHub.Processing.Messaging;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Processing.Infrastructure;

/// <summary>
/// Publishes pending outbox rows produced by incident processing.
/// </summary>
/// <remarks>
/// Same guarantees as the ingest dispatcher: oldest first, small batches, retries with error text
/// preserved and rows kept after publishing for audit and replay.
/// </remarks>
/// <param name="scopeFactory">Creates a DI scope per batch.</param>
/// <param name="publisher">Broker publisher abstraction.</param>
/// <param name="timeProvider">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IIntegrationEventPublisher publisher,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int published = await PublishBatchAsync(stoppingToken);

                if (published == 0)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                ProcessingLog.OutboxDispatchFailed(logger, exception);
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    /// <summary>Publishes one batch and returns how many rows were sent.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task<int> PublishBatchAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ProcessingDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProcessingDbContext>();

        List<OutboxMessage> pending = await dbContext.OutboxMessages
            .Where(message => message.PublishedAt == null)
            .OrderBy(message => message.OccurredOn)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        int published = 0;

        foreach (OutboxMessage message in pending)
        {
            try
            {
                // Resume the producing trace from the outbox row; without this the broker hop
                // would start a brand-new trace and lose the causal chain.
                ActivityContext parentContext = default;

                if (!string.IsNullOrWhiteSpace(message.TraceParent))
                {
                    ActivityContext.TryParse(message.TraceParent, null, out parentContext);
                }

                using Activity? publishActivity = TraceSource.Instance.StartActivity(
                    "PublishOutboxMessage",
                    ActivityKind.Producer,
                    parentContext);

                await publisher.PublishAsync(message.Type, message.Payload, cancellationToken);
                message.MarkPublished(timeProvider.GetUtcNow());
                published++;
            }
            catch (Exception exception)
            {
                message.RegisterFailure(exception.Message);
                ProcessingLog.OutboxPublishFailed(logger, message.Id, exception);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return published;
    }
}
