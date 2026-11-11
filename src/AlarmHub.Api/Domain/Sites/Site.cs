namespace AlarmHub.Api.Domain.Sites;

/// <summary>
/// A protected location (home or business) that owns alarm panels and sensors.
/// </summary>
/// <remarks>
/// The site is the unit operators reason about: incidents, notification rules and
/// arm/disarm state all hang off a site.
/// </remarks>
public sealed class Site
{
    /// <summary>Database identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owner of the site.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Human readable name, for example "Home - Fuengirola".</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Postal address used by installers and emergency services.</summary>
    public Address Address { get; private set; } = new(string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>IANA time zone identifier; schedules and quiet hours are evaluated in this zone.</summary>
    public string TimeZoneId { get; private set; } = "Europe/Madrid";

    /// <summary>Moment the site was created, in UTC.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Alarm panels installed at this site.</summary>
    public IReadOnlyCollection<Panels.AlarmPanel> Panels => _panels;

    private readonly List<Panels.AlarmPanel> _panels = [];

    /// <summary>Required by EF Core.</summary>
    private Site()
    {
    }

    /// <summary>
    /// Creates a site for an existing customer.
    /// </summary>
    /// <param name="customerId">Owner identifier.</param>
    /// <param name="name">Display name; must not be blank.</param>
    /// <param name="address">Postal address.</param>
    /// <param name="timeZoneId">IANA time zone; defaults to Central European time.</param>
    /// <param name="now">Current UTC timestamp.</param>
    /// <exception cref="ArgumentException">Thrown when the name or the time zone is invalid.</exception>
    public static Site Create(
        Guid customerId,
        string name,
        Address address,
        string timeZoneId,
        DateTimeOffset now)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Site name is required.", nameof(name));
        }

        // Validating the time zone at creation time prevents a bad value from breaking
        // scheduling later, when the site already has panels attached.
        if (string.IsNullOrWhiteSpace(timeZoneId) || !TryResolveTimeZone(timeZoneId))
        {
            throw new ArgumentException("Unknown IANA time zone.", nameof(timeZoneId));
        }

        return new Site
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Name = name.Trim(),
            Address = address,
            TimeZoneId = timeZoneId,
            CreatedAt = now
        };
    }

    /// <summary>Installs a panel at this site.</summary>
    /// <param name="panel">Panel created through <see cref="Panels.AlarmPanel.Register"/>.</param>
    public void InstallPanel(Panels.AlarmPanel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        if (panel.SiteId != Id)
        {
            throw new InvalidOperationException("The panel belongs to another site.");
        }

        _panels.Add(panel);
    }

    /// <summary>
    /// Returns true when the runtime knows the supplied IANA time zone.
    /// </summary>
    /// <param name="timeZoneId">IANA identifier, for example "Europe/Madrid".</param>
    private static bool TryResolveTimeZone(string timeZoneId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
