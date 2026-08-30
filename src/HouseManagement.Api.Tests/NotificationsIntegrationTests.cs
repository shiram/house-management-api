using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using HouseManagement.Api.Common;
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

public class NotificationsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestJwtKey = "PleaseChangeThisSecretOrSetEnvVar";

    private readonly WebApplicationFactory<Program> _factory;

    public NotificationsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMine_RequiresAuthentication()
    {
        var factory = CreateFactory();
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/api/notifications/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyOwnNotifications()
    {
        var factory = CreateFactory();

        int notificationId;
        using (var scope = factory.Services.CreateScope())
        {
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var created = await notifications.CreateAsync(50, NotificationTypes.BookingAssigned, "Assigned", "You were assigned to BK-1", "Booking", 1);
            notificationId = created.Id;
            await notifications.CreateAsync(999, NotificationTypes.BookingCreated, "Someone else's", "Not yours");
        }

        var houseHelp = CreateAuthenticatedClient(factory, "househelp", 50);
        var list = await houseHelp.GetFromJsonAsync<ApiResponse<List<NotificationDto>>>("/api/notifications/me");

        Assert.NotNull(list);
        Assert.Single(list!.Data!);
        Assert.Equal("Assigned", list.Data![0].Title);

        var detail = await houseHelp.GetFromJsonAsync<ApiResponse<NotificationDto>>($"/api/notifications/me/{notificationId}");
        Assert.Equal(notificationId, detail!.Data!.Id);
    }

    [Fact]
    public async Task GetMineById_ReturnsNotFound_WhenNotificationBelongsToAnotherUser()
    {
        var factory = CreateFactory();

        int notificationId;
        using (var scope = factory.Services.CreateScope())
        {
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var created = await notifications.CreateAsync(999, NotificationTypes.BookingCreated, "Not yours", "Message");
            notificationId = created.Id;
        }

        var client = CreateAuthenticatedClient(factory, "manager", 1);
        var response = await client.GetAsync($"/api/notifications/me/{notificationId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_SupportsUnreadOnlyFilter()
    {
        var factory = CreateFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();

            var read = await notifications.CreateAsync(70, NotificationTypes.BookingConfirmed, "Read", "Message");
            await notifications.CreateAsync(70, NotificationTypes.BookingStatusChanged, "Unread", "Message");

            var entity = await db.Notifications.SingleAsync(n => n.Id == read.Id);
            entity.IsRead = true;
            entity.ReadAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(factory, "manager", 70);
        var unread = await client.GetFromJsonAsync<ApiResponse<List<NotificationDto>>>("/api/notifications/me?unreadOnly=true");

        Assert.NotNull(unread);
        Assert.Single(unread!.Data!);
        Assert.Equal("Unread", unread.Data![0].Title);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = $"notifications_integration_{Guid.NewGuid():N}";
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
