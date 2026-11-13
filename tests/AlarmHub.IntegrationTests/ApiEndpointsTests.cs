using System.Net;
using System.Net.Http.Json;
using AlarmHub.Api.Domain.Panels;

namespace AlarmHub.IntegrationTests;

/// <summary>
/// End-to-end HTTP tests for the API service against a real PostgreSQL container.
/// </summary>
public sealed class ApiEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>Customer -> site -> panel -> sensor can be created and read back.</summary>
    [Fact]
    public async Task RegisteringHardware_ThenReadingSite_ReturnsPanelAndSensor()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage customerResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Integration Homes",
            email = $"integration+{Guid.NewGuid():N}@alarmhub.local"
        });

        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);
        CustomerPayload customer = (await customerResponse.Content.ReadFromJsonAsync<CustomerPayload>())!;

        HttpResponseMessage siteResponse = await client.PostAsJsonAsync("/api/sites", new
        {
            customerId = customer.Id,
            name = "Home - Malaga",
            street = "Calle Larios 1",
            city = "Malaga",
            postalCode = "29005",
            countryCode = "ES",
            timeZoneId = "Europe/Madrid"
        });

        Assert.Equal(HttpStatusCode.Created, siteResponse.StatusCode);
        SitePayload site = (await siteResponse.Content.ReadFromJsonAsync<SitePayload>())!;

        HttpResponseMessage panelResponse = await client.PostAsJsonAsync("/api/panels", new
        {
            siteId = site.Id,
            serialNumber = $"IT-{Guid.NewGuid():N}",
            model = "SectorPro 4G"
        });

        Assert.Equal(HttpStatusCode.Created, panelResponse.StatusCode);
        PanelPayload panel = (await panelResponse.Content.ReadFromJsonAsync<PanelPayload>())!;
        Assert.False(string.IsNullOrWhiteSpace(panel.ApiKey));

        HttpResponseMessage sensorResponse = await client.PostAsJsonAsync($"/api/panels/{panel.Id}/sensors", new
        {
            zoneNumber = 1,
            type = "Motion",
            name = "Hallway"
        });

        Assert.Equal(HttpStatusCode.Created, sensorResponse.StatusCode);

        SiteDetailsPayload details = (await client.GetFromJsonAsync<SiteDetailsPayload>($"/api/sites/{site.Id}"))!;

        Assert.Single(details.Panels);
        Assert.Equal(panel.Id, details.Panels[0].Id);

        // The site endpoint returns a panel summary; the sensor list comes from the panel endpoint.
        PanelWithSensorsPayload panelDetails =
            (await client.GetFromJsonAsync<PanelWithSensorsPayload>($"/api/panels/{panel.Id}"))!;

        Assert.Single(panelDetails.Sensors);
        Assert.Equal("Motion", panelDetails.Sensors[0].Type);
    }

    /// <summary>A second sensor on the same zone is rejected instead of silently overwriting.</summary>
    [Fact]
    public async Task AddingDuplicateZone_ReturnsValidationProblem()
    {
        HttpClient client = factory.CreateClient();

        CustomerPayload customer = await CreateCustomerAsync(client);
        SitePayload site = await CreateSiteAsync(client, customer.Id);
        PanelPayload panel = await CreatePanelAsync(client, site.Id);

        await client.PostAsJsonAsync($"/api/panels/{panel.Id}/sensors", new { zoneNumber = 2, type = "Contact", name = "Door" });
        HttpResponseMessage duplicate = await client.PostAsJsonAsync($"/api/panels/{panel.Id}/sensors", new { zoneNumber = 2, type = "Contact", name = "Door again" });

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    /// <summary>The internal verification endpoint is what ingest uses to trust a panel.</summary>
    [Fact]
    public async Task InternalVerify_WithCorrectKey_ReturnsTrue()
    {
        HttpClient client = factory.CreateClient();

        CustomerPayload customer = await CreateCustomerAsync(client);
        SitePayload site = await CreateSiteAsync(client, customer.Id);
        PanelPayload panel = await CreatePanelAsync(client, site.Id);

        VerifyPayload? result = await client.PostAsJsonAsync("/internal/panels/verify", new { panelId = panel.Id, apiKey = panel.ApiKey })
            .ContinueWith(task => task.Result.Content.ReadFromJsonAsync<VerifyPayload>())
            .Unwrap();

        Assert.True(result!.IsValid);
    }

    private static async Task<CustomerPayload> CreateCustomerAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Integration Homes",
            email = $"integration+{Guid.NewGuid():N}@alarmhub.local"
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerPayload>())!;
    }

    private static async Task<SitePayload> CreateSiteAsync(HttpClient client, Guid customerId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/sites", new
        {
            customerId,
            name = "Home - Malaga",
            street = "Calle Larios 1",
            city = "Malaga",
            postalCode = "29005",
            countryCode = "ES",
            timeZoneId = "Europe/Madrid"
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SitePayload>())!;
    }

    private static async Task<PanelPayload> CreatePanelAsync(HttpClient client, Guid siteId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/panels", new
        {
            siteId,
            serialNumber = $"IT-{Guid.NewGuid():N}",
            model = "SectorPro 4G"
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PanelPayload>())!;
    }

    /// <summary>Shape of the customer response used by the tests.</summary>
    private sealed record CustomerPayload(Guid Id, string Name, string Email, bool IsActive);

    /// <summary>Shape of the site response used by the tests.</summary>
    private sealed record SitePayload(Guid Id, Guid CustomerId, string Name, string Address, string TimeZoneId);

    /// <summary>Shape of the panel response used by the tests.</summary>
    private sealed record PanelPayload(Guid Id, Guid SiteId, string SerialNumber, string Model, string ApiKey);

    /// <summary>Shape of the site details response used by the tests.</summary>
    private sealed record SiteDetailsPayload(Guid Id, Guid CustomerId, string Name, string Address, string TimeZoneId, IReadOnlyList<PanelDetails> Panels);

    /// <summary>Panel entry inside the site details response.</summary>
    private sealed record PanelDetails(Guid Id, string SerialNumber, string Model, string Status, DateTimeOffset? LastSeenAt);

    /// <summary>Panel endpoint response, including its sensors.</summary>
    private sealed record PanelWithSensorsPayload(Guid Id, Guid SiteId, string SerialNumber, string Model, string Status, DateTimeOffset? LastSeenAt, IReadOnlyList<SensorDetails> Sensors);

    /// <summary>Sensor entry inside the panel details.</summary>
    private sealed record SensorDetails(Guid Id, Guid PanelId, int ZoneNumber, string Type, string Name, bool IsActive);

    /// <summary>Shape of the internal verification response.</summary>
    private sealed record VerifyPayload(bool IsValid);
}

