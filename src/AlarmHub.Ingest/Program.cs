using AlarmHub.Ingest.Endpoints;
using AlarmHub.Ingest.Infrastructure;
using AlarmHub.Ingest.Messaging;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// Panels send event types as readable strings ("AlarmTriggered"), not as numbers.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddDbContext<IngestDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Ingest")));

builder.Services.AddSingleton(TimeProvider.System);

// Panel credentials are validated by the API service, the single owner of the panel registry.
builder.Services.AddHttpClient<IPanelDirectory, HttpPanelDirectory>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:ApiBaseAddress"] ?? "http://localhost:5080");
});

// Service Bus is optional locally: without a connection string the log publisher is used and the
// pipeline stays fully demonstrable. In Azure the same code path talks to a real topic.
string? serviceBusConnection = builder.Configuration.GetConnectionString("ServiceBus");
string topicName = builder.Configuration["ServiceBus:Topic"] ?? "device-events";

if (string.IsNullOrWhiteSpace(serviceBusConnection))
{
    builder.Services.AddSingleton<IIntegrationEventPublisher, LoggingIntegrationEventPublisher>();
}
else
{
    builder.Services.AddSingleton(_ => new ServiceBusClient(serviceBusConnection));
    builder.Services.AddSingleton(provider => provider
        .GetRequiredService<ServiceBusClient>()
        .CreateSender(topicName));
    builder.Services.AddSingleton<IIntegrationEventPublisher, ServiceBusIntegrationEventPublisher>();
}

builder.Services.AddHostedService<OutboxDispatcher>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("alarmhub-ingest"))
    .WithTracing(tracing => tracing
        .AddSource("Azure.Messaging.ServiceBus")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            string? endpoint = builder.Configuration["OpenTelemetry:Endpoint"];

            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                options.Endpoint = new Uri(endpoint);
            }
        }));

WebApplication app = builder.Build();

// Development convenience: create the schema on startup (production uses EF migrations).
if (app.Environment.IsDevelopment())
{
    using IServiceScope scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<IngestDbContext>().Database.EnsureCreated();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapDeviceEventsEndpoints();

app.Run();

/// <summary>
/// Marker type used by integration tests to boot the service through <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program
{
}
