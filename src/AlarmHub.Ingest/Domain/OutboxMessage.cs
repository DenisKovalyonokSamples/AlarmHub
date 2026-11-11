namespace AlarmHub.Ingest.Domain;

/// <summary>
/// Outbox row written in the same transaction as the business data.
/// </summary>
/// <remarks>
/// Publishing to Service Bus directly from the request handler would create a dual-write problem:
/// the database could commit while the broker call fails (event lost), or the broker could accept
/// a message that the database later rolls back (phantom event). The outbox removes that window:
/// a background dispatcher publishes rows and marks them as sent.
/// </remarks>
public sealed class OutboxMessage
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Moment the message was produced, in UTC; also defines dispatch order.</summary>
    public DateTimeOffset OccurredOn { get; private set; }

    /// <summary>Contract type name used by consumers for routing (for example <c>DeviceEventReceivedV1</c>).</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>Serialised event payload.</summary>
    public string Payload { get; private set; } = "{}";

    /// <summary>Moment the message was successfully published; null while still pending.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Number of publish attempts; used to back off and to surface poison messages.</summary>
    public int Attempts { get; private set; }

    /// <summary>Last error message, kept for diagnostics in the operations console.</summary>
    public string? LastError { get; private set; }

    /// <summary>Required by EF Core.</summary>
    private OutboxMessage()
    {
    }

    /// <summary>Creates a pending outbox message.</summary>
    /// <param name="type">Contract type name.</param>
    /// <param name="payload">Serialised payload.</param>
    /// <param name="now">Current UTC timestamp.</param>
    public static OutboxMessage Create(string type, string payload, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        OccurredOn = now,
        Type = type,
        Payload = payload,
        Attempts = 0
    };

    /// <summary>Marks the message as successfully published.</summary>
    /// <param name="now">Current UTC timestamp.</param>
    public void MarkPublished(DateTimeOffset now)
    {
        PublishedAt = now;
        LastError = null;
    }

    /// <summary>Records a failed publish attempt so the dispatcher can retry with back-off.</summary>
    /// <param name="error">Error description.</param>
    public void RegisterFailure(string error)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;
    }
}
