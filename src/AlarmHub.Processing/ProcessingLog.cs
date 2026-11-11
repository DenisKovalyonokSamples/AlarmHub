namespace AlarmHub.Processing;

/// <summary>
/// Strongly typed log messages for the processing service (source-generated, CA1848-safe).
/// </summary>
internal static partial class ProcessingLog
{
    /// <summary>Integration event written by the log fallback publisher.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="eventType">Contract type name.</param>
    /// <param name="payload">Serialised payload.</param>
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
        Message = "Integration event {EventType} published to log: {Payload}")]
    public static partial void IntegrationEventPublished(ILogger logger, string eventType, string payload);

    /// <summary>Outbox message could not be published.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="messageId">Outbox message identifier.</param>
    /// <param name="exception">Failure.</param>
    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "Publishing outbox message {MessageId} failed")]
    public static partial void OutboxPublishFailed(ILogger logger, Guid messageId, Exception exception);

    /// <summary>The outbox dispatcher survived an unexpected failure.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">Failure.</param>
    [LoggerMessage(EventId = 2003, Level = LogLevel.Error,
        Message = "Outbox dispatch failed; retrying on the next tick")]
    public static partial void OutboxDispatchFailed(ILogger logger, Exception exception);

    /// <summary>Consumer started and is listening on the subscription.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="entityPath">Topic/subscription path.</param>
    [LoggerMessage(EventId = 2004, Level = LogLevel.Information,
        Message = "Device event consumer started on {EntityPath}")]
    public static partial void ConsumerStarted(ILogger logger, string entityPath);

    /// <summary>Message body could not be deserialised and was dead-lettered.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="messageId">Broker message identifier.</param>
    [LoggerMessage(EventId = 2005, Level = LogLevel.Warning,
        Message = "Dropping unreadable device event message {MessageId}")]
    public static partial void UnreadableMessage(ILogger logger, string messageId);

    /// <summary>Event references a panel that is not in the API directory.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="panelId">Panel identifier.</param>
    /// <param name="eventId">Device event identifier.</param>
    [LoggerMessage(EventId = 2006, Level = LogLevel.Warning,
        Message = "Panel {PanelId} is unknown; dead-lettering event {EventId}")]
    public static partial void UnknownPanel(ILogger logger, Guid panelId, Guid eventId);

    /// <summary>Incident opened for a site.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="incidentId">Incident identifier.</param>
    /// <param name="severity">Severity.</param>
    /// <param name="siteId">Site identifier.</param>
    /// <param name="eventId">Triggering device event.</param>
    [LoggerMessage(EventId = 2007, Level = LogLevel.Information,
        Message = "Opened incident {IncidentId} ({Severity}) for site {SiteId} from event {EventId}")]
    public static partial void IncidentOpened(
        ILogger logger,
        Guid incidentId,
        AlarmHub.Contracts.Incidents.IncidentSeverity severity,
        Guid siteId,
        Guid eventId);

    /// <summary>Transport-level failure raised by the Service Bus processor.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">Failure.</param>
    /// <param name="entityPath">Entity path.</param>
    /// <param name="errorSource">Error source reported by the SDK.</param>
    [LoggerMessage(EventId = 2008, Level = LogLevel.Error,
        Message = "Service Bus error in {EntityPath} ({ErrorSource})")]
    public static partial void ServiceBusError(ILogger logger, Exception exception, string entityPath, string errorSource);
}
