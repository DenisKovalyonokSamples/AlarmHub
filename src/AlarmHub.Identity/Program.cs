using AlarmHub.Identity.Endpoints;
using AlarmHub.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// Token endpoint protection and future policy-based checks for the identity host itself.
builder.Services.AddAuthorization();

builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Identity"));
    options.UseOpenIddict();
});

// Local OIDC server. When Auth:Provider is "Auth0" the api services validate tokens against the
// tenant instead; this service then only serves local development and integration tests.
builder.Services.AddOpenIddict()
    .AddCore(options => options
        .UseEntityFrameworkCore()
        .UseDbContext<IdentityDbContext>())
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("/connect/token");

        // Machine-to-machine only for now; interactive flows arrive with the React console.
        options.AllowClientCredentialsFlow();

        options.RegisterScopes("alarms.read", "alarms.write", "devices.ingest");

        // Development certificates are fine for containers; production uses a Key Vault certificate.
        options.AddDevelopmentEncryptionCertificate();
        options.AddDevelopmentSigningCertificate();

        var aspNetCore = options.UseAspNetCore().EnableTokenEndpointPassthrough();

        // The local stack talks plain HTTP inside the docker network; production enforces HTTPS.
        if (builder.Environment.IsDevelopment())
        {
            aspNetCore.DisableTransportSecurityRequirement();
        }
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("alarmhub-identity"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter(options =>
        {
            string? endpoint = builder.Configuration["OpenTelemetry:Endpoint"];

            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                options.Endpoint = new Uri(endpoint);
            }
        }));

WebApplication app = builder.Build();

// Development convenience: schema plus a demo client, so `curl` can obtain a token immediately.
if (app.Environment.IsDevelopment())
{
    using IServiceScope scope = app.Services.CreateScope();
    IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapTokenEndpoint();

app.Run();

/// <summary>
/// Marker type used by integration tests to boot the identity service.
/// </summary>
public partial class Program
{
}

