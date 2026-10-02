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

// Integration coverage for T390: once a booking is accepted, its price snapshot (price lines,
// subtotal, tax, and total) must stay exactly as calculated at booking time even if a manager
// later changes the underlying pricing rule, rate, fee, or surcharge. Only NEW quotes/bookings
// should reflect the updated pricing.
public class BookingPriceSnapshotImmutabilityIntegrationTests
{
    private const string TestJwtKey = "PleaseChangeThisSecretOrSetEnvVar";

    [Fact]
    public async Task FixedServiceBooking_StaysAtOriginalPrice_AfterBasePriceFeeAndSurchargeChange()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager");
        var anonymous = factory.CreateClient();

        var code = $"SNAP_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var create = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = code,
            Name = "Snapshot Fixed Service",
            BasePrice = 100m,
            PricingMode = ServicePricingMode.Fixed,
            IsTaxable = false
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var service = (await create.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>())!.Data!;

        var bookingResponse = await anonymous.PostAsJsonAsync("/api/bookings", new CreateAnonymousBookingRequest
        {
            ServiceId = service.Id,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            ContactName = "Snapshot Client",
            Phone = "+254712345678",
            Address = new ServiceAddressRequest { Line1 = "1 Main Street", City = "Nairobi", Country = "Kenya" }
        });
        Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);
        var booking = (await bookingResponse.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;
        Assert.Equal(100m, booking.TotalPrice);
        Assert.Single(booking.PriceLines);
        Assert.Equal(100m, booking.PriceLines.Single().UnitPrice);

        // Change the base rate, add an always-applied fee, and add a surcharge after the booking
        // already exists.
        await manager.PutAsJsonAsync($"/api/services/{service.Id}", new UpdateServiceRequest
        {
            Code = code,
            Name = "Snapshot Fixed Service",
            BasePrice = 999m,
            PricingMode = ServicePricingMode.Fixed,
            IsTaxable = false
        });
        await manager.PostAsJsonAsync($"/api/services/{service.Id}/fees", new CreateServiceFeeRequest
        {
            Name = "Added After Booking",
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 50m
        });
        await manager.PostAsJsonAsync($"/api/services/{service.Id}/surcharges", new CreateServiceSurchargeRequest
        {
            Name = "Added After Booking Location Surcharge",
            TriggerType = SurchargeTriggerType.Location,
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 75m,
            LocationMatch = "Nairobi"
        });

        var detailResponse = await manager.GetAsync($"/api/bookings/{booking.Id}");
        var detail = (await detailResponse.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;
        Assert.Equal(100m, detail.TotalPrice);
        Assert.Single(detail.PriceLines);
        Assert.Equal(100m, detail.PriceLines.Single().UnitPrice);

        // A new quote for the same service now reflects every pricing change, proving the
        // calculator itself still reads live rates -- only the already-accepted booking is frozen.
        var quote = await anonymous.PostAsJsonAsync($"/api/services/{service.Id}/quote", new ServiceQuoteRequest
        {
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Address = new ServiceAddressRequest { Line1 = "1 Main Street", City = "Nairobi", Country = "Kenya" }
        });
        var quoteBody = (await quote.Content.ReadFromJsonAsync<ApiResponse<ServiceQuoteResponse>>())!.Data!;
        Assert.Equal(999m + 50m + 75m, quoteBody.Subtotal);
    }

    [Fact]
    public async Task PerUnitServiceBooking_StaysAtOriginalPrice_AfterPriceRuleRateChange()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager");
        var anonymous = factory.CreateClient();

        var code = $"SNAPU_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var create = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = code,
            Name = "Snapshot Per-Unit Service",
            BasePrice = 0m,
            PricingMode = ServicePricingMode.PerUnit,
            IsTaxable = false
        });
        var service = (await create.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>())!.Data!;

        var ruleCreate = await manager.PostAsJsonAsync($"/api/services/{service.Id}/pricing-rules", new CreateServicePriceRuleRequest
        {
            UnitName = "Room",
            UnitPrice = 10m
        });
        Assert.Equal(HttpStatusCode.Created, ruleCreate.StatusCode);
        var rule = (await ruleCreate.Content.ReadFromJsonAsync<ApiResponse<ServicePriceRuleDto>>())!.Data!;

        var bookingResponse = await anonymous.PostAsJsonAsync("/api/bookings", new CreateAnonymousBookingRequest
        {
            ServiceId = service.Id,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            ContactName = "Snapshot Client",
            Phone = "+254712345678",
            Address = new ServiceAddressRequest { Line1 = "1 Main Street", City = "Nairobi", Country = "Kenya" },
            PricingItems = [new BookingPriceItemRequest { PriceRuleId = rule.Id, Quantity = 3 }]
        });
        Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);
        var booking = (await bookingResponse.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;
        Assert.Equal(30m, booking.TotalPrice);

        // Raise the per-unit rate after the booking exists.
        var ruleUpdate = await manager.PutAsJsonAsync($"/api/services/{service.Id}/pricing-rules/{rule.Id}", new UpdateServicePriceRuleRequest
        {
            UnitName = "Room",
            UnitPrice = 1000m
        });
        Assert.Equal(HttpStatusCode.OK, ruleUpdate.StatusCode);

        var detailResponse = await manager.GetAsync($"/api/bookings/{booking.Id}");
        var detail = (await detailResponse.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;
        Assert.Equal(30m, detail.TotalPrice);
        var line = Assert.Single(detail.PriceLines);
        Assert.Equal(10m, line.UnitPrice);
        Assert.Equal(3, line.Quantity);

        var quote = await anonymous.PostAsJsonAsync($"/api/services/{service.Id}/quote", new ServiceQuoteRequest
        {
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            PricingItems = [new BookingPriceItemRequest { PriceRuleId = rule.Id, Quantity = 3 }]
        });
        var quoteBody = (await quote.Content.ReadFromJsonAsync<ApiResponse<ServiceQuoteResponse>>())!.Data!;
        Assert.Equal(3000m, quoteBody.Subtotal);
    }

    [Fact]
    public async Task TimeBasedServiceBooking_StaysAtOriginalPrice_AfterTimePricingPolicyRateChange()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager");
        var anonymous = factory.CreateClient();

        var code = $"SNAPT_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var create = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = code,
            Name = "Snapshot Time-Based Service",
            BasePrice = 0m,
            PricingMode = ServicePricingMode.TimeBased,
            IsTaxable = false
        });
        var service = (await create.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>())!.Data!;

        var policyUpsert = await manager.PutAsJsonAsync($"/api/services/{service.Id}/time-pricing-policy", new UpsertServiceTimePricingPolicyRequest
        {
            BillingUnit = TimePricingUnit.Hour,
            UnitPrice = 20m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 15,
            RoundingPolicy = TimeRoundingPolicy.Up
        });
        Assert.Equal(HttpStatusCode.OK, policyUpsert.StatusCode);

        var scheduledStart = DateTimeOffset.UtcNow.AddDays(1);
        var bookingResponse = await anonymous.PostAsJsonAsync("/api/bookings", new CreateAnonymousBookingRequest
        {
            ServiceId = service.Id,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledStart.AddHours(2),
            ContactName = "Snapshot Client",
            Phone = "+254712345678",
            Address = new ServiceAddressRequest { Line1 = "1 Main Street", City = "Nairobi", Country = "Kenya" }
        });
        Assert.Equal(HttpStatusCode.Created, bookingResponse.StatusCode);
        var booking = (await bookingResponse.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;
        Assert.Equal(40m, booking.TotalPrice); // 2 hours * 20/hour

        // Raise the hourly rate after the booking exists.
        var policyUpdate = await manager.PutAsJsonAsync($"/api/services/{service.Id}/time-pricing-policy", new UpsertServiceTimePricingPolicyRequest
        {
            BillingUnit = TimePricingUnit.Hour,
            UnitPrice = 500m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 15,
            RoundingPolicy = TimeRoundingPolicy.Up
        });
        Assert.Equal(HttpStatusCode.OK, policyUpdate.StatusCode);

        var detailResponse = await manager.GetAsync($"/api/bookings/{booking.Id}");
        var detail = (await detailResponse.Content.ReadFromJsonAsync<ApiResponse<BookingDto>>())!.Data!;
        Assert.Equal(40m, detail.TotalPrice);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = $"booking_price_snapshot_{Guid.NewGuid():N}";
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
