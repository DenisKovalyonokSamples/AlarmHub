using AlarmHub.Processing.Infrastructure;
using AlarmHub.Processing.Messaging;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<ProcessingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Processing")));

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHttpClient<IPanelDirectory, HttpPanelDirectory>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:ApiBaseAddress"] ?? "http://localhost:5080");
});

string? serviceBusConnection = builder.Configuration.GetConnectionString("ServiceBus");
string topicName = builder.Configuration["ServiceBus:Topic"] ?? "device-events";
string subscriptionName = builder.Configuration["ServiceBus:Subscription"] ?? "processing";

if (string.IsNullOrWhiteSpace(serviceBusConnection))
{
    // Without a broker the service still starts, so the host is testable; no messages will arrive.
    builder.Services.AddSingleton<IIntegrationEventPublisher, LoggingIntegrationEventPublisher>();
}
else
{
    builder.Services.AddSingleton(_ => new ServiceBusClient(serviceBusConnection));

    builder.Services.AddSingleton(provider => provider
        .GetRequiredService<ServiceBusClient>()
        .CreateSender(topicName));

    builder.Services.AddSingleton(provider => provider
        .GetRequiredService<ServiceBusClient>()
        .CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions
        {
            // One prefetch keeps throughput predictable without holding a large buffer of messages.
            PrefetchCount = 10,
            MaxConcurrentCalls = 4,
            AutoCompleteMessages = false
        }));

    builder.Services.AddSingleton<IIntegrationEventPublisher, ServiceBusIntegrationEventPublisher>();
    builder.Services.AddHostedService<DeviceEventConsumer>();
}

builder.Services.AddHostedService<OutboxDispatcher>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("alarmhub-processing"))
    .WithTracing(tracing => tracing
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            string? endpoint = builder.Configuration["OpenTelemetry:Endpoint"];

            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                options.Endpoint = new Uri(endpoint);
            }
        }));

IHost host = builder.Build();
await host.RunAsync();
