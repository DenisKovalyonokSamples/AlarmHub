namespace AlarmHub.Contracts.Incidents;

/// <summary>
/// How urgent an incident is for the monitoring station.
/// </summary>
/// <remarks>
/// The numeric order is meaningful: consumers may sort by severity to surface the loudest alarms first.
/// </remarks>
public enum IncidentSeverity
{
    /// <summary>Informational event, no operator action expected.</summary>
    Info = 1,

    /// <summary>Something needs attention but no immediate danger (fault, tamper outside armed state).</summary>
    Warning = 2,

    /// <summary>Alarm raised while the site was armed; requires operator triage.</summary>
    Alarm = 3,

    /// <summary>Life-safety or panic alarm; must be dispatched immediately.</summary>
    Critical = 4
}
