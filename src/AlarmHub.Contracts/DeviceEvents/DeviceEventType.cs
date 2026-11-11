namespace AlarmHub.Contracts.DeviceEvents;

/// <summary>
/// Kind of telemetry an alarm panel reports to the platform.
/// </summary>
/// <remarks>
/// Values are serialised as strings on the wire, so new members can be added without breaking
/// consumers that were deployed earlier. Never renumber existing members.
/// </remarks>
public enum DeviceEventType
{
    /// <summary>The panel was armed by a user, a schedule or a mobile app.</summary>
    Armed = 1,

    /// <summary>The panel was disarmed, which cancels any entry delay in progress.</summary>
    Disarmed = 2,

    /// <summary>A sensor was triggered while the panel was armed.</summary>
    AlarmTriggered = 3,

    /// <summary>The enclosure was opened or a sensor reported tampering.</summary>
    TamperDetected = 4,

    /// <summary>Periodic keep-alive used to detect panels that went offline.</summary>
    Heartbeat = 5,

    /// <summary>A technical fault was reported (battery, mains power, GSM/LTE link).</summary>
    FaultReported = 6
}
