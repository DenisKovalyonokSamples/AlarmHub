using AlarmHub.Contracts.DeviceEvents;
using AlarmHub.Contracts.Incidents;
using AlarmHub.Processing.Domain;

namespace AlarmHub.UnitTests.Processing;

/// <summary>
/// Business rules that decide whether panel telemetry becomes an incident.
/// </summary>
/// <remarks>
/// These tests are the specification of the alarm policy: if one of them changes, the product
/// behaviour changes and an operator will notice. They use no database and no broker.
/// </remarks>
public sealed class IncidentRuleEngineTests
{
    private static DeviceEventReceivedV1 CreateEvent(DeviceEventType type, string? zone = null)
    {
        Dictionary<string, string> payload = zone is null
            ? []
            : new Dictionary<string, string> { ["zone"] = zone };

        return new DeviceEventReceivedV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            type,
            SequenceNumber: 1,
            payload,
            DateTimeOffset.UtcNow);
    }

    /// <summary>An alarm raised while armed is always an incident.</summary>
    [Fact]
    public void Evaluate_AlarmTriggered_OpensAlarmIncident()
    {
        RuleOutcome outcome = IncidentRuleEngine.Evaluate(CreateEvent(DeviceEventType.AlarmTriggered));

        Assert.True(outcome.ShouldOpenIncident);
        Assert.Equal(IncidentSeverity.Alarm, outcome.Severity);
        Assert.Equal("Alarm triggered", outcome.Title);
    }

    /// <summary>The zone from the payload is included so the operator knows where to look.</summary>
    [Fact]
    public void Evaluate_AlarmWithZone_IncludesZoneInTitle()
    {
        RuleOutcome outcome = IncidentRuleEngine.Evaluate(CreateEvent(DeviceEventType.AlarmTriggered, "3"));

        Assert.Equal("Alarm triggered (zone 3)", outcome.Title);
    }

    /// <summary>Tampering is actionable but less urgent than a live alarm.</summary>
    [Fact]
    public void Evaluate_TamperDetected_OpensWarning()
    {
        RuleOutcome outcome = IncidentRuleEngine.Evaluate(CreateEvent(DeviceEventType.TamperDetected));

        Assert.True(outcome.ShouldOpenIncident);
        Assert.Equal(IncidentSeverity.Warning, outcome.Severity);
    }

    /// <summary>A technical fault leaves the site unprotected, so it must be actionable.</summary>
    [Fact]
    public void Evaluate_FaultReported_OpensWarning()
    {
        RuleOutcome outcome = IncidentRuleEngine.Evaluate(CreateEvent(DeviceEventType.FaultReported));

        Assert.True(outcome.ShouldOpenIncident);
        Assert.Equal(IncidentSeverity.Warning, outcome.Severity);
    }

    /// <summary>State changes and keep-alives are not incidents.</summary>
    [Theory]
    [InlineData(DeviceEventType.Armed)]
    [InlineData(DeviceEventType.Disarmed)]
    [InlineData(DeviceEventType.Heartbeat)]
    public void Evaluate_RoutineTelemetry_DoesNotOpenIncident(DeviceEventType type)
    {
        RuleOutcome outcome = IncidentRuleEngine.Evaluate(CreateEvent(type));

        Assert.False(outcome.ShouldOpenIncident);
    }

    /// <summary>An unknown value can only come from newer firmware; do not raise a false alarm.</summary>
    [Fact]
    public void Evaluate_UnknownType_DoesNotOpenIncident()
    {
        DeviceEventReceivedV1 deviceEvent = CreateEvent((DeviceEventType)999);

        RuleOutcome outcome = IncidentRuleEngine.Evaluate(deviceEvent);

        Assert.False(outcome.ShouldOpenIncident);
    }

    /// <summary>Null input is a programming error and must fail fast.</summary>
    [Fact]
    public void Evaluate_NullEvent_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => IncidentRuleEngine.Evaluate(null!));
    }
}
