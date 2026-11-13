using AlarmHub.Api.Domain.Customers;
using AlarmHub.Api.Domain.Sites;

namespace AlarmHub.UnitTests.Api;

/// <summary>
/// Validation and invariants of the customer and site aggregates.
/// </summary>
public sealed class CustomerAndSiteTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    /// <summary>E-mail is normalised so lookups and uniqueness checks are predictable.</summary>
    [Fact]
    public void CreateCustomer_NormalisesEmail()
    {
        Customer customer = Customer.Create("Nordic Homes", "  OPS@Example.COM ", Now);

        Assert.Equal("ops@example.com", customer.Email);
        Assert.True(customer.IsActive);
    }

    /// <summary>A customer without a name is invalid.</summary>
    [Fact]
    public void CreateCustomer_WithoutName_Throws()
    {
        Assert.Throws<ArgumentException>(() => Customer.Create("  ", "ops@example.com", Now));
    }

    /// <summary>An address without an at sign is not an e-mail.</summary>
    [Fact]
    public void CreateCustomer_WithInvalidEmail_Throws()
    {
        Assert.Throws<ArgumentException>(() => Customer.Create("Nordic Homes", "not-an-email", Now));
    }

    /// <summary>Deactivation keeps the row for history.</summary>
    [Fact]
    public void Deactivate_KeepsCustomerButMarksInactive()
    {
        Customer customer = Customer.Create("Nordic Homes", "ops@example.com", Now);

        customer.Deactivate();

        Assert.False(customer.IsActive);
        Assert.NotEqual(Guid.Empty, customer.Id);
    }

    /// <summary>A site created with a known IANA zone is valid.</summary>
    [Fact]
    public void CreateSite_WithValidTimeZone_Succeeds()
    {
        Site site = Site.Create(
            Guid.NewGuid(),
            "Home - Fuengirola",
            new Address("Calle Marbella 12", "Fuengirola", "29640", "ES"),
            "Europe/Madrid",
            Now);

        Assert.Equal("Europe/Madrid", site.TimeZoneId);
        Assert.Equal("Calle Marbella 12, 29640 Fuengirola, ES", site.Address.ToString());
    }

    /// <summary>A typo in the time zone must fail at creation, not during scheduling later.</summary>
    [Fact]
    public void CreateSite_WithUnknownTimeZone_Throws()
    {
        Assert.Throws<ArgumentException>(() => Site.Create(
            Guid.NewGuid(),
            "Home",
            new Address("Street 1", "City", "00000", "ES"),
            "Mars/Olympus",
            Now));
    }

    /// <summary>A site cannot exist without an owner.</summary>
    [Fact]
    public void CreateSite_WithoutCustomer_Throws()
    {
        Assert.Throws<ArgumentException>(() => Site.Create(
            Guid.Empty,
            "Home",
            new Address("Street 1", "City", "00000", "ES"),
            "Europe/Madrid",
            Now));
    }
}
