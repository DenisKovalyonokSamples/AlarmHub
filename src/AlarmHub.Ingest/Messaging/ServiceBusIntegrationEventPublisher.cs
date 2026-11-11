using System.Diagnostics;
using Azure.Messaging.ServiceBus;

namespace AlarmHub.Ingest.Messaging;

/// <summary>
/// Publishes integration events to an Azure Service Bus topic.
/// </summary>
/// <remarks>
/// Three application properties travel with every message:
/// <list type="bullet">
/// <item><description><c>type</c> — contract name, used by consumers to route without deserialising;</description></item>
/// <item><description><c>Diagnostic-Id</c> — the property the Azure SDK reads to parent the consumer span,
/// which is what keeps one trace across the broker hop;</description></item>
/// <item><description><c>traceparent</c> — W3C context kept for non-.NET consumers.</description></item>
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

        if (Activity.Current?.Id is { Length: > 0 } traceParent)
        {
            message.ApplicationProperties["traceparent"] = traceParent;
            message.ApplicationProperties["Diagnostic-Id"] = traceParent;
        }

        await sender.SendMessageAsync(message, cancellationToken);
    }
}
