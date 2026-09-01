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

public class AdministrationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestJwtKey = "PleaseChangeThisSecretOrSetEnvVar";

    private readonly WebApplicationFactory<Program> _factory;

    public AdministrationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetUsers_RequiresAuthentication()
    {
        var factory = CreateFactory();
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_ForbiddenForNonAdminRoles()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager");

        var response = await manager.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_AllowsAdmin_AndReturnsUserList()
    {
        var factory = CreateFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            db.Users.Add(new User
            {
                UserName = "listeduser",
                Email = "listeduser@example.com",
                PasswordHash = "hash",
                Role = "manager",
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var admin = CreateAuthenticatedClient(factory, "admin");
        var response = await admin.GetAsync("/api/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserDto>>>();
        Assert.NotNull(payload);
        Assert.Contains(payload!.Data!, user => user.UserName == "listeduser");
    }

    [Fact]
    public async Task GetUsers_FiltersByRoleAndActiveState()
    {
        var factory = CreateFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            db.Users.AddRange(
                new User
                {
                    UserName = "active-manager",
                    Email = "active-manager@example.com",
                    PasswordHash = "hash",
                    Role = "manager",
                    IsActive = true
                },
                new User
                {
                    UserName = "inactive-manager",
                    Email = "inactive-manager@example.com",
                    PasswordHash = "hash",
                    Role = "manager",
                    IsActive = false
                },
                new User
                {
                    UserName = "active-househelp",
                    Email = "active-househelp@example.com",
                    PasswordHash = "hash",
                    Role = "househelp",
                    IsActive = true
                });
            await db.SaveChangesAsync();
        }

        var admin = CreateAuthenticatedClient(factory, "admin");
        var response = await admin.GetAsync("/api/admin/users?role=manager&isActive=true");
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserDto>>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(payload!.Data!);
        Assert.Equal("active-manager", payload.Data![0].UserName);
    }

    [Fact]
    public async Task GetUser_AllowsAdmin_AndReturnsUserDetails()
    {
        var factory = CreateFactory();
        int userId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var user = new User
            {
                UserName = "detaileduser",
                Email = "detaileduser@example.com",
                PasswordHash = "hash",
                Role = "househelp",
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var admin = CreateAuthenticatedClient(factory, "admin");
        var response = await admin.GetAsync($"/api/admin/users/{userId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        Assert.NotNull(payload);
        Assert.Equal("detaileduser", payload!.Data!.UserName);
    }

    [Fact]
    public async Task GetUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var factory = CreateFactory();
        var admin = CreateAuthenticatedClient(factory, "admin");

        var response = await admin.GetAsync("/api/admin/users/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetUser_ForbiddenForNonAdminRoles()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager");

        var response = await manager.GetAsync("/api/admin/users/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_AllowsAdmin_ToChangeAnotherUsersRole()
    {
        var factory = CreateFactory();
        int userId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var user = new User
            {
                UserName = "roleuser",
                Email = "roleuser@example.com",
                PasswordHash = "hash",
                Role = "househelp",
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var admin = CreateAuthenticatedClient(factory, "admin", 999);
        var response = await admin.PutAsJsonAsync($"/api/admin/users/{userId}/role", new UpdateUserRoleRequest { Role = "manager" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        Assert.NotNull(payload);
        Assert.Equal("manager", payload!.Data!.Role);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var updated = await db.Users.SingleAsync(item => item.Id == userId);
            Assert.Equal("manager", updated.Role);
        }
    }

    [Fact]
    public async Task UpdateRole_RejectsInvalidRoleValue()
    {
        var factory = CreateFactory();
        int userId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var user = new User
            {
                UserName = "badroleuser",
                Email = "badroleuser@example.com",
                PasswordHash = "hash",
                Role = "househelp",
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var admin = CreateAuthenticatedClient(factory, "admin", 999);
        var response = await admin.PutAsJsonAsync($"/api/admin/users/{userId}/role", new UpdateUserRoleRequest { Role = "superadmin" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_PreventsAdminFromDemotingOwnAccount()
    {
        var factory = CreateFactory();
        int adminId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var admin = new User
            {
                UserName = "selfadmin",
                Email = "selfadmin@example.com",
                PasswordHash = "hash",
                Role = "admin",
                IsActive = true
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            adminId = admin.Id;
        }

        var adminClient = CreateAuthenticatedClient(factory, "admin", adminId);
        var response = await adminClient.PutAsJsonAsync($"/api/admin/users/{adminId}/role", new UpdateUserRoleRequest { Role = "manager" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_ForbiddenForNonAdminRoles()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager");

        var response = await manager.PutAsJsonAsync("/api/admin/users/1/role", new UpdateUserRoleRequest { Role = "manager" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetActive_AllowsAdmin_ToDeactivateAnotherUser()
    {
        var factory = CreateFactory();
        int userId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var user = new User
            {
                UserName = "deactivateme",
                Email = "deactivateme@example.com",
                PasswordHash = "hash",
                Role = "manager",
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var admin = CreateAuthenticatedClient(factory, "admin", 999);
        var response = await admin.PutAsync($"/api/admin/users/{userId}/activate?active=false", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        Assert.NotNull(payload);
        Assert.False(payload!.Data!.IsActive);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var updated = await db.Users.SingleAsync(item => item.Id == userId);
            Assert.False(updated.IsActive);
        }
    }

    [Fact]
    public async Task SetActive_PreventsAdminFromDeactivatingOwnAccount()
    {
        var factory = CreateFactory();
        int adminId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var admin = new User
            {
                UserName = "selfdeactivate",
                Email = "selfdeactivate@example.com",
                PasswordHash = "hash",
                Role = "admin",
                IsActive = true
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            adminId = admin.Id;
        }

        var adminClient = CreateAuthenticatedClient(factory, "admin", adminId);
        var response = await adminClient.PutAsync($"/api/admin/users/{adminId}/activate?active=false", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetActive_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var factory = CreateFactory();
        var admin = CreateAuthenticatedClient(factory, "admin", 999);

        var response = await admin.PutAsync("/api/admin/users/999999/activate?active=false", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetActive_ForbiddenForNonAdminRoles()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager");

        var response = await manager.PutAsync("/api/admin/users/1/activate?active=false", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = $"administration_integration_{Guid.NewGuid():N}";
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
