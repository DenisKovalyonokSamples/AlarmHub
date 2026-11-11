using System.Text.Json;
using AlarmHub.Contracts.DeviceEvents;
using AlarmHub.Ingest.Domain;
using AlarmHub.Ingest.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Ingest.Endpoints;

/// <summary>
/// Telemetry ingestion endpoint used by physical alarm panels.
/// </summary>
internal static class DeviceEventsEndpoints
{
    /// <summary>Header carrying the panel API key issued at registration.</summary>
    private const string PanelKeyHeader = "X-Panel-Key";

    /// <summary>Registers the <c>/device-events</c> route.</summary>
    /// <param name="app">Application builder.</param>
    public static IEndpointRouteBuilder MapDeviceEventsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/device-events", IngestAsync)
            .WithTags("Device events")
            .WithName("IngestDeviceEvent")
            .WithSummary("Accepts one telemetry event from an alarm panel")
            .Produces<IngestResultResponse>(StatusCodes.Status202Accepted)
            .Produces<IngestResultResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        return app;
    }

    /// <summary>
    /// Validates the panel, stores the event and enqueues an outbox message in one transaction.
    /// </summary>
    private static async Task<Results<Accepted<IngestResultResponse>, Ok<IngestResultResponse>, UnauthorizedHttpResult, ValidationProblem>>
        IngestAsync(
            HttpContext httpContext,
            IngestDeviceEventRequest request,
            IPanelDirectory panelDirectory,
            IngestDbContext dbContext,
            TimeProvider timeProvider,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger("AlarmHub.Ingest.DeviceEvents");

        string? apiKey = httpContext.Request.Headers[PanelKeyHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return TypedResults.Unauthorized();
        }

        bool panelKnown = await panelDirectory.VerifyPanelKeyAsync(request.PanelId, apiKey, cancellationToken);

        if (!panelKnown)
        {
            // Unknown panel or wrong key both return 401: the caller must not learn which one failed.
            IngestLog.InvalidCredential(logger, request.PanelId);
            return TypedResults.Unauthorized();
        }

        if (!Enum.IsDefined(request.Type))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["type"] = ["Unknown device event type."]
            });
        }

        bool duplicate = await dbContext.DeviceEvents
            .AnyAsync(record => record.EventId == request.EventId, cancellationToken);

        if (duplicate)
        {
            // Panels retry aggressively; a duplicate is a success, not an error.
            IngestLog.DuplicateIgnored(logger, request.EventId, request.PanelId);
            return TypedResults.Ok(new IngestResultResponse(request.EventId, "duplicate"));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        string payloadJson = JsonSerializer.Serialize(request.Payload);

        DeviceEventRecord record = DeviceEventRecord.Create(
            request.EventId,
            request.PanelId,
            request.OccurredAt,
            request.Type.ToString(),
            request.SequenceNumber,
            payloadJson,
            now);

        DeviceEventReceivedV1 integrationEvent = new(
            request.EventId,
            request.PanelId,
            request.OccurredAt,
            request.Type,
            request.SequenceNumber,
            request.Payload,
            now);

        OutboxMessage outbox = OutboxMessage.Create(
            nameof(DeviceEventReceivedV1),
            JsonSerializer.Serialize(integrationEvent),
            now);

        // One SaveChanges call = one transaction: the event and its outbox row are atomic.
        dbContext.DeviceEvents.Add(record);
        dbContext.OutboxMessages.Add(outbox);
        await dbContext.SaveChangesAsync(cancellationToken);

        IngestLog.EventAccepted(logger, request.EventId, request.Type, request.PanelId);

        return TypedResults.Accepted($"/device-events/{request.EventId}", new IngestResultResponse(request.EventId, "accepted"));
    }

    /// <summary>Telemetry payload posted by a panel.</summary>
    /// <param name="EventId">Panel-generated identifier; used for idempotency.</param>
    /// <param name="PanelId">Panel identifier.</param>
    /// <param name="OccurredAt">Moment the panel recorded the event.</param>
    /// <param name="Type">Event type.</param>
    /// <param name="SequenceNumber">Per-panel sequence number.</param>
    /// <param name="Payload">Free-form attributes (zone, battery, signal strength, ...).</param>
    internal sealed record IngestDeviceEventRequest(
        Guid EventId,
        Guid PanelId,
        DateTimeOffset OccurredAt,
        DeviceEventType Type,
        long SequenceNumber,
        Dictionary<string, string> Payload);

    /// <summary>Result returned to the panel.</summary>
    /// <param name="EventId">Identifier that was processed.</param>
    /// <param name="Status">"accepted" or "duplicate".</param>
    internal sealed record IngestResultResponse(Guid EventId, string Status);
}
