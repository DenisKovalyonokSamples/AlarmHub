namespace AlarmHub.Api.Domain.Panels;

/// <summary>
/// Connectivity state of an alarm panel as last observed by the platform.
/// </summary>
public enum PanelStatus
{
    /// <summary>No heartbeat received yet; the panel was registered but never contacted us.</summary>
    Unknown = 0,

    /// <summary>Heartbeats are arriving within the expected interval.</summary>
    Online = 1,

    /// <summary>No heartbeat within the offline threshold; the site may be unprotected.</summary>
    Offline = 2
}
