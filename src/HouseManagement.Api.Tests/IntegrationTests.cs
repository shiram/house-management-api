using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using HouseManagement.Api.Data;

namespace HouseManagement.Api.Tests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace HouseContext with InMemory for tests
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<HouseContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<HouseContext>(options =>
                {
                    options.UseInMemoryDatabase("integration_tests_db");
                });
            });
        });
    }

    [Fact]
    public async Task HealthEndpoints_ReturnAliveAndReady()
    {
        var client = _factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        live.EnsureSuccessStatusCode();
        var liveJson = JsonDocument.Parse(await live.Content.ReadAsStringAsync());
        Assert.Equal("Alive", liveJson.RootElement.GetProperty("status").GetString());

        var ready = await client.GetAsync("/health/ready");
        ready.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Swagger_IncludesBearerSecurity()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/swagger/v1/swagger.json");
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.True(root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }
}
