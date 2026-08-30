using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HouseManagement.Api.Tests;

public class AuditLogsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestJwtKey = "PleaseChangeThisSecretOrSetEnvVar";

    private readonly WebApplicationFactory<Program> _factory;

    public AuditLogsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetList_RequiresAuthentication()
    {
        var factory = CreateFactory();
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetList_ForbiddenForNonAdminRoles()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager");

        var response = await manager.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetList_ReturnsLoggedEntries_ForAdmin_AndSupportsFilters()
    {
        var factory = CreateFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var auditLogs = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            await auditLogs.LogAsync("user.role_changed", "User", entityId: 1, userId: 10, details: "manager -> admin");
            await auditLogs.LogAsync("booking.assigned", "Booking", entityId: 2, userId: 20);
        }

        var admin = CreateAuthenticatedClient(factory, "admin");

        var all = await admin.GetFromJsonAsync<ApiResponse<List<AuditLogDto>>>("/api/admin/audit-logs");
        Assert.NotNull(all);
        Assert.Equal(2, all!.Data!.Count);

        var filtered = await admin.GetFromJsonAsync<ApiResponse<List<AuditLogDto>>>("/api/admin/audit-logs?entityType=Booking");
        Assert.NotNull(filtered);
        Assert.Single(filtered!.Data!);
        Assert.Equal("booking.assigned", filtered.Data![0].Action);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = $"audit_logs_integration_{Guid.NewGuid():N}";
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
