namespace AlarmHub.Api.Domain.Sensors;

/// <summary>
/// Sensor wired to a panel, identified by the zone number reported in telemetry.
/// </summary>
/// <remarks>
/// Zone numbers are assigned by the installer and are the only sensor identifier that appears
/// in panel telemetry, so the pair (panel, zone) must stay stable for the lifetime of the device.
/// </remarks>
public sealed class Sensor
{
    /// <summary>Database identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Panel the sensor is wired to.</summary>
    public Guid PanelId { get; private set; }

    /// <summary>Zone number as configured on the panel.</summary>
    public int ZoneNumber { get; private set; }

    /// <summary>Physical sensor type.</summary>
    public SensorType Type { get; private set; }

    /// <summary>Operator-facing name, for example "Kitchen window".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>False when the zone was bypassed or the sensor was removed.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Required by EF Core.</summary>
    private Sensor()
    {
    }

    /// <summary>
    /// Creates a sensor definition for a panel.
    /// </summary>
    /// <param name="panelId">Panel the sensor belongs to.</param>
    /// <param name="zoneNumber">Zone number configured on the panel; must be positive.</param>
    /// <param name="type">Physical sensor type.</param>
    /// <param name="name">Operator-facing name.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the zone number is not positive.</exception>
    public static Sensor Create(Guid panelId, int zoneNumber, SensorType type, string name)
    {
        if (zoneNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoneNumber), "Zone numbers start at 1.");
        }

        return new Sensor
        {
            Id = Guid.NewGuid(),
            PanelId = panelId,
            ZoneNumber = zoneNumber,
            Type = type,
            Name = string.IsNullOrWhiteSpace(name) ? $"Zone {zoneNumber}" : name.Trim(),
            IsActive = true
        };
    }

    /// <summary>Bypasses the sensor without deleting its configuration.</summary>
    public void Deactivate() => IsActive = false;
}
