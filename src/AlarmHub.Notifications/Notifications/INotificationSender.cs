namespace AlarmHub.Notifications.Notifications;

/// <summary>
/// Abstraction over the delivery provider (e-mail today, push/SMS later).
/// </summary>
/// <remarks>
/// Keeping the provider behind an interface means the consumer and its retry behaviour can be
/// tested without sending real messages.
/// </remarks>
public interface INotificationSender
{
    /// <summary>
    /// Sends one notification.
    /// </summary>
    /// <param name="recipient">E-mail address or device token.</param>
    /// <param name="subject">Subject line.</param>
    /// <param name="body">Plain-text body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken);
}

/// <summary>
/// Development sender that writes the message to the log instead of sending it.
/// </summary>
/// <remarks>
/// Used until a real provider (ACS Email, SendGrid, …) is configured. The log line contains the full
/// message so the pipeline can be demonstrated end to end without an external account.
/// </remarks>
/// <param name="logger">Logger.</param>
public sealed class LogNotificationSender(ILogger<LogNotificationSender> logger) : INotificationSender
{
    /// <inheritdoc />
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        NotificationsLog.NotificationDelivered(logger, recipient, subject, body);
        return Task.CompletedTask;
    }
}
