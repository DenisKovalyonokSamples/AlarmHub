using System.Diagnostics;

namespace AlarmHub.Processing;

/// <summary>
/// Activity source for the processing pipeline.
/// </summary>
/// <remarks>
/// A dedicated source (instead of reusing the Azure SDK one) lets the service create an explicit
/// consumer activity whose parent is the producer's <c>traceparent</c>. That is what keeps a single
/// trace across the broker hop.
/// </remarks>
internal static class TraceSource
{
    /// <summary>Name registered with OpenTelemetry in <c>Program.cs</c>.</summary>
    public const string Name = "AlarmHub.Processing.Trace";

    /// <summary>Shared activity source.</summary>
    public static readonly ActivitySource Instance = new(Name);
}
