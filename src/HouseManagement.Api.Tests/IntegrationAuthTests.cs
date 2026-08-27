using System.Net.Http.Json;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using System.Net;

namespace HouseManagement.Api.Tests;

public class IntegrationAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IntegrationAuthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<HouseContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<HouseContext>(options =>
                {
                    options.UseInMemoryDatabase("integration_auth_db");
                });
            });
        });
    }

    [Fact]
    public async Task Register_Then_Login_ReturnsToken_And_DefaultRole()
    {
        var client = _factory.CreateClient();

        var register = new RegisterRequest { UserName = "intuser", Email = "int@example.com", Password = "Password123!" };
        var regResp = await client.PostAsJsonAsync("/api/auth/register", register);
        Assert.Equal(HttpStatusCode.OK, regResp.StatusCode);

        var regJson = await regResp.Content.ReadFromJsonAsync<JsonElement>();
        var token = regJson.GetProperty("data").GetProperty("token").GetString();
        var role = regJson.GetProperty("data").GetProperty("role").GetString();
        Assert.Equal("househelp", role);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var login = new LoginRequest { Email = "int@example.com", Password = "Password123!" };
        var loginResp = await client.PostAsJsonAsync("/api/auth/login", login);
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        var loginToken = loginJson.GetProperty("data").GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(loginToken));
    }
}