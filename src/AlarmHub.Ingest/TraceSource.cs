using System.Diagnostics;

namespace AlarmHub.Ingest;

/// <summary>
/// Activity source for the ingest pipeline.
/// </summary>
/// <remarks>
/// The outbox dispatcher starts a producer activity from the context stored on the outbox row,
/// which is what keeps one trace across the database write and the later broker publish.
/// </remarks>
internal static class TraceSource
{
    /// <summary>Name registered with OpenTelemetry in <c>Program.cs</c>.</summary>
    public const string Name = "AlarmHub.Ingest.Trace";

    /// <summary>Shared activity source.</summary>
    public static readonly ActivitySource Instance = new(Name);
}
