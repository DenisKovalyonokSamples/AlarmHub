namespace AlarmHub.Processing.Domain;

/// <summary>
/// Inbox row that records which integration events were already processed.
/// </summary>
/// <remarks>
/// Service Bus guarantees at-least-once delivery, so every consumer needs an inbox. The primary key
/// is the device event identifier, which makes the "insert or fail" check a single atomic operation.
/// </remarks>
public sealed class ProcessedEvent
{
    /// <summary>Device event identifier; primary key.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Moment the event was processed, in UTC.</summary>
    public DateTimeOffset ProcessedAt { get; private set; }

    /// <summary>Required by EF Core.</summary>
    private ProcessedEvent()
    {
    }

    /// <summary>Creates an inbox row.</summary>
    /// <param name="eventId">Processed device event identifier.</param>
    /// <param name="processedAt">Moment of processing.</param>
    public static ProcessedEvent Create(Guid eventId, DateTimeOffset processedAt) => new()
    {
        EventId = eventId,
        ProcessedAt = processedAt
    };
}
