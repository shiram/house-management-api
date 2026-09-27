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

public class ServiceCatalogIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ServiceCatalogIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(item => item.ServiceType == typeof(DbContextOptions<HouseContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<HouseContext>(options =>
                    options.UseInMemoryDatabase("service_catalog_integration_db"));
            });
        });
    }

    [Fact]
    public async Task ServiceLifecycle_IsPubliclyReadableAndManagerProtected()
    {
        var anonymous = _factory.CreateClient();
        var code = $"TEST_{Guid.NewGuid():N}"[..16].ToUpperInvariant();

        var unauthorized = await anonymous.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = code,
            Name = "Test Service",
            BasePrice = 20
        });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var manager = CreateAuthenticatedClient("manager");
        var create = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = code,
            Name = "Test Service",
            Description = "Integration service",
            BasePrice = 20
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>();
        Assert.NotNull(created?.Data);

        var list = await anonymous.GetFromJsonAsync<ApiResponse<List<ServiceDto>>>("/api/services");
        Assert.Contains(list!.Data!, service => service.Code == code);

        var detail = await anonymous.GetFromJsonAsync<ApiResponse<ServiceDto>>($"/api/services/{created!.Data!.Id}");
        Assert.Equal(code, detail!.Data!.Code);

        var update = await manager.PutAsJsonAsync($"/api/services/{created.Data.Id}", new UpdateServiceRequest
        {
            Code = code,
            Name = "Updated Service",
            BasePrice = 25
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var deactivate = await manager.PutAsync($"/api/services/{created.Data.Id}/activate?active=false", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var hiddenList = await anonymous.GetFromJsonAsync<ApiResponse<List<ServiceDto>>>("/api/services");
        Assert.DoesNotContain(hiddenList!.Data!, service => service.Code == code);
        var hiddenDetail = await anonymous.GetAsync($"/api/services/{created.Data.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenDetail.StatusCode);
    }

    [Fact]
    public async Task AdminServicesList_IncludesInactiveServices_ForManagerOrAdmin()
    {
        var manager = CreateAuthenticatedClient("manager");
        var code = $"TEST_{Guid.NewGuid():N}"[..16].ToUpperInvariant();

        var create = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = code,
            Name = "Admin Visible Service",
            BasePrice = 15
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>();

        var deactivate = await manager.PutAsync($"/api/services/{created!.Data!.Id}/activate?active=false", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var publicList = await manager.GetFromJsonAsync<ApiResponse<List<ServiceDto>>>("/api/services");
        Assert.DoesNotContain(publicList!.Data!, service => service.Code == code);

        var adminList = await manager.GetFromJsonAsync<ApiResponse<List<ServiceDto>>>("/api/admin/services");
        Assert.Contains(adminList!.Data!, service => service.Code == code && !service.IsActive);

        var inactiveServices = await manager.GetFromJsonAsync<ApiResponse<List<ServiceDto>>>("/api/admin/services?isActive=false");
        Assert.Contains(inactiveServices!.Data!, service => service.Code == code);
        Assert.All(inactiveServices.Data!, service => Assert.False(service.IsActive));

        var adminDetail = await manager.GetFromJsonAsync<ApiResponse<ServiceDto>>($"/api/admin/services/{created.Data.Id}");
        Assert.Equal(code, adminDetail!.Data!.Code);
        Assert.False(adminDetail.Data.IsActive);
    }

    [Fact]
    public async Task ServiceLifecycle_PreservesTimeBasedPricingMode()
    {
        var manager = CreateAuthenticatedClient("manager");
        var anonymous = _factory.CreateClient();
        var code = $"TIME_{Guid.NewGuid():N}"[..16].ToUpperInvariant();

        var create = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = code,
            Name = "Hourly Cleaning",
            BasePrice = 0,
            PricingMode = ServicePricingMode.TimeBased
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>();
        Assert.Equal(ServicePricingMode.TimeBased, created!.Data!.PricingMode);

        var detail = await anonymous.GetFromJsonAsync<ApiResponse<ServiceDto>>($"/api/services/{created.Data.Id}");
        Assert.Equal(ServicePricingMode.TimeBased, detail!.Data!.PricingMode);
    }

    [Fact]
    public async Task AdminServicesEndpoints_RejectUnauthenticatedAndNonManagerRoles()
    {
        var anonymous = _factory.CreateClient();
        var houseHelp = CreateAuthenticatedClient("househelp");

        var unauthenticatedResponse = await anonymous.GetAsync("/api/admin/services");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedResponse.StatusCode);

        var forbiddenResponse = await houseHelp.GetAsync("/api/admin/services");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task AdminServiceDetail_ReturnsNotFound_WhenServiceDoesNotExist()
    {
        var manager = CreateAuthenticatedClient("manager");

        var response = await manager.GetAsync("/api/admin/services/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Quote_CalculatesFixedPerUnitAndTimeBasedPricingWithoutCreatingABooking()
    {
        var manager = CreateAuthenticatedClient("manager");
        var anonymous = _factory.CreateClient();

        var fixedCode = $"QFIX_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var fixedCreate = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = fixedCode,
            Name = "Quote Fixed Service",
            BasePrice = 40m,
            PricingMode = ServicePricingMode.Fixed
        });
        var fixedService = (await fixedCreate.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>())!.Data!;

        var fixedQuote = await anonymous.PostAsJsonAsync($"/api/services/{fixedService.Id}/quote", new ServiceQuoteRequest
        {
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1)
        });
        Assert.Equal(HttpStatusCode.OK, fixedQuote.StatusCode);
        var fixedQuoteBody = await fixedQuote.Content.ReadFromJsonAsync<ApiResponse<ServiceQuoteResponse>>();
        Assert.Equal(40m, fixedQuoteBody!.Data!.Subtotal);
        Assert.Single(fixedQuoteBody.Data.PriceLines);

        var perUnitCode = $"QUNIT_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var perUnitCreate = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = perUnitCode,
            Name = "Quote Per Unit Service",
            BasePrice = 0,
            PricingMode = ServicePricingMode.PerUnit
        });
        var perUnitService = (await perUnitCreate.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>())!.Data!;
        var ruleCreate = await manager.PostAsJsonAsync($"/api/services/{perUnitService.Id}/pricing-rules", new CreateServicePriceRuleRequest
        {
            UnitName = "Room",
            UnitPrice = 10m
        });
        var rule = (await ruleCreate.Content.ReadFromJsonAsync<ApiResponse<ServicePriceRuleDto>>())!.Data!;

        var perUnitQuote = await anonymous.PostAsJsonAsync($"/api/services/{perUnitService.Id}/quote", new ServiceQuoteRequest
        {
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            PricingItems = [new BookingPriceItemRequest { PriceRuleId = rule.Id, Quantity = 3 }]
        });
        Assert.Equal(HttpStatusCode.OK, perUnitQuote.StatusCode);
        var perUnitQuoteBody = await perUnitQuote.Content.ReadFromJsonAsync<ApiResponse<ServiceQuoteResponse>>();
        Assert.Equal(30m, perUnitQuoteBody!.Data!.Subtotal);

        var missingItemsQuote = await anonymous.PostAsJsonAsync($"/api/services/{perUnitService.Id}/quote", new ServiceQuoteRequest
        {
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1)
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingItemsQuote.StatusCode);

        var notFoundQuote = await anonymous.PostAsJsonAsync("/api/services/999999/quote", new ServiceQuoteRequest
        {
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1)
        });
        Assert.Equal(HttpStatusCode.NotFound, notFoundQuote.StatusCode);

        // Fixed pricing does not use the schedule to calculate price, but the requested window is
        // still a real booking window, so an invalid/past schedule must be rejected here too.
        var pastScheduleQuote = await anonymous.PostAsJsonAsync($"/api/services/{fixedService.Id}/quote", new ServiceQuoteRequest
        {
            ScheduledStart = DateTimeOffset.UtcNow.AddMinutes(-30),
            ScheduledEnd = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        Assert.Equal(HttpStatusCode.BadRequest, pastScheduleQuote.StatusCode);
    }

    [Fact]
    public async Task Quote_AppliesActiveFeesAndTriggeredSurchargesThroughTheHttpPipeline()
    {
        var manager = CreateAuthenticatedClient("manager");
        var anonymous = _factory.CreateClient();

        var code = $"QFEE_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var createResponse = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = code,
            Name = "Quote Fee/Surcharge Service",
            BasePrice = 100m,
            PricingMode = ServicePricingMode.Fixed
        });
        var service = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>())!.Data!;

        var holidayDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10).Date);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();

            db.ServiceFees.Add(new ServiceFee
            {
                ServiceId = service.Id,
                Name = "Booking Fee",
                AdjustmentType = PricingAdjustmentType.FixedAmount,
                Amount = 5m,
                IsActive = true
            });
            db.ServiceFees.Add(new ServiceFee
            {
                ServiceId = service.Id,
                Name = "Inactive Fee",
                AdjustmentType = PricingAdjustmentType.FixedAmount,
                Amount = 999m,
                IsActive = false
            });
            db.ServiceSurcharges.Add(new ServiceSurcharge
            {
                ServiceId = service.Id,
                Name = "Holiday Surcharge",
                TriggerType = SurchargeTriggerType.Holiday,
                AdjustmentType = PricingAdjustmentType.Percentage,
                Amount = 20m,
                IsActive = true
            });
            db.PublicHolidays.Add(new PublicHoliday
            {
                Date = holidayDate,
                Name = "Integration Test Holiday"
            });

            await db.SaveChangesAsync();
        }

        var scheduledStart = new DateTimeOffset(holidayDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(9))), TimeSpan.Zero);
        var quote = await anonymous.PostAsJsonAsync($"/api/services/{service.Id}/quote", new ServiceQuoteRequest
        {
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledStart.AddHours(1)
        });

        Assert.Equal(HttpStatusCode.OK, quote.StatusCode);
        var quoteBody = await quote.Content.ReadFromJsonAsync<ApiResponse<ServiceQuoteResponse>>();

        // base 100 + fixed fee 5 + 20% holiday surcharge of the 100 base subtotal (20) = 125.
        Assert.Equal(125m, quoteBody!.Data!.Subtotal);
        Assert.Equal(3, quoteBody.Data.PriceLines.Count());
    }

    [Fact]
    public async Task Quote_AppliesTheConfiguredTaxRateOnlyToTaxableServices()
    {
        var manager = CreateAuthenticatedClient("manager");
        var admin = CreateAuthenticatedClient("admin");
        var anonymous = _factory.CreateClient();

        var taxableCode = $"QTAX_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var taxableCreate = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = taxableCode,
            Name = "Taxable Quote Service",
            BasePrice = 100m,
            PricingMode = ServicePricingMode.Fixed,
            IsTaxable = true
        });
        var taxableService = (await taxableCreate.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>())!.Data!;

        var exemptCode = $"QEXM_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var exemptCreate = await manager.PostAsJsonAsync("/api/services", new CreateServiceRequest
        {
            Code = exemptCode,
            Name = "Exempt Quote Service",
            BasePrice = 100m,
            PricingMode = ServicePricingMode.Fixed,
            IsTaxable = false
        });
        var exemptService = (await exemptCreate.Content.ReadFromJsonAsync<ApiResponse<ServiceDto>>())!.Data!;

        // Admin configures the platform-wide tax/VAT rate through the existing generic settings
        // endpoint; no dedicated pricing-tax administration endpoint is needed for this.
        var settingResponse = await admin.PutAsJsonAsync("/api/admin/settings/Pricing.TaxRatePercentage", new UpsertSystemSettingRequest
        {
            Value = "18"
        });
        Assert.Equal(HttpStatusCode.OK, settingResponse.StatusCode);

        var taxableQuote = await anonymous.PostAsJsonAsync($"/api/services/{taxableService.Id}/quote", new ServiceQuoteRequest
        {
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1)
        });
        Assert.Equal(HttpStatusCode.OK, taxableQuote.StatusCode);
        var taxableBody = await taxableQuote.Content.ReadFromJsonAsync<ApiResponse<ServiceQuoteResponse>>();
        Assert.Equal(100m, taxableBody!.Data!.Subtotal);
        Assert.Equal(18m, taxableBody.Data.TaxRatePercentage);
        Assert.Equal(18m, taxableBody.Data.TaxAmount);
        Assert.Equal(118m, taxableBody.Data.Total);
        Assert.Contains(taxableBody.Data.PriceLines, line => line.Description.StartsWith("Tax", StringComparison.Ordinal));

        var exemptQuote = await anonymous.PostAsJsonAsync($"/api/services/{exemptService.Id}/quote", new ServiceQuoteRequest
        {
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(1),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(1).AddHours(1)
        });
        Assert.Equal(HttpStatusCode.OK, exemptQuote.StatusCode);
        var exemptBody = await exemptQuote.Content.ReadFromJsonAsync<ApiResponse<ServiceQuoteResponse>>();
        Assert.Equal(0m, exemptBody!.Data!.TaxAmount);
        Assert.Equal(100m, exemptBody.Data.Total);
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
