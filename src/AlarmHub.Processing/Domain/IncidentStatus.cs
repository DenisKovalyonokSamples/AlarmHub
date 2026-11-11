namespace AlarmHub.Processing.Domain;

/// <summary>
/// Lifecycle of an incident.
/// </summary>
public enum IncidentStatus
{
    /// <summary>Newly opened; nobody looked at it yet.</summary>
    Open = 1,

    /// <summary>An operator acknowledged the incident and is working on it.</summary>
    Acknowledged = 2,

    /// <summary>Resolved; kept for reporting and audits.</summary>
    Closed = 3
}
