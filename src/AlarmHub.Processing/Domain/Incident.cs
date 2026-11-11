using AlarmHub.Contracts.Incidents;

namespace AlarmHub.Processing.Domain;

/// <summary>
/// Incident opened by the rules engine when panel telemetry requires operator attention.
/// </summary>
/// <remarks>
/// An incident is the unit an operator works with: it aggregates the triggering event, carries
/// severity and keeps its own lifecycle (open → acknowledged → closed). Only the opening transition
/// is implemented in this milestone; acknowledgement and closing arrive with the operations console.
/// </remarks>
public sealed class Incident
{
    /// <summary>Database identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Site the incident belongs to; taken from the panel directory when the event is processed.</summary>
    public Guid SiteId { get; private set; }

    /// <summary>Panel that produced the triggering event.</summary>
    public Guid PanelId { get; private set; }

    /// <summary>Device event that opened the incident; used for traceability and replay.</summary>
    public Guid TriggerEventId { get; private set; }

    /// <summary>Severity assigned by the rules engine.</summary>
    public IncidentSeverity Severity { get; private set; }

    /// <summary>Current lifecycle state.</summary>
    public IncidentStatus Status { get; private set; }

    /// <summary>Headline shown to operators and used in notifications.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Moment the incident was opened, in UTC.</summary>
    public DateTimeOffset OpenedAt { get; private set; }

    /// <summary>Required by EF Core.</summary>
    private Incident()
    {
    }

    /// <summary>
    /// Opens a new incident in <see cref="IncidentStatus.Open"/> state.
    /// </summary>
    /// <param name="siteId">Site resolved from the panel directory.</param>
    /// <param name="panelId">Panel that raised the event.</param>
    /// <param name="triggerEventId">Event that caused the incident.</param>
    /// <param name="severity">Severity from the rules engine.</param>
    /// <param name="title">Operator-facing headline.</param>
    /// <param name="now">Current UTC timestamp.</param>
    public static Incident Open(
        Guid siteId,
        Guid panelId,
        Guid triggerEventId,
        IncidentSeverity severity,
        string title,
        DateTimeOffset now)
    {
        if (siteId == Guid.Empty)
        {
            throw new ArgumentException("Site id is required.", nameof(siteId));
        }

        if (panelId == Guid.Empty)
        {
            throw new ArgumentException("Panel id is required.", nameof(panelId));
        }

        return new Incident
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            PanelId = panelId,
            TriggerEventId = triggerEventId,
            Severity = severity,
            Status = IncidentStatus.Open,
            Title = string.IsNullOrWhiteSpace(title) ? "Incident" : title.Trim(),
            OpenedAt = now
        };
    }

    /// <summary>Moves the incident to acknowledged (operator picked it up).</summary>
    public void Acknowledge()
    {
        if (Status != IncidentStatus.Open)
        {
            throw new InvalidOperationException("Only open incidents can be acknowledged.");
        }

        Status = IncidentStatus.Acknowledged;
    }

    /// <summary>Closes the incident.</summary>
    public void Close()
    {
        if (Status == IncidentStatus.Closed)
        {
            throw new InvalidOperationException("Incident is already closed.");
        }

        Status = IncidentStatus.Closed;
    }
}
