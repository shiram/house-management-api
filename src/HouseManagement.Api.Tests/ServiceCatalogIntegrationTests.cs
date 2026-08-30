using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Data;
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

        var adminDetail = await manager.GetFromJsonAsync<ApiResponse<ServiceDto>>($"/api/admin/services/{created.Data.Id}");
        Assert.Equal(code, adminDetail!.Data!.Code);
        Assert.False(adminDetail.Data.IsActive);
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
