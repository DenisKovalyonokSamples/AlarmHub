namespace AlarmHub.Ingest.Messaging;

/// <summary>
/// Publishes integration events to the message broker.
/// </summary>
/// <remarks>
/// The outbox dispatcher depends on this abstraction so that tests and local runs can swap
/// Service Bus for a logging implementation without changing any business code.
/// </remarks>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Publishes one integration event.
    /// </summary>
    /// <param name="eventType">Contract type name used for routing.</param>
    /// <param name="payloadJson">Serialised payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(string eventType, string payloadJson, CancellationToken cancellationToken);
}
