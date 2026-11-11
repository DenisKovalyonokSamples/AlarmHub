using AlarmHub.Notifications.Domain;

namespace AlarmHub.Notifications;

/// <summary>
/// Strongly typed log messages for the notification service.
/// </summary>
internal static partial class NotificationsLog
{
    /// <summary>Notification accepted by the sender.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="recipient">Recipient address.</param>
    /// <param name="subject">Subject line.</param>
    /// <param name="body">Message body.</param>
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information,
        Message = "Notification to {Recipient} | {Subject} | {Body}")]
    public static partial void NotificationDelivered(ILogger logger, string recipient, string subject, string body);

    /// <summary>Consumer started.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="entityPath">Topic/subscription path.</param>
    [LoggerMessage(EventId = 3002, Level = LogLevel.Information,
        Message = "Incident notification consumer started on {EntityPath}")]
    public static partial void ConsumerStarted(ILogger logger, string entityPath);

    /// <summary>Body could not be deserialised.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="messageId">Broker message identifier.</param>
    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning,
        Message = "Dropping unreadable incident message {MessageId}")]
    public static partial void UnreadableMessage(ILogger logger, string messageId);

    /// <summary>Delivery failed and the message will be retried or dead-lettered.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="incidentId">Incident identifier.</param>
    /// <param name="exception">Failure.</param>
    [LoggerMessage(EventId = 3004, Level = LogLevel.Error,
        Message = "Notification for incident {IncidentId} failed")]
    public static partial void DeliveryFailed(ILogger logger, Guid incidentId, Exception exception);

    /// <summary>Incident notification already delivered earlier.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="incidentId">Incident identifier.</param>
    [LoggerMessage(EventId = 3005, Level = LogLevel.Information,
        Message = "Incident {IncidentId} was already notified; duplicate ignored")]
    public static partial void DuplicateIgnored(ILogger logger, Guid incidentId);

    /// <summary>Incident notification sent.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="incidentId">Incident identifier.</param>
    /// <param name="recipient">Recipient.</param>
    [LoggerMessage(EventId = 3006, Level = LogLevel.Information,
        Message = "Incident {IncidentId} notification sent to {Recipient}")]
    public static partial void IncidentNotified(ILogger logger, Guid incidentId, string recipient);

    /// <summary>Transport-level failure.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">Failure.</param>
    /// <param name="entityPath">Entity path.</param>
    /// <param name="errorSource">Error source.</param>
    [LoggerMessage(EventId = 3007, Level = LogLevel.Error,
        Message = "Service Bus error in {EntityPath} ({ErrorSource})")]
    public static partial void ServiceBusError(ILogger logger, Exception exception, string entityPath, string errorSource);
}
