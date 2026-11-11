using AlarmHub.Ingest.Domain;
using AlarmHub.Ingest.Messaging;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Ingest.Infrastructure;

/// <summary>
/// Background worker that publishes pending outbox rows to the broker.
/// </summary>
/// <remarks>
/// Design notes:
/// <list type="bullet">
/// <item><description>Rows are processed oldest first in small batches, so a burst of telemetry does not
/// starve older events.</description></item>
/// <item><description>A failed publish increments <c>Attempts</c> and leaves the row pending; the next tick
/// retries it. Poison messages keep their error text for the operations console.</description></item>
/// <item><description>Successful rows get <c>PublishedAt</c> instead of being deleted, so the outbox can be
/// audited and replayed.</description></item>
/// </list>
/// </remarks>
/// <param name="scopeFactory">Creates a DI scope per batch because <see cref="IngestDbContext"/> is scoped.</param>
/// <param name="publisher">Broker publisher abstraction.</param>
/// <param name="timeProvider">Clock used for timestamps and tests.</param>
/// <param name="logger">Logger.</param>
public sealed partial class OutboxDispatcher(
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

                // Only sleep when the queue is empty; a busy queue is drained without delay.
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
                // The dispatcher must survive broker outages: log and retry on the next tick.
                IngestLog.OutboxDispatchFailed(logger, exception);
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    /// <summary>Publishes one batch and returns how many rows were sent.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task<int> PublishBatchAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IngestDbContext dbContext = scope.ServiceProvider.GetRequiredService<IngestDbContext>();

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
                await publisher.PublishAsync(message.Type, message.Payload, cancellationToken);
                message.MarkPublished(timeProvider.GetUtcNow());
                published++;
            }
            catch (Exception exception)
            {
                message.RegisterFailure(exception.Message);
                IngestLog.OutboxPublishFailed(logger, message.Id, exception);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return published;
    }
}
