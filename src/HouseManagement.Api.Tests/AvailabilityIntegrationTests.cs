using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HouseManagement.Api.Tests;

public class AvailabilityIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AvailabilityIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(item => item.ServiceType == typeof(DbContextOptions<HouseContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<HouseContext>(options =>
                    options.UseInMemoryDatabase("availability_integration_db"));
            });
        });
    }

    [Fact]
    public async Task ManagerCanUpdateAvailability_AndAnonymousClientCanReadIt()
    {
        var admin = CreateAuthenticatedClient("admin");
        var houseHelp = await CreateHouseHelpAsync(admin);
        var manager = CreateAuthenticatedClient("manager");

        var update = await manager.PutAsJsonAsync(
            $"/api/househelps/{houseHelp.Id}/availability",
            new UpdateAvailabilityRequest
            {
                WeeklySlots = new[]
                {
                    new AvailabilitySlotRequest
                    {
                        DayOfWeek = DayOfWeek.Monday,
                        StartTime = new TimeOnly(8),
                        EndTime = new TimeOnly(12)
                    }
                }
            });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var read = await _factory.CreateClient().GetFromJsonAsync<ApiResponse<AvailabilityDto>>(
            $"/api/availability?houseHelpId={houseHelp.Id}");
        Assert.Single(read!.Data!.WeeklySlots);
        Assert.Equal(DayOfWeek.Monday, read.Data.WeeklySlots.Single().DayOfWeek);
    }

    [Fact]
    public async Task AvailabilityUpdate_RejectsOverlap_AndRequiresAuthorization()
    {
        var admin = CreateAuthenticatedClient("admin");
        var houseHelp = await CreateHouseHelpAsync(admin);
        var payload = new UpdateAvailabilityRequest
        {
            WeeklySlots = new[]
            {
                new AvailabilitySlotRequest { DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(8), EndTime = new TimeOnly(12) },
                new AvailabilitySlotRequest { DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeOnly(11), EndTime = new TimeOnly(14) }
            }
        };

        var anonymous = _factory.CreateClient();
        var unauthorized = await anonymous.PutAsJsonAsync($"/api/househelps/{houseHelp.Id}/availability", payload);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var manager = CreateAuthenticatedClient("manager");
        var invalid = await manager.PutAsJsonAsync($"/api/househelps/{houseHelp.Id}/availability", payload);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private async Task<HouseHelpDto> CreateHouseHelpAsync(HttpClient client)
    {
        var request = new CreateHouseHelpRequest
        {
            FirstName = "Integration",
            LastName = Guid.NewGuid().ToString("N")[..8],
            Phone = $"+2547{Random.Shared.Next(10000000, 99999999)}",
            City = "Nairobi"
        };
        var response = await client.PostAsJsonAsync("/api/househelps", request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();
        return envelope!.Data!;
    }

    private HttpClient CreateAuthenticatedClient(string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(role));
        return client;
    }

    private static string CreateToken(string role)
    {
        var keyBytes = Encoding.UTF8.GetBytes("PleaseChangeThisSecretOrSetEnvVar");
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "HouseManagement",
            audience: "HouseManagement",
            claims: new[] { new Claim(ClaimTypes.Role, role) },
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
