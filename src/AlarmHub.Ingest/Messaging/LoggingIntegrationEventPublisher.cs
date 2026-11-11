namespace AlarmHub.Ingest.Messaging;

/// <summary>
/// Development fallback that writes integration events to the log instead of a broker.
/// </summary>
/// <remarks>
/// Used when no Service Bus namespace is configured, so the whole pipeline can be demonstrated
/// locally without cloud resources. The service logs a warning at startup when this implementation
/// is selected, so a missing broker configuration cannot go unnoticed in a deployed environment.
/// </remarks>
/// <param name="logger">Logger used to record published events.</param>
public sealed class LoggingIntegrationEventPublisher(ILogger<LoggingIntegrationEventPublisher> logger)
    : IIntegrationEventPublisher
{
    /// <inheritdoc />
    public Task PublishAsync(string eventType, string payloadJson, CancellationToken cancellationToken)
    {
        // The payload is already JSON, so it is logged verbatim and can be replayed by hand.
        IngestLog.IntegrationEventPublished(logger, eventType, payloadJson);

        return Task.CompletedTask;
    }
}
