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
using HouseManagement.Api.Common.Api;
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
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();
        Assert.NotNull(envelope);
        Assert.Equal(201, envelope!.StatusCode);
        Assert.Equal("HouseHelp created", envelope.Message);
        Assert.NotNull(envelope.Data);
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
    public async Task Post_CreateHouseHelp_ReturnsValidationEnvelope_WhenPayloadInvalid()
    {
        var client = _factory.CreateClient();
        var token = CreateToken("admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var req = new CreateHouseHelpRequest { LastName = "J", Phone = "bad", City = "X" };
        var resp = await client.PostAsJsonAsync("/api/househelps", req);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.StatusCode);
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<Dictionary<string, string[]>>>();
        Assert.NotNull(envelope);
        Assert.Equal(400, envelope!.StatusCode);
        Assert.Equal("Validation failed", envelope.Message);
        Assert.Contains("FirstName", envelope.Data.Keys);
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
        var envelope = await listResp.Content.ReadFromJsonAsync<ApiResponse<List<HouseHelpDto>>>();
        Assert.NotNull(envelope);
        Assert.Equal(200, envelope!.StatusCode);
        Assert.NotNull(envelope.Data);
        Assert.NotEmpty(envelope.Data!);
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
        var createdEnvelope = await createResp.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();
        Assert.NotNull(createdEnvelope);
        Assert.NotNull(createdEnvelope!.Data);

        var anon = _factory.CreateClient();
        var detailResp = await anon.GetAsync($"/api/househelps/{createdEnvelope.Data!.Id}");
        Assert.Equal(System.Net.HttpStatusCode.OK, detailResp.StatusCode);
        var envelope = await detailResp.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();
        Assert.NotNull(envelope);
        Assert.Equal(createdEnvelope.Data.Id, envelope!.Data!.Id);
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
        var createdEnvelope = await createResp.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();
        Assert.NotNull(createdEnvelope);
        Assert.NotNull(createdEnvelope!.Data);

        // manager toggles active=false
        var managerClient = _factory.CreateClient();
        var managerToken = CreateToken("manager");
        managerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var actResp = await managerClient.PutAsync($"/api/househelps/{createdEnvelope.Data!.Id}/activate?active=false", null);
        Assert.Equal(System.Net.HttpStatusCode.OK, actResp.StatusCode);
        var envelope = await actResp.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        Assert.NotNull(envelope);
        Assert.Equal(200, envelope!.StatusCode);
        Assert.Equal("HouseHelp status updated", envelope.Message);

        // verify
        var anon = _factory.CreateClient();
        var detailEnvelope = await anon.GetFromJsonAsync<ApiResponse<HouseHelpDto>>($"/api/househelps/{createdEnvelope.Data!.Id}");
        Assert.False(detailEnvelope!.Data!.IsActive);
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
        var createdEnvelope = await createResp.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();
        Assert.NotNull(createdEnvelope);
        Assert.NotNull(createdEnvelope!.Data);

        var hhClient = _factory.CreateClient();
        var hhToken = CreateToken("househelp");
        hhClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hhToken);
        var actResp = await hhClient.PutAsync($"/api/househelps/{createdEnvelope.Data!.Id}/activate?active=false", null);
        Assert.True(actResp.StatusCode == System.Net.HttpStatusCode.Forbidden || actResp.StatusCode == System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminHouseHelps_List_SupportsUserIdFilter_ForManagerOrAdmin()
    {
        var client = _factory.CreateClient();
        var adminToken = CreateToken("admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var registerEmail = $"admin-hh-{System.Guid.NewGuid():N}@example.com";
        var registerResp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            UserName = registerEmail,
            Email = registerEmail,
            Password = "Password123!"
        });
        registerResp.EnsureSuccessStatusCode();

        var usersEnvelope = await client.GetFromJsonAsync<ApiResponse<List<UserDto>>>("/api/admin/users?page=1&pageSize=1000");
        var linkedUserId = usersEnvelope!.Data!.Single(u => u.Email == registerEmail).Id;

        var req = new CreateHouseHelpRequest { UserId = linkedUserId, FirstName = "Ad1", LastName = "Min1", Phone = "+1", City = "AdminCity" };
        var createResp = await client.PostAsJsonAsync("/api/househelps", req);
        createResp.EnsureSuccessStatusCode();
        var createdEnvelope = await createResp.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();

        var byUserId = await client.GetFromJsonAsync<ApiResponse<List<HouseHelpDto>>>($"/api/admin/househelps?userId={linkedUserId}");
        Assert.NotNull(byUserId);
        Assert.Single(byUserId!.Data!);
        Assert.Equal(createdEnvelope!.Data!.Id, byUserId.Data![0].Id);

        var detail = await client.GetFromJsonAsync<ApiResponse<HouseHelpDto>>($"/api/admin/househelps/{createdEnvelope.Data!.Id}");
        Assert.Equal(createdEnvelope.Data.Id, detail!.Data!.Id);
    }

    [Fact]
    public async Task AdminHouseHelps_RejectUnauthenticatedAndNonManagerRoles()
    {
        var anonymous = _factory.CreateClient();
        var houseHelpClient = _factory.CreateClient();
        houseHelpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("househelp"));

        var unauthenticatedResponse = await anonymous.GetAsync("/api/admin/househelps");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, unauthenticatedResponse.StatusCode);

        var forbiddenResponse = await houseHelpClient.GetAsync("/api/admin/househelps");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task AdminHouseHelpDetail_ReturnsNotFound_WhenMissing()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin"));

        var response = await client.GetAsync("/api/admin/househelps/999999");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}