using System.Net;
using System.Net.Http.Json;

namespace AlarmHub.Processing.Infrastructure;

/// <summary>
/// Minimal view of a panel needed by processing: which site it protects.
/// </summary>
/// <param name="PanelId">Panel identifier.</param>
/// <param name="SiteId">Site the panel is installed at.</param>
/// <param name="SerialNumber">Manufacturer serial number.</param>
public sealed record PanelInfo(Guid PanelId, Guid SiteId, string SerialNumber);

/// <summary>
/// Resolves panel metadata through the API's internal directory endpoint.
/// </summary>
/// <remarks>
/// Processing must not keep a copy of the panel registry: a second copy would drift every time
/// hardware is moved between sites. A tiny read-through call is cheaper than that risk.
/// </remarks>
public interface IPanelDirectory
{
    /// <summary>
    /// Looks up a panel by identifier.
    /// </summary>
    /// <param name="panelId">Panel identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Panel information, or null when the panel is unknown.</returns>
    Task<PanelInfo?> FindAsync(Guid panelId, CancellationToken cancellationToken);
}

/// <summary>
/// HTTP implementation of <see cref="IPanelDirectory"/>.
/// </summary>
/// <param name="httpClient">Client configured with the API base address.</param>
public sealed class HttpPanelDirectory(HttpClient httpClient) : IPanelDirectory
{
    /// <inheritdoc />
    public async Task<PanelInfo?> FindAsync(Guid panelId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient
            .GetAsync($"/internal/panels/{panelId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        PanelResponse? panel = await response.Content
            .ReadFromJsonAsync<PanelResponse>(cancellationToken);

        return panel is null ? null : new PanelInfo(panel.Id, panel.SiteId, panel.SerialNumber);
    }

    /// <summary>Shape returned by the API internal endpoint.</summary>
    /// <param name="Id">Panel identifier.</param>
    /// <param name="SiteId">Owning site.</param>
    /// <param name="SerialNumber">Manufacturer serial number.</param>
    private sealed record PanelResponse(Guid Id, Guid SiteId, string SerialNumber);
}
