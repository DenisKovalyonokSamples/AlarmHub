namespace AlarmHub.Notifications.Domain;

/// <summary>
/// One delivery attempt of a notification for an incident.
/// </summary>
/// <remarks>
/// Rows are keyed by incident + channel, which makes the consumer idempotent: a redelivered
/// <c>IncidentOpenedV1</c> finds the existing row and does not notify the customer twice.
/// </remarks>
public sealed class NotificationRecord
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Incident the notification belongs to.</summary>
    public Guid IncidentId { get; private set; }

    /// <summary>Site the incident happened at; used for routing and reporting.</summary>
    public Guid SiteId { get; private set; }

    /// <summary>Delivery channel, for example <c>email</c>.</summary>
    public string Channel { get; private set; } = "email";

    /// <summary>Recipient address or device token.</summary>
    public string Recipient { get; private set; } = string.Empty;

    /// <summary>Delivery state.</summary>
    public NotificationStatus Status { get; private set; } = NotificationStatus.Pending;

    /// <summary>Number of delivery attempts.</summary>
    public int Attempts { get; private set; }

    /// <summary>Moment the notification was accepted by the provider, in UTC.</summary>
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Last error, kept for the operations console.</summary>
    public string? LastError { get; private set; }

    /// <summary>Headline copied from the incident, useful when browsing delivery history.</summary>
    public string Subject { get; private set; } = string.Empty;

    /// <summary>Required by EF Core.</summary>
    private NotificationRecord()
    {
    }

    /// <summary>Creates a pending notification.</summary>
    /// <param name="incidentId">Incident identifier.</param>
    /// <param name="siteId">Site identifier.</param>
    /// <param name="recipient">Recipient address.</param>
    /// <param name="subject">Notification subject.</param>
    public static NotificationRecord Create(Guid incidentId, Guid siteId, string recipient, string subject) => new()
    {
        Id = Guid.NewGuid(),
        IncidentId = incidentId,
        SiteId = siteId,
        Channel = "email",
        Recipient = recipient,
        Subject = subject,
        Status = NotificationStatus.Pending
    };

    /// <summary>Marks the notification as delivered.</summary>
    /// <param name="now">Current UTC timestamp.</param>
    public void MarkSent(DateTimeOffset now)
    {
        Status = NotificationStatus.Sent;
        SentAt = now;
        LastError = null;
    }

    /// <summary>Records a failed attempt so the message can be retried or dead-lettered.</summary>
    /// <param name="error">Failure description.</param>
    public void RegisterFailure(string error)
    {
        Attempts++;
        Status = NotificationStatus.Failed;
        LastError = error.Length > 2000 ? error[..2000] : error;
    }
}
