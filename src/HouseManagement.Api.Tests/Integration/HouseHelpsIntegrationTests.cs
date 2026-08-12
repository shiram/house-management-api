using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
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

    [Fact]
    public async Task Get_List_IsPubliclyAccessible()
    {
        var client = _factory.CreateClient();
        // create an item as admin
        var token = CreateToken("admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var req = new CreateHouseHelpRequest { FirstName = "L", LastName = "M", Phone = "+1", City = "Y" };
        var createResp = await client.PostAsJsonAsync("/api/househelps", req);
        createResp.EnsureSuccessStatusCode();

        // anonymous client
        var anon = _factory.CreateClient();
        var listResp = await anon.GetAsync("/api/househelps");
        Assert.Equal(System.Net.HttpStatusCode.OK, listResp.StatusCode);
        var items = await listResp.Content.ReadFromJsonAsync<List<HouseHelpDto>>();
        Assert.NotNull(items);
        Assert.NotEmpty(items!);
    }

    [Fact]
    public async Task Get_Detail_IsPubliclyAccessible()
    {
        var client = _factory.CreateClient();
        var token = CreateToken("admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var req = new CreateHouseHelpRequest { FirstName = "P", LastName = "Q", Phone = "+1", City = "Z" };
        var createResp = await client.PostAsJsonAsync("/api/househelps", req);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<HouseHelpDto>();
        Assert.NotNull(created);

        var anon = _factory.CreateClient();
        var detailResp = await anon.GetAsync($"/api/househelps/{created!.Id}");
        Assert.Equal(System.Net.HttpStatusCode.OK, detailResp.StatusCode);
        var dto = await detailResp.Content.ReadFromJsonAsync<HouseHelpDto>();
        Assert.NotNull(dto);
        Assert.Equal(created.Id, dto!.Id);
    }

    [Fact]
    public async Task Put_Activate_AllowsManager()
    {
        var client = _factory.CreateClient();
        var adminToken = CreateToken("admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var req = new CreateHouseHelpRequest { FirstName = "A1", LastName = "B1", Phone = "+1", City = "C1" };
        var createResp = await client.PostAsJsonAsync("/api/househelps", req);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<HouseHelpDto>();
        Assert.NotNull(created);

        // manager toggles active=false
        var managerClient = _factory.CreateClient();
        var managerToken = CreateToken("manager");
        managerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var actResp = await managerClient.PutAsync($"/api/househelps/{created!.Id}/activate?active=false", null);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, actResp.StatusCode);

        // verify
        var anon = _factory.CreateClient();
        var detail = await anon.GetFromJsonAsync<HouseHelpDto>($"/api/househelps/{created.Id}");
        Assert.False(detail!.IsActive);
    }

    [Fact]
    public async Task Put_Activate_ForbidHouseHelpRole()
    {
        var client = _factory.CreateClient();
        var adminToken = CreateToken("admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var req = new CreateHouseHelpRequest { FirstName = "A2", LastName = "B2", Phone = "+1", City = "C2" };
        var createResp = await client.PostAsJsonAsync("/api/househelps", req);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<HouseHelpDto>();
        Assert.NotNull(created);

        var hhClient = _factory.CreateClient();
        var hhToken = CreateToken("househelp");
        hhClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hhToken);
        var actResp = await hhClient.PutAsync($"/api/househelps/{created!.Id}/activate?active=false", null);
        Assert.True(actResp.StatusCode == System.Net.HttpStatusCode.Forbidden || actResp.StatusCode == System.Net.HttpStatusCode.Unauthorized);
    }
}