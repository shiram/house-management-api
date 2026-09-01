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

public class RatingsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestJwtKey = "PleaseChangeThisSecretOrSetEnvVar";

    private readonly WebApplicationFactory<Program> _factory;

    public RatingsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RateBooking_AllowsClientToRateOwnCompletedBooking_AndRejectsDuplicate()
    {
        var factory = CreateFactory();
        await SeedCompletedBookingAsync(factory, bookingId: 1, houseHelpId: 1, clientUserId: 30);

        var clientUser = CreateAuthenticatedClient(factory, "manager", 30);
        var response = await clientUser.PostAsJsonAsync("/api/bookings/1/rating", new CreateHouseHelpRatingRequest
        {
            Score = 5,
            Comment = "Excellent service"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ApiResponse<HouseHelpRatingDto>>();
        Assert.NotNull(created);
        Assert.Equal(5, created!.Data!.Score);
        Assert.Equal("Excellent service", created.Data.Comment);

        var duplicate = await clientUser.PostAsJsonAsync("/api/bookings/1/rating", new CreateHouseHelpRatingRequest
        {
            Score = 2
        });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task RateBooking_RejectsWhenCallerDoesNotOwnBooking()
    {
        var factory = CreateFactory();
        await SeedCompletedBookingAsync(factory, bookingId: 2, houseHelpId: 2, clientUserId: 31);

        var otherUser = CreateAuthenticatedClient(factory, "manager", 999);
        var response = await otherUser.PostAsJsonAsync("/api/bookings/2/rating", new CreateHouseHelpRatingRequest
        {
            Score = 4
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetForHouseHelp_AndSummary_AreReadableAnonymously()
    {
        var factory = CreateFactory();
        await SeedCompletedBookingAsync(factory, bookingId: 3, houseHelpId: 3, clientUserId: 32);

        var clientUser = CreateAuthenticatedClient(factory, "manager", 32);
        var rateResponse = await clientUser.PostAsJsonAsync("/api/bookings/3/rating", new CreateHouseHelpRatingRequest
        {
            Score = 4,
            Comment = "Very good"
        });
        Assert.Equal(HttpStatusCode.Created, rateResponse.StatusCode);

        var anonymous = factory.CreateClient();
        var listResponse = await anonymous.GetAsync("/api/househelps/3/ratings");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<HouseHelpRatingDto>>>();
        Assert.Single(list!.Data!);
        Assert.Equal(4, list.Data![0].Score);

        var summaryResponse = await anonymous.GetAsync("/api/househelps/3/ratings/summary");
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<ApiResponse<HouseHelpRatingSummaryDto>>();
        Assert.Equal(1, summary!.Data!.RatingCount);
        Assert.Equal(4.0, summary.Data.AverageScore);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = $"rating_integration_{Guid.NewGuid():N}";
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

    private static async Task SeedCompletedBookingAsync(WebApplicationFactory<Program> factory, int bookingId, int houseHelpId, int clientUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();

        if (!await db.Services.AnyAsync(service => service.Id == 1))
        {
            db.Services.Add(new Service { Id = 1, Code = "RATING_TEST", Name = "Rating Test Service", BasePrice = 20m, IsActive = true, CreatedAt = DateTimeOffset.UtcNow });
        }

        db.HouseHelps.Add(new HouseHelp
        {
            Id = houseHelpId,
            FirstName = "Helper",
            LastName = houseHelpId.ToString(),
            Phone = "+25470000" + houseHelpId,
            City = "Nairobi",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var client = new Client
        {
            Id = clientUserId,
            UserId = clientUserId,
            Name = "Client " + clientUserId,
            Phone = "+25471000" + clientUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Clients.Add(client);

        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            Reference = "BK-RATE-" + bookingId,
            ServiceId = 1,
            ClientId = client.Id,
            AssignedHouseHelpId = houseHelpId,
            ServiceAddress = new ServiceAddress { Line1 = "1 Rating Road", City = "Nairobi", Country = "Kenya" },
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(-2),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(-2).AddHours(2),
            Status = BookingStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-3)
        });

        await db.SaveChangesAsync();
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
