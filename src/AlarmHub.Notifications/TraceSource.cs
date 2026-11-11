using System.Diagnostics;

namespace AlarmHub.Notifications;

/// <summary>
/// Activity source for the notification pipeline.
/// </summary>
/// <remarks>
/// Registered with OpenTelemetry in <c>Program.cs</c>; the consumer starts its activity with the
/// producer's <c>traceparent</c> as parent so the alert stays inside the original trace.
/// </remarks>
internal static class TraceSource
{
    /// <summary>Name registered with OpenTelemetry in <c>Program.cs</c>.</summary>
    public const string Name = "AlarmHub.Notifications.Trace";

    /// <summary>Shared activity source.</summary>
    public static readonly ActivitySource Instance = new(Name);
}
