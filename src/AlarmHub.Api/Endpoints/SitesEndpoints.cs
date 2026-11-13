using AlarmHub.Api.Domain.Sites;
using AlarmHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Api.Endpoints;

/// <summary>
/// Site endpoints: create a protected location and read its panels.
/// </summary>
internal static class SitesEndpoints
{
    /// <summary>Registers the <c>/api/sites</c> route group.</summary>
    /// <param name="app">Application builder.</param>
    public static IEndpointRouteBuilder MapSitesEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/sites").WithTags("Sites");

        group.MapPost("/", CreateSiteAsync)
            .WithName("CreateSite")
            .WithSummary("Creates a protected site for an existing customer")
            .Produces<SiteResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization("alarms.write");

        group.MapGet("/{id:guid}", GetSiteAsync)
            .WithName("GetSite")
            .WithSummary("Returns a site with its panels")
            .Produces<SiteDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("alarms.read");

        return app;
    }

    /// <summary>Creates a site.</summary>
    private static async Task<Results<Created<SiteResponse>, ValidationProblem>> CreateSiteAsync(
        CreateSiteRequest request,
        AlarmHubDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        bool customerExists = await dbContext.Customers
            .AnyAsync(customer => customer.Id == request.CustomerId, cancellationToken);

        if (!customerExists)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["customerId"] = ["Customer does not exist."]
            });
        }

        Address address = new(request.Street, request.City, request.PostalCode, request.CountryCode);

        Site site = Site.Create(
            request.CustomerId,
            request.Name,
            address,
            request.TimeZoneId,
            timeProvider.GetUtcNow());

        dbContext.Sites.Add(site);
        await dbContext.SaveChangesAsync(cancellationToken);

        SiteResponse response = new(
            site.Id,
            site.CustomerId,
            site.Name,
            site.Address.ToString(),
            site.TimeZoneId);

        return TypedResults.Created($"/api/sites/{site.Id}", response);
    }

    /// <summary>Returns a site with its panels.</summary>
    private static async Task<Results<Ok<SiteDetailsResponse>, NotFound>> GetSiteAsync(
        Guid id,
        AlarmHubDbContext dbContext,
        CancellationToken cancellationToken)
    {
        Site? site = await dbContext.Sites
            .AsNoTracking()
            .Include(s => s.Panels)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (site is null)
        {
            return TypedResults.NotFound();
        }

        SiteDetailsResponse response = new(
            site.Id,
            site.CustomerId,
            site.Name,
            site.Address.ToString(),
            site.TimeZoneId,
            site.Panels
                .Select(panel => new PanelSummaryResponse(
                    panel.Id,
                    panel.SerialNumber,
                    panel.Model,
                    panel.Status.ToString(),
                    panel.LastSeenAt))
                .ToList());

        return TypedResults.Ok(response);
    }

    /// <summary>Payload accepted by <c>POST /api/sites</c>.</summary>
    internal sealed record CreateSiteRequest(
        Guid CustomerId,
        string Name,
        string Street,
        string City,
        string PostalCode,
        string CountryCode,
        string TimeZoneId);

    /// <summary>Compact site representation.</summary>
    internal sealed record SiteResponse(
        Guid Id,
        Guid CustomerId,
        string Name,
        string Address,
        string TimeZoneId);

    /// <summary>Site representation with its panels.</summary>
    internal sealed record SiteDetailsResponse(
        Guid Id,
        Guid CustomerId,
        string Name,
        string Address,
        string TimeZoneId,
        IReadOnlyCollection<PanelSummaryResponse> Panels);

    /// <summary>Panel summary embedded in site responses.</summary>
    internal sealed record PanelSummaryResponse(
        Guid Id,
        string SerialNumber,
        string Model,
        string Status,
        DateTimeOffset? LastSeenAt);
}
