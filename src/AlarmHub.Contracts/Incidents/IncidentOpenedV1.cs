namespace AlarmHub.Contracts.Incidents;

/// <summary>
/// Integration event published when the processing service opens an incident for a site.
/// </summary>
/// <remarks>
/// The notification service consumes this event to alert operators and customers.
/// The event carries everything a notifier needs, so it never has to call back into the platform
/// (avoids a synchronous dependency between workers and keeps the pipeline resilient).
/// </remarks>
/// <param name="IncidentId">Identifier of the incident aggregate.</param>
/// <param name="SiteId">Site the incident belongs to.</param>
/// <param name="PanelId">Panel that produced the trigger.</param>
/// <param name="TriggerEventId">Id of the device event that opened the incident (traceability).</param>
/// <param name="Severity">Severity assigned by the rules engine.</param>
/// <param name="OpenedAt">Moment the incident was opened, in UTC.</param>
/// <param name="Title">Human readable headline shown in the operations console and in notifications.</param>
public sealed record IncidentOpenedV1(
    Guid IncidentId,
    Guid SiteId,
    Guid PanelId,
    Guid TriggerEventId,
    IncidentSeverity Severity,
    DateTimeOffset OpenedAt,
    string Title);
