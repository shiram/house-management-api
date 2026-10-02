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

// Integration coverage for T389's public pricing projection: the public ServiceDto exposes
// read-only, non-sensitive summaries of a service's time-based rate, active fees, and active
// surcharge triggers, plus the platform tax rate/currency, without requiring a quote call.
public class PublicPricingProjectionIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PublicPricingProjectionIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(item => item.ServiceType == typeof(DbContextOptions<HouseContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<HouseContext>(options =>
                    options.UseInMemoryDatabase("public_pricing_projection_integration_db"));
            });
        });
    }

    [Fact]
    public async Task ServiceDetail_ProjectsTimePricingActiveFeesAndSurchargesToAnonymousClients()
    {
        var manager = CreateAuthenticatedClient("manager");
        var admin = CreateAuthenticatedClient("admin");
        var anonymous = _factory.CreateClient();

        var code = $"PROJ_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var create = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = code,
            Name = "Projection Service",
            BasePrice = 50m,
            PricingMode = ServicePricingMode.TimeBased,
            IsTaxable = true
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var service = (await create.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>())!.Data!;

        await manager.PutAsJsonAsync($"/api/services/{service.Id}/time-pricing-policy", new UpsertServiceTimePricingPolicyRequest
        {
            BillingUnit = TimePricingUnit.Hour,
            UnitPrice = 30m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 15,
            RoundingPolicy = TimeRoundingPolicy.Up,
            OvertimeThresholdMinutes = 120,
            OvertimeUnitPrice = 20m
        });

        var activeFee = await manager.PostAsJsonAsync($"/api/services/{service.Id}/fees", new CreateServiceFeeRequest
        {
            Name = "Platform fee",
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 5m
        });
        Assert.Equal(HttpStatusCode.Created, activeFee.StatusCode);

        var inactiveFeeCreate = await manager.PostAsJsonAsync($"/api/services/{service.Id}/fees", new CreateServiceFeeRequest
        {
            Name = "Retired fee",
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 1m
        });
        var inactiveFee = (await inactiveFeeCreate.Content.ReadFromJsonAsync<ApiResponse<ServiceFeeDto>>())!.Data!;
        await manager.PutAsync($"/api/services/{service.Id}/fees/{inactiveFee.Id}/activate?active=false", null);

        var surcharge = await manager.PostAsJsonAsync($"/api/services/{service.Id}/surcharges", new CreateServiceSurchargeRequest
        {
            Name = "Weekend surcharge",
            TriggerType = SurchargeTriggerType.Weekend,
            AdjustmentType = PricingAdjustmentType.Percentage,
            Amount = 10m
        });
        Assert.Equal(HttpStatusCode.Created, surcharge.StatusCode);

        var settingResponse = await admin.PutAsJsonAsync("/api/admin/settings/Pricing.TaxRatePercentage", new UpsertSystemSettingRequest
        {
            Value = "18"
        });
        Assert.Equal(HttpStatusCode.OK, settingResponse.StatusCode);

        var detail = await anonymous.GetFromJsonAsync<ApiResponse<ServiceDto>>($"/api/services/{service.Id}");
        var dto = detail!.Data!;

        Assert.NotNull(dto.TimePricing);
        Assert.Equal(TimePricingUnit.Hour, dto.TimePricing!.BillingUnit);
        Assert.Equal(30m, dto.TimePricing.UnitPrice);
        Assert.Equal(20m, dto.TimePricing.OvertimeUnitPrice);

        var fee = Assert.Single(dto.Fees);
        Assert.Equal("Platform fee", fee.Name);
        Assert.Equal(5m, fee.Amount);

        var surchargeDto = Assert.Single(dto.Surcharges);
        Assert.Equal("Weekend surcharge", surchargeDto.Name);
        Assert.Equal(SurchargeTriggerType.Weekend, surchargeDto.TriggerType);

        Assert.Equal(18m, dto.TaxRatePercentage);
        Assert.False(string.IsNullOrWhiteSpace(dto.Currency));

        var list = await anonymous.GetFromJsonAsync<ApiResponse<List<ServiceDto>>>("/api/services");
        var listedDto = Assert.Single(list!.Data!, item => item.Id == service.Id);
        Assert.NotNull(listedDto.TimePricing);
        Assert.Single(listedDto.Fees);
        Assert.Single(listedDto.Surcharges);
        Assert.Equal(18m, listedDto.TaxRatePercentage);
    }

    [Fact]
    public async Task ServiceDetail_OmitsTaxRate_ForNonTaxableServices()
    {
        var manager = CreateAuthenticatedClient("manager");
        var admin = CreateAuthenticatedClient("admin");
        var anonymous = _factory.CreateClient();

        await admin.PutAsJsonAsync("/api/admin/settings/Pricing.TaxRatePercentage", new UpsertSystemSettingRequest { Value = "18" });

        var code = $"PRJX_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var create = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = code,
            Name = "Exempt Projection Service",
            BasePrice = 50m,
            PricingMode = ServicePricingMode.Fixed,
            IsTaxable = false
        });
        var service = (await create.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>())!.Data!;

        var detail = await anonymous.GetFromJsonAsync<ApiResponse<ServiceDto>>($"/api/services/{service.Id}");
        Assert.Equal(0m, detail!.Data!.TaxRatePercentage);
        Assert.Null(detail.Data.TimePricing);
        Assert.Empty(detail.Data.Fees);
        Assert.Empty(detail.Data.Surcharges);
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
