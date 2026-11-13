using System.Security.Claims;
using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AlarmHub.Identity.Endpoints;

/// <summary>
/// Token endpoint for the client-credentials flow.
/// </summary>
/// <remarks>
/// OpenIddict validates the incoming request (client id, scopes, signature) and this endpoint only
/// authenticates the client and issues the principal. Keeping it explicit makes the flow readable
/// and easy to extend later with authorization-code and refresh tokens for the React console.
/// </remarks>
internal static class TokenEndpoint
{
    /// <summary>Authentication type used for the issued principal.</summary>
    private const string AuthenticationType = "OpenIddict";

    /// <summary>Maps <c>POST /connect/token</c>.</summary>
    /// <param name="app">Application builder.</param>
    public static IEndpointRouteBuilder MapTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/connect/token", HandleAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    /// <summary>Authenticates the client and signs the access token.</summary>
    /// <param name="context">Current HTTP context.</param>
    /// <param name="applicationManager">OpenIddict application store.</param>
    private static async Task<IResult> HandleAsync(HttpContext context, IOpenIddictApplicationManager applicationManager)
    {
        OpenIddictRequest? request = context.GetOpenIddictServerRequest();

        if (request?.ClientId is null || string.IsNullOrEmpty(request.ClientSecret))
        {
            return Results.Problem("Malformed token request.", statusCode: StatusCodes.Status400BadRequest);
        }

        object? application = await applicationManager.FindByClientIdAsync(request.ClientId);

        if (application is null || !await applicationManager.ValidateClientSecretAsync(application, request.ClientSecret))
        {
            return Results.Problem("Invalid client credentials.", statusCode: StatusCodes.Status401Unauthorized);
        }

        ClaimsIdentity identity = new(AuthenticationType, Claims.Name, Claims.Role);

        identity.SetClaim(Claims.Subject, request.ClientId)
            .SetScopes(request.GetScopes());

        // Machine-to-machine: every claim belongs in the access token, none in an id token.
        identity.SetDestinations(static _ => [Destinations.AccessToken]);

        return Results.SignIn(
            new ClaimsPrincipal(identity),
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
