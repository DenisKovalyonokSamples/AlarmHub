namespace AlarmHub.Api.Domain.Sensors;

/// <summary>
/// Kind of physical sensor wired to a panel.
/// </summary>
public enum SensorType
{
    /// <summary>Magnetic contact on a door or window.</summary>
    Contact = 1,

    /// <summary>Passive infrared motion detector.</summary>
    Motion = 2,

    /// <summary>Glass-break detector.</summary>
    GlassBreak = 3,

    /// <summary>Smoke detector.</summary>
    Smoke = 4,

    /// <summary>Water-leak detector.</summary>
    WaterLeak = 5,

    /// <summary>Panic button.</summary>
    Panic = 6
}
