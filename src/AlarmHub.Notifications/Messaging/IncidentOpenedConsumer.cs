using System.Diagnostics;
using System.Diagnostics;
using System.Text.Json;
using AlarmHub.Contracts.Incidents;
using AlarmHub.Notifications.Domain;
using AlarmHub.Notifications.Infrastructure;
using AlarmHub.Notifications.Notifications;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Notifications.Messaging;

/// <summary>
/// Consumes <c>IncidentOpenedV1</c> and delivers one notification per incident.
/// </summary>
/// <remarks>
/// Delivery semantics:
/// <list type="bullet">
/// <item><description>the unique index on (incident, channel) makes redelivery harmless;</description></item>
/// <item><description>a provider failure leaves the message uncompleted so Service Bus retries it and eventually
/// dead-letters it, instead of silently losing the alert;</description></item>
/// <item><description>the full message body is written to the log only by the development sender.</description></item>
/// </list>
/// </remarks>
/// <param name="processor">Processor bound to the notifications subscription.</param>
/// <param name="scopeFactory">Scope factory for the DbContext.</param>
/// <param name="sender">Delivery provider.</param>
/// <param name="configuration">Configuration holding the fallback recipient.</param>
/// <param name="timeProvider">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed class IncidentOpenedConsumer(
    ServiceBusProcessor processor,
    IServiceScopeFactory scopeFactory,
    INotificationSender sender,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<IncidentOpenedConsumer> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        processor.ProcessMessageAsync += HandleMessageAsync;
        processor.ProcessErrorAsync += HandleErrorAsync;

        await processor.StartProcessingAsync(stoppingToken);

        NotificationsLog.ConsumerStarted(logger, processor.EntityPath);

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

    /// <summary>Handles one incident message.</summary>
    /// <param name="args">Message context.</param>
    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        // Continue the trace started by processing, so one trace covers the whole alert.
        ActivityContext parentContext = default;

        if (args.Message.ApplicationProperties.TryGetValue("traceparent", out object? rawTraceParent)
            && rawTraceParent is string traceParent)
        {
            ActivityContext.TryParse(traceParent, null, out parentContext);
        }

        using Activity? activity = TraceSource.Instance.StartActivity("SendIncidentNotification", ActivityKind.Consumer, parentContext);


        IncidentOpenedV1? incident = JsonSerializer.Deserialize<IncidentOpenedV1>(args.Message.Body);

        if (incident is null)
        {
            NotificationsLog.UnreadableMessage(logger, args.Message.MessageId);
            await args.DeadLetterMessageAsync(args.Message, "invalid-payload", "Body could not be deserialised", args.CancellationToken);
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        NotificationsDbContext dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        bool alreadyNotified = await dbContext.Notifications
            .AnyAsync(record => record.IncidentId == incident.IncidentId, args.CancellationToken);

        if (alreadyNotified)
        {
            NotificationsLog.DuplicateIgnored(logger, incident.IncidentId);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            return;
        }

        // Until customer contacts are resolved through the API, all alerts go to the operations inbox.
        string recipient = configuration["Notifications:DefaultRecipient"] ?? "operations@alarmhub.local";

        NotificationRecord record = NotificationRecord.Create(
            incident.IncidentId,
            incident.SiteId,
            recipient,
            $"[{incident.Severity}] {incident.Title}");

        dbContext.Notifications.Add(record);

        try
        {
            await sender.SendAsync(record.Recipient, record.Subject, BuildBody(incident), args.CancellationToken);

            record.MarkSent(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);

            NotificationsLog.IncidentNotified(logger, incident.IncidentId, record.Recipient);
        }
        catch (Exception exception)
        {
            // Keep the record with the error, then abandon so Service Bus retries the message.
            record.RegisterFailure(exception.Message);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            NotificationsLog.DeliveryFailed(logger, incident.IncidentId, exception);
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    /// <summary>Builds the plain-text notification body.</summary>
    /// <param name="incident">Incident that was opened.</param>
    private static string BuildBody(IncidentOpenedV1 incident) =>
        $"""
         Incident: {incident.Title}
         Severity: {incident.Severity}
         Site: {incident.SiteId}
         Panel: {incident.PanelId}
         Opened at (UTC): {incident.OpenedAt:yyyy-MM-dd HH:mm:ss}
         Incident id: {incident.IncidentId}
         """;

    /// <summary>Logs transport-level failures.</summary>
    /// <param name="args">Error context.</param>
    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        NotificationsLog.ServiceBusError(logger, args.Exception, args.EntityPath, args.ErrorSource.ToString());
        return Task.CompletedTask;
    }
}

