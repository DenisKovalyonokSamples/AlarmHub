namespace AlarmHub.Processing.Domain;

/// <summary>
/// Outbox row written in the same transaction as the incident it announces.
/// </summary>
/// <remarks>
/// The pattern is identical to the ingest service: business data and the pending integration event
/// are committed atomically, then a background dispatcher publishes and stamps the row. The stored
/// <see cref="TraceParent"/> lets the dispatcher resume the originating trace instead of starting
/// a detached one.
/// </remarks>
public sealed class OutboxMessage
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Moment the message was produced, in UTC; defines dispatch order.</summary>
    public DateTimeOffset OccurredOn { get; private set; }

    /// <summary>Contract type name used for routing.</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>Serialised event payload.</summary>
    public string Payload { get; private set; } = "{}";

    /// <summary>W3C trace context captured when the row was created.</summary>
    public string? TraceParent { get; private set; }

    /// <summary>Moment the message was published; null while pending.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Publish attempts so far.</summary>
    public int Attempts { get; private set; }

    /// <summary>Last publish error, kept for diagnostics.</summary>
    public string? LastError { get; private set; }

    /// <summary>Required by EF Core.</summary>
    private OutboxMessage()
    {
    }

    /// <summary>Creates a pending outbox message.</summary>
    /// <param name="type">Contract type name.</param>
    /// <param name="payload">Serialised payload.</param>
    /// <param name="traceParent">W3C context of the producing activity.</param>
    /// <param name="now">Current UTC timestamp.</param>
    public static OutboxMessage Create(string type, string payload, string? traceParent, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        OccurredOn = now,
        Type = type,
        Payload = payload,
        TraceParent = traceParent
    };

    /// <summary>Marks the message as published.</summary>
    /// <param name="now">Current UTC timestamp.</param>
    public void MarkPublished(DateTimeOffset now)
    {
        PublishedAt = now;
        LastError = null;
    }

    /// <summary>Records a failed publish attempt.</summary>
    /// <param name="error">Error description.</param>
    public void RegisterFailure(string error)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;
    }
}
