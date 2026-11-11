using AlarmHub.Api.Domain.Sites;

namespace AlarmHub.Api.Domain.Customers;

/// <summary>
/// A paying customer of the alarm service. A customer may own several protected sites.
/// </summary>
/// <remarks>
/// The entity is intentionally anaemic on behaviour at this stage: the interesting invariants
/// (for example "a customer cannot be deactivated while an alarm is open") belong to the
/// application layer and will be added together with the incident workflow.
/// </remarks>
public sealed class Customer
{
    /// <summary>Database identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Display name shown to operators and in notifications.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Primary contact e-mail, used for alarm notifications.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Moment the customer was registered, in UTC.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>False when the customer cancelled the service; historical data is kept.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Protected sites owned by this customer.</summary>
    public IReadOnlyCollection<Site> Sites => _sites;

    private readonly List<Site> _sites = [];

    /// <summary>Required by EF Core.</summary>
    private Customer()
    {
    }

    /// <summary>
    /// Creates a new active customer.
    /// </summary>
    /// <param name="name">Display name; must not be blank.</param>
    /// <param name="email">Contact e-mail; must contain an at sign.</param>
    /// <param name="now">Current UTC timestamp, injected to keep the entity deterministic in tests.</param>
    /// <exception cref="ArgumentException">Thrown when the name or the e-mail is invalid.</exception>
    public static Customer Create(string name, string email, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Customer name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("A valid e-mail is required.", nameof(email));
        }

        return new Customer
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            CreatedAt = now,
            IsActive = true
        };
    }

    /// <summary>Marks the customer as cancelled without deleting the aggregate.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Adds a protected site to this customer.</summary>
    /// <param name="site">Site created through <see cref="Site.Create"/>.</param>
    public void AddSite(Site site)
    {
        ArgumentNullException.ThrowIfNull(site);

        if (site.CustomerId != Id)
        {
            throw new InvalidOperationException("The site belongs to another customer.");
        }

        _sites.Add(site);
    }
}
