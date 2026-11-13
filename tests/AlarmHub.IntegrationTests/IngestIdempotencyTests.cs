using System.Net;
using System.Net.Http.Json;
using AlarmHub.Contracts.DeviceEvents;
using Microsoft.EntityFrameworkCore;

namespace AlarmHub.IntegrationTests;

/// <summary>
/// Ingest behaviour that protects the platform from panel retries and lost events.
/// </summary>
public sealed class IngestIdempotencyTests(IngestFactory factory) : IClassFixture<IngestFactory>
{
    /// <summary>The same event delivered twice is stored once and answered with "duplicate".</summary>
    [Fact]
    public async Task PostingSameEventTwice_StoresOneRowAndOneOutboxMessage()
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Panel-Key", "test-panel-key");

        object payload = new
        {
            eventId = Guid.NewGuid(),
            panelId = Guid.NewGuid(),
            occurredAt = DateTimeOffset.UtcNow,
            type = "AlarmTriggered",
            sequenceNumber = 1,
            payload = new Dictionary<string, string> { ["zone"] = "1" }
        };

        HttpResponseMessage first = await client.PostAsJsonAsync("/device-events", payload);
        HttpResponseMessage second = await client.PostAsJsonAsync("/device-events", payload);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        ResultPayload firstResult = (await first.Content.ReadFromJsonAsync<ResultPayload>())!;
        ResultPayload secondResult = (await second.Content.ReadFromJsonAsync<ResultPayload>())!;

        Assert.Equal("accepted", firstResult.Status);
        Assert.Equal("duplicate", secondResult.Status);

        await factory.WithDbContextAsync(async dbContext =>
        {
            Assert.Equal(1, await dbContext.DeviceEvents.CountAsync());
            Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
        });
    }

    /// <summary>A request without the panel header is rejected before touching the database.</summary>
    [Fact]
    public async Task PostingWithoutPanelKey_ReturnsUnauthorized()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/device-events", new
        {
            eventId = Guid.NewGuid(),
            panelId = Guid.NewGuid(),
            occurredAt = DateTimeOffset.UtcNow,
            type = DeviceEventType.AlarmTriggered,
            sequenceNumber = 2,
            payload = new Dictionary<string, string>()
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>An unknown event type is rejected with a validation problem.</summary>
    [Fact]
    public async Task PostingUnknownType_ReturnsValidationProblem()
    {
        HttpClient client = factory.CreateClient();

        HttpRequestMessage request = new(HttpMethod.Post, "/device-events")
        {
            Content = JsonContent.Create(new
            {
                eventId = Guid.NewGuid(),
                panelId = Guid.NewGuid(),
                occurredAt = DateTimeOffset.UtcNow,
                type = 999,
                sequenceNumber = 3,
                payload = new Dictionary<string, string>()
            })
        };

        request.Headers.Add("X-Panel-Key", "any-key");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Shape of the ingest response.</summary>
    private sealed record ResultPayload(Guid EventId, string Status);
}

