using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HouseManagement.Api.Tests;

public class ClientPreferencesIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestJwtKey = "PleaseChangeThisSecretOrSetEnvVar";

    private readonly WebApplicationFactory<Program> _factory;

    public ClientPreferencesIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PreferredHouseHelps_RequireAuthentication()
    {
        var factory = CreateFactory();

        var response = await factory.CreateClient().GetAsync("/api/clients/me/preferred-househelps");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ClientCanAddListAndRemoveOnlyOwnPreferredHouseHelp()
    {
        var factory = CreateFactory();
        await SeedAsync(factory);

        var firstClient = CreateAuthenticatedClient(factory, 10);
        var addResponse = await firstClient.PostAsync("/api/clients/me/preferred-househelps/1", null);
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        var duplicateResponse = await firstClient.PostAsync("/api/clients/me/preferred-househelps/1", null);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);

        var firstList = await firstClient.GetFromJsonAsync<ApiResponse<List<PreferredHouseHelpDto>>>(
            "/api/clients/me/preferred-househelps");
        var preferred = Assert.IsType<List<PreferredHouseHelpDto>>(firstList!.Data);
        Assert.Single(preferred);
        Assert.Equal(1, preferred[0].Id);
        Assert.Equal("Active", preferred[0].FirstName);

        var secondClient = CreateAuthenticatedClient(factory, 11);
        var secondList = await secondClient.GetFromJsonAsync<ApiResponse<List<PreferredHouseHelpDto>>>(
            "/api/clients/me/preferred-househelps");
        Assert.Empty(secondList!.Data!);

        var removeAsOtherClient = await secondClient.DeleteAsync("/api/clients/me/preferred-househelps/1");
        Assert.Equal(HttpStatusCode.NotFound, removeAsOtherClient.StatusCode);

        var removeResponse = await firstClient.DeleteAsync("/api/clients/me/preferred-househelps/1");
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);

        var afterRemoval = await firstClient.GetFromJsonAsync<ApiResponse<List<PreferredHouseHelpDto>>>(
            "/api/clients/me/preferred-househelps");
        Assert.Empty(afterRemoval!.Data!);
    }

    [Fact]
    public async Task ClientCannotPreferInactiveHouseHelp()
    {
        var factory = CreateFactory();
        await SeedAsync(factory);

        var client = CreateAuthenticatedClient(factory, 10);
        var response = await client.PostAsync("/api/clients/me/preferred-househelps/2", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = $"client_preferences_integration_{Guid.NewGuid():N}";
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<HouseContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<HouseContext>(options => options.UseInMemoryDatabase(dbName));
            });
        });
    }

    private static async Task SeedAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        db.Clients.AddRange(
            new Client { Id = 10, UserId = 10, Name = "First Client", Phone = "+254700000010" },
            new Client { Id = 11, UserId = 11, Name = "Second Client", Phone = "+254700000011" });
        db.HouseHelps.AddRange(
            new HouseHelp
            {
                Id = 1,
                FirstName = "Active",
                LastName = "Helper",
                Phone = "+254700000001",
                City = "Nairobi",
                IsActive = true
            },
            new HouseHelp
            {
                Id = 2,
                FirstName = "Inactive",
                LastName = "Helper",
                Phone = "+254700000002",
                City = "Nairobi",
                IsActive = false
            });
        await db.SaveChangesAsync();
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, int userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId));
        return client;
    }

    private static string CreateToken(int userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "manager"),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "HouseManagement",
            audience: "HouseManagement",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
