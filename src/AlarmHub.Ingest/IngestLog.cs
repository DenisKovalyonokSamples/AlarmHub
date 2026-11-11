using AlarmHub.Contracts.DeviceEvents;

namespace AlarmHub.Ingest;

/// <summary>
/// Strongly typed log messages for the ingest service.
/// </summary>
/// <remarks>
/// Source-generated <c>LoggerMessage</c> delegates avoid boxing and message formatting when the
/// level is disabled, which matters on the hot ingestion path (see CA1848).
/// </remarks>
internal static partial class IngestLog
{
    /// <summary>Event accepted and persisted.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="eventId">Event identifier.</param>
    /// <param name="eventType">Event type.</param>
    /// <param name="panelId">Panel identifier.</param>
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Accepted event {EventId} ({EventType}) from panel {PanelId}")]
    public static partial void EventAccepted(ILogger logger, Guid eventId, DeviceEventType eventType, Guid panelId);

    /// <summary>Duplicate event ignored because of the idempotency key.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="eventId">Event identifier.</param>
    /// <param name="panelId">Panel identifier.</param>
    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Duplicate event {EventId} from panel {PanelId} ignored")]
    public static partial void DuplicateIgnored(ILogger logger, Guid eventId, Guid panelId);

    /// <summary>Request rejected because the panel credential did not match.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="panelId">Panel identifier.</param>
    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning,
        Message = "Rejected telemetry for panel {PanelId}: invalid credential")]
    public static partial void InvalidCredential(ILogger logger, Guid panelId);

    /// <summary>Outbox message could not be published.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="messageId">Outbox message identifier.</param>
    /// <param name="exception">Failure.</param>
    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning,
        Message = "Publishing outbox message {MessageId} failed")]
    public static partial void OutboxPublishFailed(ILogger logger, Guid messageId, Exception exception);

    /// <summary>Integration event written to the log fallback publisher.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="eventType">Contract type name.</param>
    /// <param name="payload">Serialised payload.</param>
    [LoggerMessage(EventId = 1006, Level = LogLevel.Information,
        Message = "Integration event {EventType} published to log: {Payload}")]
    public static partial void IntegrationEventPublished(ILogger logger, string eventType, string payload);

    /// <summary>The dispatcher survived an unexpected failure.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="exception">Failure.</param>
    [LoggerMessage(EventId = 1005, Level = LogLevel.Error,
        Message = "Outbox dispatch failed; retrying on the next tick")]
    public static partial void OutboxDispatchFailed(ILogger logger, Exception exception);
}
