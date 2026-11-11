namespace AlarmHub.Processing.Messaging;

/// <summary>
/// Publishes integration events to the broker.
/// </summary>
/// <remarks>
/// Same contract as in the ingest service; kept local so each service can evolve its transport
/// independently and no shared binary couples their release cycles.
/// </remarks>
public interface IIntegrationEventPublisher
{
    /// <summary>Publishes one integration event.</summary>
    /// <param name="eventType">Contract type name used for routing.</param>
    /// <param name="payloadJson">Serialised payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(string eventType, string payloadJson, CancellationToken cancellationToken);
}
