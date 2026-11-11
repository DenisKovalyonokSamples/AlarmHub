namespace AlarmHub.Api.Domain.Panels;

/// <summary>
/// Physical alarm panel installed at a site. Panels report telemetry and are addressed by
/// the ingest service, so their identity and credential live here.
/// </summary>
/// <remarks>
/// Registration returns the plaintext API key once; afterwards only the hash is available.
/// Status is a projection of the last heartbeat and is updated by the ingest pipeline.
/// </remarks>
public sealed class AlarmPanel
{
    /// <summary>Database identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Site the panel is installed at.</summary>
    public Guid SiteId { get; private set; }

    /// <summary>Manufacturer serial number, unique across the platform.</summary>
    public string SerialNumber { get; private set; } = string.Empty;

    /// <summary>Hardware model, used to pick a compatible firmware profile.</summary>
    public string Model { get; private set; } = string.Empty;

    /// <summary>Salted hash of the panel API key; the plaintext key is never stored.</summary>
    public string ApiKeyHash { get; private set; } = string.Empty;

    /// <summary>Per-panel salt used when hashing the API key.</summary>
    public string ApiKeySalt { get; private set; } = string.Empty;

    /// <summary>Last observed connectivity state.</summary>
    public PanelStatus Status { get; private set; } = PanelStatus.Unknown;

    /// <summary>Moment of the last accepted heartbeat, in UTC; null when the panel never called.</summary>
    public DateTimeOffset? LastSeenAt { get; private set; }

    /// <summary>Moment the panel was registered, in UTC.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Sensors wired to this panel.</summary>
    public IReadOnlyCollection<Sensors.Sensor> Sensors => _sensors;

    private readonly List<Sensors.Sensor> _sensors = [];

    /// <summary>Required by EF Core.</summary>
    private AlarmPanel()
    {
    }

    /// <summary>
    /// Registers a new panel at a site and issues its API key.
    /// </summary>
    /// <param name="siteId">Site the panel belongs to.</param>
    /// <param name="serialNumber">Manufacturer serial number; must be unique.</param>
    /// <param name="model">Hardware model.</param>
    /// <param name="now">Current UTC timestamp.</param>
    /// <param name="plaintextApiKey">Plaintext key that must be delivered to the device.</param>
    /// <returns>The panel aggregate ready to be persisted.</returns>
    public static AlarmPanel Register(
        Guid siteId,
        string serialNumber,
        string model,
        DateTimeOffset now,
        out string plaintextApiKey)
    {
        if (siteId == Guid.Empty)
        {
            throw new ArgumentException("Site id is required.", nameof(siteId));
        }

        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            throw new ArgumentException("Serial number is required.", nameof(serialNumber));
        }

        (string plaintextKey, string hash, string salt) = PanelApiKeys.Create();
        plaintextApiKey = plaintextKey;

        return new AlarmPanel
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            SerialNumber = serialNumber.Trim(),
            Model = string.IsNullOrWhiteSpace(model) ? "unknown" : model.Trim(),
            ApiKeyHash = hash,
            ApiKeySalt = salt,
            Status = PanelStatus.Unknown,
            CreatedAt = now
        };
    }

    /// <summary>
    /// Records a heartbeat and marks the panel online.
    /// </summary>
    /// <param name="observedAt">Moment the heartbeat was observed.</param>
    public void RecordHeartbeat(DateTimeOffset observedAt)
    {
        // Out-of-order heartbeats are ignored so that a retried message cannot move LastSeenAt backwards.
        if (LastSeenAt is not null && observedAt <= LastSeenAt)
        {
            return;
        }

        LastSeenAt = observedAt;
        Status = PanelStatus.Online;
    }

    /// <summary>Marks the panel offline after the heartbeat threshold elapsed.</summary>
    public void MarkOffline() => Status = PanelStatus.Offline;
}
