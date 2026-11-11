namespace AlarmHub.Api.Domain.Sites;

/// <summary>
/// Postal address of a protected site.
/// </summary>
/// <remarks>
/// Modelled as an EF Core owned type: the address has no identity of its own and is always
/// loaded together with its site.
/// </remarks>
/// <param name="Street">Street and number.</param>
/// <param name="City">City or town.</param>
/// <param name="PostalCode">Postal code as used by the local postal service.</param>
/// <param name="CountryCode">Two-letter ISO country code (for example ES, NO, SE).</param>
public sealed record Address(string Street, string City, string PostalCode, string CountryCode)
{
    /// <summary>Single-line representation used in notifications.</summary>
    public override string ToString() => $"{Street}, {PostalCode} {City}, {CountryCode}";
}
