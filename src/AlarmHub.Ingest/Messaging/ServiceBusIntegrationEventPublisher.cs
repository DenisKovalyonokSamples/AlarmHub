using System.Diagnostics;
using Azure.Messaging.ServiceBus;

namespace AlarmHub.Ingest.Messaging;

/// <summary>
/// Publishes integration events to an Azure Service Bus topic.
/// </summary>
/// <remarks>
/// Two application properties travel with every message:
/// <list type="bullet">
/// <item><description><c>type</c> — contract name, used by consumers to route without deserialising;</description></item>
/// <item><description><c>traceparent</c> — W3C trace context, so one trace spans ingest, processing and notifications.</description></item>
/// </list>
/// </remarks>
/// <param name="sender">Sender bound to the destination topic.</param>
public sealed class ServiceBusIntegrationEventPublisher(ServiceBusSender sender) : IIntegrationEventPublisher
{
    /// <inheritdoc />
    public async Task PublishAsync(string eventType, string payloadJson, CancellationToken cancellationToken)
    {
        ServiceBusMessage message = new(payloadJson)
        {
            ContentType = "application/json",
            Subject = eventType
        };

        message.ApplicationProperties["type"] = eventType;

        // Propagating the trace context keeps a single end-to-end trace across the broker hop.
        string? traceParent = Activity.Current?.Id;

        if (!string.IsNullOrWhiteSpace(traceParent))
        {
            message.ApplicationProperties["traceparent"] = traceParent;
        }

        await sender.SendMessageAsync(message, cancellationToken);
    }
}
