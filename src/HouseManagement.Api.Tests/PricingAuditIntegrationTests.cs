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
using HouseManagement.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HouseManagement.Api.Tests;

// Integration coverage for T391: every pricing administration mutation (price rules, time
// pricing policy, fees, surcharges, pricing versions, public holidays) must write an AuditLog
// row identifying the acting manager/admin, the affected record, and the new values. These
// mutations never retroactively touch an already-accepted booking's price snapshot (T390);
// this suite only proves the audit trail for changes to the live catalog.
public class PricingAuditIntegrationTests
{
    private const string TestJwtKey = "PleaseChangeThisSecretOrSetEnvVar";

    [Fact]
    public async Task PriceRuleMutations_WriteAuditLogEntries()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager", 11);
        var service = await CreateServiceAsync(manager, ServicePricingMode.PerUnit);

        var created = await manager.PostAsJsonAsync($"/api/services/{service.Id}/pricing-rules", new CreateServicePriceRuleRequest
        {
            UnitName = "Room",
            UnitPrice = 25m
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var rule = (await created.Content.ReadFromJsonAsync<ApiResponse<ServicePriceRuleDto>>())!.Data!;

        var updated = await manager.PutAsJsonAsync($"/api/services/{service.Id}/pricing-rules/{rule.Id}", new UpdateServicePriceRuleRequest
        {
            UnitName = "Room",
            UnitPrice = 30m
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var activated = await manager.PutAsync($"/api/services/{service.Id}/pricing-rules/{rule.Id}/activate?active=false", null);
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        AssertAudit(db, AuditEventTypes.ServicePriceRuleCreated, nameof(ServicePriceRule), rule.Id, 11);
        AssertAudit(db, AuditEventTypes.ServicePriceRuleUpdated, nameof(ServicePriceRule), rule.Id, 11);
        AssertAudit(db, AuditEventTypes.ServicePriceRuleActivationChanged, nameof(ServicePriceRule), rule.Id, 11);
    }

    [Fact]
    public async Task TimePricingPolicyUpsert_WritesAuditLogEntry()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager", 12);
        var service = await CreateServiceAsync(manager, ServicePricingMode.TimeBased);

        var upserted = await manager.PutAsJsonAsync($"/api/services/{service.Id}/time-pricing-policy", new UpsertServiceTimePricingPolicyRequest
        {
            BillingUnit = TimePricingUnit.Hour,
            UnitPrice = 20m,
            MinimumBillableDurationMinutes = 60,
            BillingIncrementMinutes = 15
        });
        Assert.Equal(HttpStatusCode.OK, upserted.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        AssertAudit(db, AuditEventTypes.ServiceTimePricingPolicyUpdated, nameof(ServiceTimePricingPolicy), service.Id, 12);
    }

    [Fact]
    public async Task FeeMutations_WriteAuditLogEntries()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager", 13);
        var service = await CreateServiceAsync(manager, ServicePricingMode.Fixed);

        var created = await manager.PostAsJsonAsync($"/api/services/{service.Id}/fees", new CreateServiceFeeRequest
        {
            Name = "Platform Fee",
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 5m
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var fee = (await created.Content.ReadFromJsonAsync<ApiResponse<ServiceFeeDto>>())!.Data!;

        var updated = await manager.PutAsJsonAsync($"/api/services/{service.Id}/fees/{fee.Id}", new UpdateServiceFeeRequest
        {
            Name = "Platform Fee",
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 7m
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var activated = await manager.PutAsync($"/api/services/{service.Id}/fees/{fee.Id}/activate?active=false", null);
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        AssertAudit(db, AuditEventTypes.ServiceFeeCreated, nameof(ServiceFee), fee.Id, 13);
        AssertAudit(db, AuditEventTypes.ServiceFeeUpdated, nameof(ServiceFee), fee.Id, 13);
        AssertAudit(db, AuditEventTypes.ServiceFeeActivationChanged, nameof(ServiceFee), fee.Id, 13);
    }

    [Fact]
    public async Task SurchargeMutations_WriteAuditLogEntries()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager", 14);
        var service = await CreateServiceAsync(manager, ServicePricingMode.Fixed);

        var created = await manager.PostAsJsonAsync($"/api/services/{service.Id}/surcharges", new CreateServiceSurchargeRequest
        {
            Name = "Urgent Request",
            TriggerType = SurchargeTriggerType.Urgent,
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 10m,
            UrgentLeadTimeMinutes = 60
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var surcharge = (await created.Content.ReadFromJsonAsync<ApiResponse<ServiceSurchargeDto>>())!.Data!;

        var updated = await manager.PutAsJsonAsync($"/api/services/{service.Id}/surcharges/{surcharge.Id}", new UpdateServiceSurchargeRequest
        {
            Name = "Urgent Request",
            TriggerType = SurchargeTriggerType.Urgent,
            AdjustmentType = PricingAdjustmentType.FixedAmount,
            Amount = 15m,
            UrgentLeadTimeMinutes = 60
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var activated = await manager.PutAsync($"/api/services/{service.Id}/surcharges/{surcharge.Id}/activate?active=false", null);
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        AssertAudit(db, AuditEventTypes.ServiceSurchargeCreated, nameof(ServiceSurcharge), surcharge.Id, 14);
        AssertAudit(db, AuditEventTypes.ServiceSurchargeUpdated, nameof(ServiceSurcharge), surcharge.Id, 14);
        AssertAudit(db, AuditEventTypes.ServiceSurchargeActivationChanged, nameof(ServiceSurcharge), surcharge.Id, 14);
    }

    [Fact]
    public async Task PricingVersionDraftAndPublish_WriteAuditLogEntries()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager", 15);
        var service = await CreateServiceAsync(manager, ServicePricingMode.Fixed);

        var draft = await manager.PostAsJsonAsync($"/api/services/{service.Id}/pricing-versions", new CreateServicePricingVersionRequest
        {
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(1),
            BasePrice = 120m
        });
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var version = (await draft.Content.ReadFromJsonAsync<ApiResponse<ServicePricingVersionDto>>())!.Data!;

        var published = await manager.PostAsync($"/api/services/pricing-versions/{version.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        AssertAudit(db, AuditEventTypes.ServicePricingVersionCreated, nameof(ServicePricingVersion), version.Id, 15);
        AssertAudit(db, AuditEventTypes.ServicePricingVersionPublished, nameof(ServicePricingVersion), version.Id, 15);
    }

    [Fact]
    public async Task PublicHolidayMutations_WriteAuditLogEntries()
    {
        var factory = CreateFactory();
        var manager = CreateAuthenticatedClient(factory, "manager", 16);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(45));

        var created = await manager.PostAsJsonAsync("/api/admin/public-holidays", new CreatePublicHolidayRequest
        {
            Date = date,
            Name = "Founders' Day"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var holiday = (await created.Content.ReadFromJsonAsync<ApiResponse<PublicHolidayDto>>())!.Data!;

        var deleted = await manager.DeleteAsync($"/api/admin/public-holidays/{holiday.Id}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        AssertAudit(db, AuditEventTypes.PublicHolidayCreated, nameof(PublicHoliday), holiday.Id, 16);
        AssertAudit(db, AuditEventTypes.PublicHolidayDeleted, nameof(PublicHoliday), holiday.Id, 16);
    }

    private static void AssertAudit(HouseContext db, string action, string entityType, int entityId, int userId)
    {
        var entry = db.AuditLogs.SingleOrDefault(log =>
            log.Action == action && log.EntityType == entityType && log.EntityId == entityId);
        Assert.NotNull(entry);
        Assert.Equal(userId, entry!.UserId);
    }

    private static async Task<ServiceDto> CreateServiceAsync(HttpClient manager, ServicePricingMode pricingMode)
    {
        var code = $"PAUD_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var create = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = code,
            Name = "Pricing Audit Service",
            BasePrice = 100m,
            PricingMode = pricingMode
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return (await create.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>())!.Data!;
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var dbName = $"pricing_audit_{Guid.NewGuid():N}";
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
