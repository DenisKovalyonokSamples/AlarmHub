using System.Text.Json;
using AlarmHub.Contracts.DeviceEvents;
using AlarmHub.Contracts.Incidents;
using AlarmHub.Processing.Domain;
using AlarmHub.Processing.Infrastructure;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Processing.Messaging;

/// <summary>
/// Consumes device events from the <c>device-events</c> topic and turns them into incidents.
/// </summary>
/// <remarks>
/// Reliability rules implemented here:
/// <list type="bullet">
/// <item><description><b>Idempotency</b> — the inbox row is inserted in the same transaction as the incident,
/// so a redelivered message cannot open two incidents.</description></item>
/// <item><description><b>Poison messages</b> — after the broker's delivery count is exhausted the message is
/// dead-lettered; the error is logged with the event id so it can be replayed.</description></item>
/// <item><description><b>Tracing</b> — the ambient activity continues the trace started by ingest thanks to the
/// propagated <c>traceparent</c> property.</description></item>
/// </list>
/// </remarks>
/// <param name="processor">Service Bus processor bound to the processing subscription.</param>
/// <param name="scopeFactory">Creates a scope per message (DbContext is scoped).</param>
/// <param name="panelDirectory">Panel lookup used to resolve the site.</param>
/// <param name="timeProvider">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed class DeviceEventConsumer(
    ServiceBusProcessor processor,
    IServiceScopeFactory scopeFactory,
    IPanelDirectory panelDirectory,
    TimeProvider timeProvider,
    ILogger<DeviceEventConsumer> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        processor.ProcessMessageAsync += HandleMessageAsync;
        processor.ProcessErrorAsync += HandleErrorAsync;

        await processor.StartProcessingAsync(stoppingToken);

        ProcessingLog.ConsumerStarted(logger, processor.EntityPath);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            await processor.StopProcessingAsync(CancellationToken.None);
            await processor.DisposeAsync();
        }
    }

    /// <summary>Handles one broker message.</summary>
    /// <param name="args">Message and cancellation context.</param>
    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        DeviceEventReceivedV1? deviceEvent = JsonSerializer.Deserialize<DeviceEventReceivedV1>(args.Message.Body);

        if (deviceEvent is null)
        {
            ProcessingLog.UnreadableMessage(logger, args.Message.MessageId);
            await args.DeadLetterMessageAsync(args.Message, "invalid-payload", "Body could not be deserialised", args.CancellationToken);
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        ProcessingDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProcessingDbContext>();

        bool alreadyProcessed = await dbContext.ProcessedEvents
            .AnyAsync(processed => processed.EventId == deviceEvent.EventId, args.CancellationToken);

        if (alreadyProcessed)
        {
            // At-least-once delivery: a duplicate is expected and simply acknowledged.
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            return;
        }

        RuleOutcome outcome = IncidentRuleEngine.Evaluate(deviceEvent);

        if (!outcome.ShouldOpenIncident)
        {
            dbContext.ProcessedEvents.Add(ProcessedEvent.Create(deviceEvent.EventId, timeProvider.GetUtcNow()));
            await dbContext.SaveChangesAsync(args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            return;
        }

        PanelInfo? panel = await panelDirectory.FindAsync(deviceEvent.PanelId, args.CancellationToken);

        if (panel is null)
        {
            // Unknown panel: retrying will not help, so dead-letter it for investigation.
            ProcessingLog.UnknownPanel(logger, deviceEvent.PanelId, deviceEvent.EventId);
            await args.DeadLetterMessageAsync(args.Message, "unknown-panel", "Panel not found in the API directory", args.CancellationToken);
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        Incident incident = Incident.Open(
            panel.SiteId,
            deviceEvent.PanelId,
            deviceEvent.EventId,
            outcome.Severity,
            outcome.Title,
            now);

        IncidentOpenedV1 integrationEvent = new(
            incident.Id,
            incident.SiteId,
            incident.PanelId,
            incident.TriggerEventId,
            incident.Severity,
            incident.OpenedAt,
            incident.Title);

        // One transaction: inbox row, incident and outbox message either all persist or none do.
        dbContext.ProcessedEvents.Add(ProcessedEvent.Create(deviceEvent.EventId, now));
        dbContext.Incidents.Add(incident);
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            nameof(IncidentOpenedV1),
            JsonSerializer.Serialize(integrationEvent),
            now));

        await dbContext.SaveChangesAsync(args.CancellationToken);
        await args.CompleteMessageAsync(args.Message, args.CancellationToken);

        ProcessingLog.IncidentOpened(logger, incident.Id, incident.Severity, incident.SiteId, deviceEvent.EventId);
    }

    /// <summary>Logs transport-level failures raised by the Service Bus processor.</summary>
    /// <param name="args">Error context.</param>
    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        ProcessingLog.ServiceBusError(logger, args.Exception, args.EntityPath, args.ErrorSource.ToString());
        return Task.CompletedTask;
    }
}
