using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using HouseManagement.Api.DTOs;

namespace HouseManagement.Api.Tests.Integration;

public class HouseHelpsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HouseHelpsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private string CreateToken(string role)
    {
        var key = "PleaseChangeThisSecretOrSetEnvVar";
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var claims = new[] { new Claim(ClaimTypes.Role, role) };
        var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "HouseManagement",
            audience: "HouseManagement",
            claims: claims,
            expires: System.DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task Post_CreateHouseHelp_UnauthorizedWithoutToken()
    {
        var client = _factory.CreateClient();
        var req = new CreateHouseHelpRequest { FirstName = "I", LastName = "J", Phone = "+1", City = "X" };
        var resp = await client.PostAsJsonAsync("/api/househelps", req);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Post_CreateHouseHelp_AllowsAdmin()
    {
        var client = _factory.CreateClient();
        var token = CreateToken("admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var req = new CreateHouseHelpRequest { FirstName = "I", LastName = "J", Phone = "+1", City = "X" };
        var resp = await client.PostAsJsonAsync("/api/househelps", req);
        Assert.Equal(System.Net.HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task Post_CreateHouseHelp_ForbiddenForHouseHelpRole()
    {
        var client = _factory.CreateClient();
        var token = CreateToken("househelp");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var req = new CreateHouseHelpRequest { FirstName = "I", LastName = "J", Phone = "+1", City = "X" };
        var resp = await client.PostAsJsonAsync("/api/househelps", req);
        Assert.True(resp.StatusCode == System.Net.HttpStatusCode.Forbidden || resp.StatusCode == System.Net.HttpStatusCode.Unauthorized);
    }
}