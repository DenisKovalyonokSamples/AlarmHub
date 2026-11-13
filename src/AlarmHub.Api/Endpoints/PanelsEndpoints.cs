using AlarmHub.Api.Domain.Panels;
using AlarmHub.Api.Domain.Sensors;
using AlarmHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Api.Endpoints;

/// <summary>
/// Panel endpoints: register hardware, issue credentials and add sensors.
/// </summary>
internal static class PanelsEndpoints
{
    /// <summary>Registers the <c>/api/panels</c> route group.</summary>
    /// <param name="app">Application builder.</param>
    public static IEndpointRouteBuilder MapPanelsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/panels").WithTags("Panels");

        group.MapPost("/", RegisterPanelAsync)
            .WithName("RegisterPanel")
            .WithSummary("Registers a panel and returns its API key exactly once")
            .Produces<RegisteredPanelResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("alarms.write");

        group.MapPost("/{panelId:guid}/sensors", AddSensorAsync)
            .WithName("AddSensor")
            .WithSummary("Adds a sensor zone to a panel")
            .Produces<SensorResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("alarms.write");

        group.MapGet("/{id:guid}", GetPanelAsync)
            .WithName("GetPanel")
            .WithSummary("Returns a panel with its sensors")
            .Produces<PanelDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("alarms.read");

        return app;
    }

    /// <summary>Registers a panel at a site.</summary>
    private static async Task<Results<Created<RegisteredPanelResponse>, ValidationProblem>> RegisterPanelAsync(
        RegisterPanelRequest request,
        AlarmHubDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        bool siteExists = await dbContext.Sites
            .AnyAsync(site => site.Id == request.SiteId, cancellationToken);

        if (!siteExists)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["siteId"] = ["Site does not exist."]
            });
        }

        bool serialTaken = await dbContext.Panels
            .AnyAsync(panel => panel.SerialNumber == request.SerialNumber, cancellationToken);

        if (serialTaken)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["serialNumber"] = ["A panel with this serial number is already registered."]
            });
        }

        AlarmPanel panel = AlarmPanel.Register(
            request.SiteId,
            request.SerialNumber,
            request.Model,
            timeProvider.GetUtcNow(),
            out string plaintextApiKey);

        dbContext.Panels.Add(panel);
        await dbContext.SaveChangesAsync(cancellationToken);

        // The plaintext key is returned once and never persisted: it is the only moment
        // the installer can hand it to the device.
        RegisteredPanelResponse response = new(
            panel.Id,
            panel.SiteId,
            panel.SerialNumber,
            panel.Model,
            plaintextApiKey);

        return TypedResults.Created($"/api/panels/{panel.Id}", response);
    }

    /// <summary>Adds a sensor zone to an existing panel.</summary>
    private static async Task<Results<Created<SensorResponse>, ValidationProblem>> AddSensorAsync(
        Guid panelId,
        AddSensorRequest request,
        AlarmHubDbContext dbContext,
        CancellationToken cancellationToken)
    {
        bool panelExists = await dbContext.Panels
            .AnyAsync(panel => panel.Id == panelId, cancellationToken);

        if (!panelExists)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["panelId"] = ["Panel does not exist."]
            });
        }

        bool zoneTaken = await dbContext.Sensors
            .AnyAsync(sensor => sensor.PanelId == panelId && sensor.ZoneNumber == request.ZoneNumber, cancellationToken);

        if (zoneTaken)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["zoneNumber"] = ["This zone is already configured on the panel."]
            });
        }

        Sensor sensor = Sensor.Create(panelId, request.ZoneNumber, request.Type, request.Name);

        dbContext.Sensors.Add(sensor);
        await dbContext.SaveChangesAsync(cancellationToken);

        SensorResponse response = new(
            sensor.Id,
            sensor.PanelId,
            sensor.ZoneNumber,
            sensor.Type.ToString(),
            sensor.Name,
            sensor.IsActive);

        return TypedResults.Created($"/api/panels/{panelId}/sensors/{sensor.Id}", response);
    }

    /// <summary>Returns a panel with its sensors.</summary>
    private static async Task<Results<Ok<PanelDetailsResponse>, NotFound>> GetPanelAsync(
        Guid id,
        AlarmHubDbContext dbContext,
        CancellationToken cancellationToken)
    {
        AlarmPanel? panel = await dbContext.Panels
            .AsNoTracking()
            .Include(p => p.Sensors)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (panel is null)
        {
            return TypedResults.NotFound();
        }

        PanelDetailsResponse response = new(
            panel.Id,
            panel.SiteId,
            panel.SerialNumber,
            panel.Model,
            panel.Status.ToString(),
            panel.LastSeenAt,
            panel.Sensors
                .Select(sensor => new SensorResponse(
                    sensor.Id,
                    sensor.PanelId,
                    sensor.ZoneNumber,
                    sensor.Type.ToString(),
                    sensor.Name,
                    sensor.IsActive))
                .ToList());

        return TypedResults.Ok(response);
    }

    /// <summary>Payload accepted by <c>POST /api/panels</c>.</summary>
    internal sealed record RegisterPanelRequest(Guid SiteId, string SerialNumber, string Model);

    /// <summary>Registration result; contains the one-time API key.</summary>
    internal sealed record RegisteredPanelResponse(
        Guid Id,
        Guid SiteId,
        string SerialNumber,
        string Model,
        string ApiKey);

    /// <summary>Payload accepted by <c>POST /api/panels/{panelId}/sensors</c>.</summary>
    internal sealed record AddSensorRequest(int ZoneNumber, SensorType Type, string Name);

    /// <summary>Sensor representation.</summary>
    internal sealed record SensorResponse(
        Guid Id,
        Guid PanelId,
        int ZoneNumber,
        string Type,
        string Name,
        bool IsActive);

    /// <summary>Panel representation with its sensors.</summary>
    internal sealed record PanelDetailsResponse(
        Guid Id,
        Guid SiteId,
        string SerialNumber,
        string Model,
        string Status,
        DateTimeOffset? LastSeenAt,
        IReadOnlyCollection<SensorResponse> Sensors);
}
