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

public class SystemSettingsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestJwtKey = "PleaseChangeThisSecretOrSetEnvVar";

    private readonly WebApplicationFactory<Program> _factory;

    public SystemSettingsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_RequiresAuthentication()
    {
        var factory = CreateFactory();
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/admin/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upsert_ForbiddenForNonAdminRoles()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager");

        var response = await manager.PutAsJsonAsync("/api/admin/settings/Booking.MaxAdvanceDays", new UpsertSystemSettingRequest { Value = "30" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Upsert_CreatesSetting_WhenItDoesNotExist_ForAdmin()
    {
        var factory = CreateFactory();
        var admin = CreateAuthenticatedClient(factory, "admin", 1);

        var response = await admin.PutAsJsonAsync(
            "/api/admin/settings/Booking.MaxAdvanceDays",
            new UpsertSystemSettingRequest { Value = "30", Description = "Maximum days a booking can be scheduled in advance." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<SystemSettingDto>>();
        Assert.NotNull(payload?.Data);
        Assert.Equal("Booking.MaxAdvanceDays", payload!.Data!.Key);
        Assert.Equal("30", payload.Data.Value);
        Assert.Null(payload.Data.UpdatedAt);
    }

    [Fact]
    public async Task Upsert_UpdatesExistingSetting_AndSetsUpdatedAt()
    {
        var factory = CreateFactory();
        var admin = CreateAuthenticatedClient(factory, "admin", 1);

        var create = await admin.PutAsJsonAsync(
            "/api/admin/settings/Booking.MaxAdvanceDays",
            new UpsertSystemSettingRequest { Value = "30" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var update = await admin.PutAsJsonAsync(
            "/api/admin/settings/Booking.MaxAdvanceDays",
            new UpsertSystemSettingRequest { Value = "45" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var payload = await update.Content.ReadFromJsonAsync<ApiResponse<SystemSettingDto>>();
        Assert.Equal("45", payload!.Data!.Value);
        Assert.NotNull(payload.Data.UpdatedAt);
    }

    [Fact]
    public async Task Upsert_RejectsMissingValue()
    {
        var factory = CreateFactory();
        var admin = CreateAuthenticatedClient(factory, "admin", 1);

        var response = await admin.PutAsJsonAsync(
            "/api/admin/settings/Booking.MaxAdvanceDays",
            new UpsertSystemSettingRequest { Value = string.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsSavedSettings_ForAdmin()
    {
        var factory = CreateFactory();
        var admin = CreateAuthenticatedClient(factory, "admin", 1);

        await admin.PutAsJsonAsync("/api/admin/settings/Feature.Notifications", new UpsertSystemSettingRequest { Value = "true" });

        var list = await admin.GetFromJsonAsync<ApiResponse<List<SystemSettingDto>>>("/api/admin/settings");
        Assert.NotNull(list);
        Assert.Contains(list!.Data!, setting => setting.Key == "Feature.Notifications" && setting.Value == "true");
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenSettingDoesNotExist()
    {
        var factory = CreateFactory();
        var admin = CreateAuthenticatedClient(factory, "admin", 1);

        var response = await admin.GetAsync("/api/admin/settings/DoesNotExist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = $"system_settings_integration_{Guid.NewGuid():N}";
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<HouseContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<HouseContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                });
            });
        });
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, string role, int? userId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(role, userId));
        return client;
    }

    private static string CreateToken(string role, int? userId = null)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Role, role) };
        if (userId.HasValue)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId.Value.ToString()));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        var keyBytes = Encoding.UTF8.GetBytes(TestJwtKey);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "HouseManagement",
            audience: "HouseManagement",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
