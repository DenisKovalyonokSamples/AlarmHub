using AlarmHub.Contracts.DeviceEvents;
using AlarmHub.Contracts.Incidents;

namespace AlarmHub.Processing.Domain;

/// <summary>
/// Result of evaluating a device event: whether it opens an incident, with which severity and title.
/// </summary>
/// <param name="ShouldOpenIncident">False for routine telemetry such as heartbeats.</param>
/// <param name="Severity">Severity to assign when an incident is opened.</param>
/// <param name="Title">Operator-facing headline.</param>
public sealed record RuleOutcome(bool ShouldOpenIncident, IncidentSeverity Severity, string Title)
{
    /// <summary>Outcome for telemetry that must not create an incident.</summary>
    public static RuleOutcome NoIncident { get; } = new(false, IncidentSeverity.Info, string.Empty);
}

/// <summary>
/// Pure rules engine that maps a device event to an incident decision.
/// </summary>
/// <remarks>
/// Deliberately free of infrastructure concerns so the business rules can be unit tested without a
/// database or a broker. The engine is the first place to look when an alarm "did not fire".
/// </remarks>
public static class IncidentRuleEngine
{
    /// <summary>Zone attribute reported by panels inside the event payload.</summary>
    private const string ZonePayloadKey = "zone";

    /// <summary>
    /// Evaluates one device event.
    /// </summary>
    /// <param name="deviceEvent">Event received from the ingest service.</param>
    /// <returns>The decision for this event.</returns>
    public static RuleOutcome Evaluate(DeviceEventReceivedV1 deviceEvent)
    {
        ArgumentNullException.ThrowIfNull(deviceEvent);

        return deviceEvent.Type switch
        {
            // An alarm while armed is the product's core promise: it always becomes an incident.
            DeviceEventType.AlarmTriggered => new RuleOutcome(
                true,
                IncidentSeverity.Alarm,
                BuildTitle("Alarm triggered", deviceEvent)),

            // Tampering means the site may be compromised even if no alarm was raised.
            DeviceEventType.TamperDetected => new RuleOutcome(
                true,
                IncidentSeverity.Warning,
                BuildTitle("Tamper detected", deviceEvent)),

            // Technical faults leave the site unprotected, so they are actionable as well.
            DeviceEventType.FaultReported => new RuleOutcome(
                true,
                IncidentSeverity.Warning,
                BuildTitle("Panel fault reported", deviceEvent)),

            // Arm/disarm and heartbeats are state, not incidents.
            DeviceEventType.Armed or DeviceEventType.Disarmed or DeviceEventType.Heartbeat => RuleOutcome.NoIncident,

            // Unknown enum values can only arrive from a newer panel firmware; ignoring them is safer
            // than raising a false alarm, and the raw event stays in the ingest store for analysis.
            _ => RuleOutcome.NoIncident
        };
    }

    /// <summary>
    /// Builds a readable headline, using the zone number when the panel reported one.
    /// </summary>
    /// <param name="prefix">Human readable action, for example "Alarm triggered".</param>
    /// <param name="deviceEvent">Source event.</param>
    private static string BuildTitle(string prefix, DeviceEventReceivedV1 deviceEvent)
    {
        if (deviceEvent.Payload.TryGetValue(ZonePayloadKey, out string? zone) && !string.IsNullOrWhiteSpace(zone))
        {
            return $"{prefix} (zone {zone})";
        }

        return prefix;
    }
}
