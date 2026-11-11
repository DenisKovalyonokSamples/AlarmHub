namespace AlarmHub.Ingest.Infrastructure;

/// <summary>
/// Read-only view on the panel registry owned by the API service.
/// </summary>
/// <remarks>
/// Device credentials live in exactly one place (the API database). Ingest asks the API whether a
/// key is valid instead of keeping a second copy of the hashes, which would drift on key rotation.
/// </remarks>
public interface IPanelDirectory
{
    /// <summary>
    /// Verifies that the supplied key belongs to the supplied panel.
    /// </summary>
    /// <param name="panelId">Panel the telemetry claims to come from.</param>
    /// <param name="apiKey">Plaintext key taken from the <c>X-Panel-Key</c> header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the API confirms the credential.</returns>
    Task<bool> VerifyPanelKeyAsync(Guid panelId, string apiKey, CancellationToken cancellationToken);
}
