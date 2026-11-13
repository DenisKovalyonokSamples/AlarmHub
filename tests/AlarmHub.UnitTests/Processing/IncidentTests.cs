using AlarmHub.Contracts.Incidents;
using AlarmHub.Processing.Domain;

namespace AlarmHub.UnitTests.Processing;

/// <summary>
/// Lifecycle invariants of the incident aggregate.
/// </summary>
public sealed class IncidentTests
{
    private static Incident OpenIncident() => Incident.Open(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        IncidentSeverity.Alarm,
        "Alarm triggered (zone 1)",
        DateTimeOffset.UtcNow);

    /// <summary>A new incident always starts open.</summary>
    [Fact]
    public void Open_CreatesOpenIncident()
    {
        Incident incident = OpenIncident();

        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.NotEqual(Guid.Empty, incident.Id);
        Assert.Equal("Alarm triggered (zone 1)", incident.Title);
    }

    /// <summary>Opening without a site is rejected: an incident must belong to a protected location.</summary>
    [Fact]
    public void Open_WithoutSite_Throws()
    {
        Assert.Throws<ArgumentException>(() => Incident.Open(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            IncidentSeverity.Alarm,
            "x",
            DateTimeOffset.UtcNow));
    }

    /// <summary>Operator picked the incident up.</summary>
    [Fact]
    public void Acknowledge_MovesToAcknowledged()
    {
        Incident incident = OpenIncident();

        incident.Acknowledge();

        Assert.Equal(IncidentStatus.Acknowledged, incident.Status);
    }

    /// <summary>Acknowledging twice is a state-machine violation, not a silent no-op.</summary>
    [Fact]
    public void Acknowledge_Twice_Throws()
    {
        Incident incident = OpenIncident();
        incident.Acknowledge();

        Assert.Throws<InvalidOperationException>(() => incident.Acknowledge());
    }

    /// <summary>Closing is allowed once and only once.</summary>
    [Fact]
    public void Close_Twice_Throws()
    {
        Incident incident = OpenIncident();
        incident.Close();

        Assert.Equal(IncidentStatus.Closed, incident.Status);
        Assert.Throws<InvalidOperationException>(() => incident.Close());
    }
}
