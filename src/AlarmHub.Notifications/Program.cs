using AlarmHub.Notifications.Infrastructure;
using AlarmHub.Notifications.Messaging;
using AlarmHub.Notifications.Notifications;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<NotificationsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Notifications")));

builder.Services.AddSingleton(TimeProvider.System);

// The provider is swappable: the log sender is used until a real e-mail provider is configured.
builder.Services.AddSingleton<INotificationSender, LogNotificationSender>();

string? serviceBusConnection = builder.Configuration.GetConnectionString("ServiceBus");
string topicName = builder.Configuration["ServiceBus:Topic"] ?? "incidents";
string subscriptionName = builder.Configuration["ServiceBus:Subscription"] ?? "notifications";

if (!string.IsNullOrWhiteSpace(serviceBusConnection))
{
    builder.Services.AddSingleton(_ => new ServiceBusClient(serviceBusConnection));

    builder.Services.AddSingleton(provider => provider
        .GetRequiredService<ServiceBusClient>()
        .CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions
        {
            PrefetchCount = 5,
            MaxConcurrentCalls = 2,
            AutoCompleteMessages = false
        }));

    builder.Services.AddHostedService<IncidentOpenedConsumer>();
}

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("alarmhub-notifications"))
    .WithTracing(tracing => tracing
        .AddOtlpExporter(options =>
        {
            string? endpoint = builder.Configuration["OpenTelemetry:Endpoint"];

            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                options.Endpoint = new Uri(endpoint);
            }
        }));

IHost host = builder.Build();

// Development convenience: create the schema on startup (production applies EF migrations).
if (builder.Environment.IsDevelopment())
{
    using IServiceScope scope = host.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.EnsureCreated();
}

await host.RunAsync();
