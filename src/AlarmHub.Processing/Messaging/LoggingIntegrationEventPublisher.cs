namespace AlarmHub.Processing.Messaging;

/// <summary>
/// Fallback publisher used when no Service Bus namespace is configured.
/// </summary>
/// <remarks>
/// Keeps local development runnable without cloud resources. The service logs a warning at startup,
/// so the fallback cannot be mistaken for a working broker in a deployed environment.
/// </remarks>
/// <param name="logger">Logger.</param>
public sealed class LoggingIntegrationEventPublisher(ILogger<LoggingIntegrationEventPublisher> logger)
    : IIntegrationEventPublisher
{
    /// <inheritdoc />
    public Task PublishAsync(string eventType, string payloadJson, CancellationToken cancellationToken)
    {
        ProcessingLog.IntegrationEventPublished(logger, eventType, payloadJson);
        return Task.CompletedTask;
    }
}
