using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlarmHub.IntegrationTests;

/// <summary>
/// Authentication handler used by integration tests.
/// </summary>
/// <remarks>
/// The tests exercise business behaviour, not the token issuer, so every request is authenticated
/// with the scopes the endpoints require. The real JWT pipeline is covered by the identity smoke test.
/// </remarks>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Scheme name registered by the test factories.</summary>
    public const string SchemeName = "Test";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim[] claims =
        [
            new(ClaimTypes.Name, "integration-test"),
            new("scope", "alarms.read"),
            new("scope", "alarms.write"),
            new("scope", "devices.ingest")
        ];

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
