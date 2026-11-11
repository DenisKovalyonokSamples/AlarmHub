using AlarmHub.Api.Endpoints;
using AlarmHub.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Problem details give every error a stable RFC 9457 shape, which the future React console relies on.
builder.Services.AddProblemDetails();

// Enums travel as strings: a request with "Motion" is self-describing and does not break when
// a new enum member changes the numeric values.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Built-in OpenAPI document is exposed in development at /openapi/v1.json.
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks();

// One database per service: the API owns customers, sites, panels and sensors and nothing else.
builder.Services.AddDbContext<AlarmHubDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AlarmHub")));

// TimeProvider keeps domain code testable: tests inject a fake clock instead of sleeping.
builder.Services.AddSingleton(TimeProvider.System);

// OpenTelemetry is wired once per service; the collector endpoint comes from configuration
// so the same image runs locally (compose) and in Azure (Honeycomb/App Insights exporter).
string? otlpEndpoint = builder.Configuration["OpenTelemetry:Endpoint"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("alarmhub-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                options.Endpoint = new Uri(otlpEndpoint);
            }
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter(options =>
        {
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                options.Endpoint = new Uri(otlpEndpoint);
            }
        }));

WebApplication app = builder.Build();

// Development convenience: create the schema on startup. Production uses versioned EF migrations
// applied by the deployment pipeline, never EnsureCreated.
if (app.Environment.IsDevelopment())
{
    using IServiceScope scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AlarmHubDbContext>().Database.EnsureCreated();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness answers "is the process alive"; readiness is the endpoint a load balancer should use.
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapCustomersEndpoints();
app.MapSitesEndpoints();
app.MapPanelsEndpoints();

// Service-to-service routes: reachable only inside the platform network, never through the gateway.
app.MapInternalPanelsEndpoints();

app.Run();

/// <summary>
/// Marker type used by integration tests to boot the API through <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program
{
}
