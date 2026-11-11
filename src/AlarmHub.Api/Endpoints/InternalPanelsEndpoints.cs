using AlarmHub.Api.Domain.Panels;
using AlarmHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.Api.Endpoints;

/// <summary>
/// Internal, service-to-service endpoints. Not exposed through the public gateway.
/// </summary>
/// <remarks>
/// In production these routes sit behind the cluster network policy and an internal service token.
/// They are kept on a separate prefix so the public OpenAPI document can exclude them.
/// </remarks>
internal static class InternalPanelsEndpoints
{
    /// <summary>Registers the <c>/internal/panels</c> route group.</summary>
    /// <param name="app">Application builder.</param>
    public static IEndpointRouteBuilder MapInternalPanelsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/internal/panels")
            .WithTags("Internal")
            .ExcludeFromDescription();

        group.MapPost("/verify", VerifyPanelKeyAsync)
            .WithName("VerifyPanelKey");

        group.MapGet("/{panelId:guid}", GetPanelAsync)
            .WithName("GetPanelDirectoryEntry");

        return app;
    }

    /// <summary>
    /// Returns the minimal panel projection needed by other services (site and serial number).
    /// </summary>
    private static async Task<Results<Ok<PanelDirectoryResponse>, NotFound>> GetPanelAsync(
        Guid panelId,
        AlarmHubDbContext dbContext,
        CancellationToken cancellationToken)
    {
        PanelDirectoryResponse? panel = await dbContext.Panels
            .AsNoTracking()
            .Where(candidate => candidate.Id == panelId)
            .Select(candidate => new PanelDirectoryResponse(
                candidate.Id,
                candidate.SiteId,
                candidate.SerialNumber))
            .SingleOrDefaultAsync(cancellationToken);

        return panel is null ? TypedResults.NotFound() : TypedResults.Ok(panel);
    }

    /// <summary>Panel projection exposed to other services.</summary>
    /// <param name="Id">Panel identifier.</param>
    /// <param name="SiteId">Owning site.</param>
    /// <param name="SerialNumber">Manufacturer serial number.</param>
    internal sealed record PanelDirectoryResponse(Guid Id, Guid SiteId, string SerialNumber);

    /// <summary>
    /// Checks whether the presented key belongs to the panel. The ingest service calls this on every batch.
    /// </summary>
    private static async Task<Results<Ok<VerifyPanelKeyResponse>, NotFound>> VerifyPanelKeyAsync(
        VerifyPanelKeyRequest request,
        AlarmHubDbContext dbContext,
        CancellationToken cancellationToken)
    {
        AlarmPanel? panel = await dbContext.Panels
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.PanelId, cancellationToken);

        if (panel is null)
        {
            return TypedResults.NotFound();
        }

        bool isValid = PanelApiKeys.Verify(request.ApiKey, panel.ApiKeyHash, panel.ApiKeySalt);

        return TypedResults.Ok(new VerifyPanelKeyResponse(isValid));
    }

    /// <summary>Request body for the verification endpoint.</summary>
    /// <param name="PanelId">Panel identifier.</param>
    /// <param name="ApiKey">Plaintext key presented by the device.</param>
    internal sealed record VerifyPanelKeyRequest(Guid PanelId, string ApiKey);

    /// <summary>Verification result.</summary>
    /// <param name="IsValid">True when the key matches the stored hash.</param>
    internal sealed record VerifyPanelKeyResponse(bool IsValid);
}
