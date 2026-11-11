using System.Net;
using System.Net.Http.Json;

namespace AlarmHub.Ingest.Infrastructure;

/// <summary>
/// HTTP client that asks the API service to validate panel credentials.
/// </summary>
/// <remarks>
/// The call is deliberately small and cacheable later; at current volumes one extra round trip per
/// batch of telemetry is cheaper than duplicating panel secrets across services.
/// </remarks>
/// <param name="httpClient">Configured client with the API base address.</param>
public sealed class HttpPanelDirectory(HttpClient httpClient) : IPanelDirectory
{
    /// <inheritdoc />
    public async Task<bool> VerifyPanelKeyAsync(Guid panelId, string apiKey, CancellationToken cancellationToken)
    {
        VerifyPanelKeyRequest request = new(panelId, apiKey);

        using HttpResponseMessage response = await httpClient
            .PostAsJsonAsync("/internal/panels/verify", request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        VerifyPanelKeyResponse? result = await response.Content
            .ReadFromJsonAsync<VerifyPanelKeyResponse>(cancellationToken);

        return result?.IsValid ?? false;
    }

    /// <summary>Request body sent to the API.</summary>
    /// <param name="PanelId">Panel identifier.</param>
    /// <param name="ApiKey">Plaintext key presented by the device.</param>
    private sealed record VerifyPanelKeyRequest(Guid PanelId, string ApiKey);

    /// <summary>Response returned by the API.</summary>
    /// <param name="IsValid">True when the key matches the stored hash.</param>
    private sealed record VerifyPanelKeyResponse(bool IsValid);
}
