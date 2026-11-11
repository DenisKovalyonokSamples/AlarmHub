namespace AlarmHub.Notifications.Domain;

/// <summary>
/// Delivery state of a notification.
/// </summary>
public enum NotificationStatus
{
    /// <summary>Created but not delivered yet.</summary>
    Pending = 1,

    /// <summary>Accepted by the provider.</summary>
    Sent = 2,

    /// <summary>Delivery failed; the record keeps the error for triage.</summary>
    Failed = 3
}
