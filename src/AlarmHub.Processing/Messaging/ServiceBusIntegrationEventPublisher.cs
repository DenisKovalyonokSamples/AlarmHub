using System.Diagnostics;
using Azure.Messaging.ServiceBus;

namespace AlarmHub.Processing.Messaging;

/// <summary>
/// Publishes integration events to an Azure Service Bus topic.
/// </summary>
/// <remarks>
/// <c>type</c> lets consumers route without deserialising the body; <c>traceparent</c> continues the
/// W3C trace across the broker hop so one trace covers ingest → processing → notifications.
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

        if (Activity.Current?.Id is { Length: > 0 } traceParent)
        {
            message.ApplicationProperties["traceparent"] = traceParent;
        }

        await sender.SendMessageAsync(message, cancellationToken);
    }
}
