namespace AlarmHub.Ingest.Domain;

/// <summary>
/// Raw device event persisted by ingest before any business interpretation happened.
/// </summary>
/// <remarks>
/// The table doubles as the idempotency store: <see cref="EventId"/> is the primary key, so a panel
/// that retries a request can never create a duplicate. Rows are append-only; the processing service
/// keeps its own derived state.
/// </remarks>
public sealed class DeviceEventRecord
{
    /// <summary>Identifier produced by the panel; primary key and idempotency key.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Panel that reported the event.</summary>
    public Guid PanelId { get; private set; }

    /// <summary>Moment the panel recorded the event, in UTC.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Event type as reported by the panel (kept as text so unknown values are still stored).</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Per-panel sequence number used to detect gaps.</summary>
    public long SequenceNumber { get; private set; }

    /// <summary>Raw payload exactly as received, serialised as JSON.</summary>
    public string PayloadJson { get; private set; } = "{}";

    /// <summary>Moment ingest accepted the event, in UTC.</summary>
    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Required by EF Core.</summary>
    private DeviceEventRecord()
    {
    }

    /// <summary>
    /// Creates the record that will be stored for an accepted event.
    /// </summary>
    /// <param name="eventId">Panel-provided event identifier.</param>
    /// <param name="panelId">Panel that produced the event.</param>
    /// <param name="occurredAt">Moment recorded by the panel.</param>
    /// <param name="eventType">Event type name.</param>
    /// <param name="sequenceNumber">Per-panel sequence number.</param>
    /// <param name="payloadJson">Serialised payload.</param>
    /// <param name="receivedAt">Moment ingest accepted the event.</param>
    public static DeviceEventRecord Create(
        Guid eventId,
        Guid panelId,
        DateTimeOffset occurredAt,
        string eventType,
        long sequenceNumber,
        string payloadJson,
        DateTimeOffset receivedAt)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event id is required.", nameof(eventId));
        }

        return new DeviceEventRecord
        {
            EventId = eventId,
            PanelId = panelId,
            OccurredAt = occurredAt,
            EventType = eventType,
            SequenceNumber = sequenceNumber,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            ReceivedAt = receivedAt
        };
    }
}
