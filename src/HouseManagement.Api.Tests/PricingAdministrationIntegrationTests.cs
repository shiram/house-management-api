using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HouseManagement.Api.Tests;

// Integration coverage for T388's manager/admin pricing administration endpoints: time-based
// rate policy, fees, surcharges, pricing versions, and the public holiday calendar.
public class PricingAdministrationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PricingAdministrationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(item => item.ServiceType == typeof(DbContextOptions<HouseContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<HouseContext>(options =>
                    options.UseInMemoryDatabase("pricing_administration_integration_db"));
            });
        });
    }

    [Fact]
    public async Task TimePricingPolicy_IsManagerProtectedAndRejectsInvalidOvertime()
    {
        var manager = CreateAuthenticatedClient("manager");
        var anonymous = _factory.CreateClient();
        var service = await CreateServiceAsync(manager, ServicePricingMode.TimeBased);

        var unauthorized = await anonymous.PutAsJsonAsync($"/api/services/{service.Id}/time-pricing-policy", new UpsertServiceTimePricingPolicyRequest
        {
            BillingUnit = TimePricingUnit.Hour,
            UnitPrice = 20m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 15
        });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var invalid = await manager.PutAsJsonAsync($"/api/services/{service.Id}/time-pricing-policy", new UpsertServiceTimePricingPolicyRequest
        {
            BillingUnit = TimePricingUnit.Hour,
            UnitPrice = 20m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 15,
            OvertimeThresholdMinutes = 30 // below minimum duration
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var valid = await manager.PutAsJsonAsync($"/api/services/{service.Id}/time-pricing-policy", new UpsertServiceTimePricingPolicyRequest
        {
            BillingUnit = TimePricingUnit.Hour,
            UnitPrice = 20m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 15,
            RoundingPolicy = TimeRoundingPolicy.Up
        });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        var body = await valid.Content.ReadFromJsonAsync<ApiResponse<ServiceTimePricingPolicyDto>>();
        Assert.Equal(20m, body!.Data!.UnitPrice);

        var missingService = await manager.PutAsJsonAsync("/api/services/999999/time-pricing-policy", new UpsertServiceTimePricingPolicyRequest
        {
            BillingUnit = TimePricingUnit.Hour,
            UnitPrice = 20m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 15
        });
        Assert.Equal(HttpStatusCode.NotFound, missingService.StatusCode);
    }

    [Fact]
    public async Task Fees_SupportCreateUpdateActivateAndRejectInvalidPercentage()
    {
        var manager = CreateAuthenticatedClient("manager");
        var anonymous = _factory.CreateClient();
        var service = await CreateServiceAsync(manager, ServicePricingMode.Fixed);

        var unauthorized = await anonymous.PostAsJsonAsync($"/api/services/{service.Id}/fees", new CreateServiceFeeRequest
        {
            Name = "Platform Fee",
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 5m
        });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var invalidPercentage = await manager.PostAsJsonAsync($"/api/services/{service.Id}/fees", new CreateServiceFeeRequest
        {
            Name = "Service Fee",
            AdjustmentType = PricingAdjustmentType.Percentage,
            Amount = 150m
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPercentage.StatusCode);

        var created = await manager.PostAsJsonAsync($"/api/services/{service.Id}/fees", new CreateServiceFeeRequest
        {
            Name = "Platform Fee",
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 5m
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var fee = (await created.Content.ReadFromJsonAsync<ApiResponse<ServiceFeeDto>>())!.Data!;

        var list = await manager.GetFromJsonAsync<ApiResponse<List<ServiceFeeDto>>>($"/api/services/{service.Id}/fees");
        Assert.Contains(list!.Data!, item => item.Id == fee.Id);

        var update = await manager.PutAsJsonAsync($"/api/services/{service.Id}/fees/{fee.Id}", new UpdateServiceFeeRequest
        {
            Name = "Platform Fee",
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 7.5m
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var deactivate = await manager.PutAsync($"/api/services/{service.Id}/fees/{fee.Id}/activate?active=false", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var missingFee = await manager.PutAsync($"/api/services/{service.Id}/fees/999999/activate?active=true", null);
        Assert.Equal(HttpStatusCode.NotFound, missingFee.StatusCode);
    }

    [Fact]
    public async Task Surcharges_RejectTriggerFieldsThatDoNotMatchTheTriggerType()
    {
        var manager = CreateAuthenticatedClient("manager");
        var service = await CreateServiceAsync(manager, ServicePricingMode.Fixed);

        var mismatched = await manager.PostAsJsonAsync($"/api/services/{service.Id}/surcharges", new CreateServiceSurchargeRequest
        {
            Name = "Weekend",
            TriggerType = SurchargeTriggerType.Weekend,
            AdjustmentType = PricingAdjustmentType.Percentage,
            Amount = 10m,
            LocationMatch = "Nairobi" // must be null for a Weekend trigger
        });
        Assert.Equal(HttpStatusCode.BadRequest, mismatched.StatusCode);

        var created = await manager.PostAsJsonAsync($"/api/services/{service.Id}/surcharges", new CreateServiceSurchargeRequest
        {
            Name = "After Hours",
            TriggerType = SurchargeTriggerType.AfterHours,
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 15m,
            AfterHoursStartMinutes = 1080,
            AfterHoursEndMinutes = 480
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var surcharge = (await created.Content.ReadFromJsonAsync<ApiResponse<ServiceSurchargeDto>>())!.Data!;
        Assert.Equal(1080, surcharge.AfterHoursStartMinutes);

        var list = await manager.GetFromJsonAsync<ApiResponse<List<ServiceSurchargeDto>>>($"/api/services/{service.Id}/surcharges");
        Assert.Contains(list!.Data!, item => item.Id == surcharge.Id);

        var update = await manager.PutAsJsonAsync($"/api/services/{service.Id}/surcharges/{surcharge.Id}", new UpdateServiceSurchargeRequest
        {
            Name = "Urgent Booking",
            TriggerType = SurchargeTriggerType.Urgent,
            AdjustmentType = PricingAdjustmentType.Percentage,
            Amount = 20m,
            UrgentLeadTimeMinutes = 120
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var deactivate = await manager.PutAsync($"/api/services/{service.Id}/surcharges/{surcharge.Id}/activate?active=false", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
    }

    [Fact]
    public async Task PricingVersions_CanBeDraftedAndPublished()
    {
        var manager = CreateAuthenticatedClient("manager");
        var service = await CreateServiceAsync(manager, ServicePricingMode.Fixed);

        var invalidDraft = await manager.PostAsJsonAsync($"/api/services/{service.Id}/pricing-versions", new CreateServicePricingVersionRequest
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1)
            // BasePrice omitted: invalid for a Fixed-mode service.
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidDraft.StatusCode);

        var draft = await manager.PostAsJsonAsync($"/api/services/{service.Id}/pricing-versions", new CreateServicePricingVersionRequest
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1),
            BasePrice = 150m
        });
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var version = (await draft.Content.ReadFromJsonAsync<ApiResponse<ServicePricingVersionDto>>())!.Data!;
        Assert.Equal(PricingVersionStatus.Draft, version.Status);

        var list = await manager.GetFromJsonAsync<ApiResponse<List<ServicePricingVersionDto>>>($"/api/services/{service.Id}/pricing-versions");
        Assert.Contains(list!.Data!, item => item.Id == version.Id);

        var publish = await manager.PostAsync($"/api/services/pricing-versions/{version.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var published = (await publish.Content.ReadFromJsonAsync<ApiResponse<ServicePricingVersionDto>>())!.Data!;
        Assert.Equal(PricingVersionStatus.Published, published.Status);

        var missingVersion = await manager.PostAsync("/api/services/pricing-versions/999999/publish", null);
        Assert.Equal(HttpStatusCode.NotFound, missingVersion.StatusCode);

        var republish = await manager.PostAsync($"/api/services/pricing-versions/{version.Id}/publish", null);
        Assert.Equal(HttpStatusCode.Conflict, republish.StatusCode);
    }

    [Fact]
    public async Task PublicHolidays_SupportCreateListAndDeleteAndRejectDuplicateDates()
    {
        var manager = CreateAuthenticatedClient("manager");
        var anonymous = _factory.CreateClient();
        var date = new DateOnly(2027, 1, 1);

        var unauthorized = await anonymous.PostAsJsonAsync("/api/admin/public-holidays", new CreatePublicHolidayRequest
        {
            Date = date,
            Name = "New Year's Day"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var created = await manager.PostAsJsonAsync("/api/admin/public-holidays", new CreatePublicHolidayRequest
        {
            Date = date,
            Name = "New Year's Day"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var holiday = (await created.Content.ReadFromJsonAsync<ApiResponse<PublicHolidayDto>>())!.Data!;

        var duplicate = await manager.PostAsJsonAsync("/api/admin/public-holidays", new CreatePublicHolidayRequest
        {
            Date = date,
            Name = "Also New Year's Day"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var list = await manager.GetFromJsonAsync<ApiResponse<List<PublicHolidayDto>>>("/api/admin/public-holidays");
        Assert.Contains(list!.Data!, item => item.Id == holiday.Id);

        var delete = await manager.DeleteAsync($"/api/admin/public-holidays/{holiday.Id}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var missingDelete = await manager.DeleteAsync($"/api/admin/public-holidays/{holiday.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missingDelete.StatusCode);
    }

    private async Task<ServiceDto> CreateServiceAsync(HttpClient manager, ServicePricingMode pricingMode)
    {
        var code = $"PADM_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var create = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = code,
            Name = "Pricing Admin Service",
            BasePrice = 100m,
            PricingMode = pricingMode
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return (await create.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>())!.Data!;
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
