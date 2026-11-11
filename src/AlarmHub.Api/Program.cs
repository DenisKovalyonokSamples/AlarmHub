using AlarmHub.Api.Endpoints;
using AlarmHub.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Problem details give every error a stable RFC 9457 shape, which the future React console relies on.
builder.Services.AddProblemDetails();

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

app.Run();

/// <summary>
/// Marker type used by integration tests to boot the API through <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program
{
}
