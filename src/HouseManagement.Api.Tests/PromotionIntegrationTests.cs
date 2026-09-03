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

public class PromotionIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PromotionIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(item => item.ServiceType == typeof(DbContextOptions<HouseContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<HouseContext>(options =>
                    options.UseInMemoryDatabase("promotion_integration_db"));
            });
        });
    }

    [Fact]
    public async Task PromotionLifecycle_IsManagerProtected()
    {
        var anonymous = _factory.CreateClient();
        var code = $"PROMO_{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var request = new CreatePromotionRequest
        {
            Code = code,
            Name = "Launch discount",
            DiscountType = PromotionDiscountType.Percentage,
            DiscountValue = 15,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            EndsAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        var unauthorized = await anonymous.PostAsJsonAsync("/api/admin/promotions", request);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var manager = CreateAuthenticatedClient("manager");
        var create = await manager.PostAsJsonAsync("/api/admin/promotions", request);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ApiResponse<PromotionDto>>();
        Assert.NotNull(created?.Data);
        Assert.Equal(code, created.Data.Code);

        var list = await manager.GetFromJsonAsync<ApiResponse<List<PromotionDto>>>("/api/admin/promotions");
        Assert.Contains(list!.Data!, promotion => promotion.Code == code);

        var deactivate = await manager.PutAsync($"/api/admin/promotions/{created.Data.Id}/activate?active=false", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
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
