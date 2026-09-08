using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using HouseManagement.Api.Common;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Infrastructure.Files;
using SkiaSharp;

namespace HouseManagement.Api.Tests.Integration;

public class HouseHelpsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _profileImageRoot;

    public HouseHelpsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = $"househelps_integration_{Guid.NewGuid()}";
        _profileImageRoot = Path.Combine(
            Path.GetTempPath(),
            "house-management-profile-images",
            Guid.NewGuid().ToString("N"));
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(item => item.ServiceType == typeof(DbContextOptions<HouseContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<HouseContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
                services.PostConfigure<ProfileImageOptions>(options =>
                    options.StorageRootPath = _profileImageRoot);
            });
        });
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (Directory.Exists(_profileImageRoot))
        {
            Directory.Delete(_profileImageRoot, true);
        }
    }

    private string CreateToken(string role, int? userId = null)
    {
        var key = "PleaseChangeThisSecretOrSetEnvVar";
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId.Value.ToString()));
        }

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
        var content = await listResp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"userId\"", content);
        Assert.DoesNotContain("\"phone\"", content);
        Assert.DoesNotContain("\"address\"", content);
        Assert.DoesNotContain("\"isActive\"", content);
        Assert.DoesNotContain("\"profileImageStorageKey\"", content);
        Assert.DoesNotContain("\"profileImageContentType\"", content);
        Assert.DoesNotContain("\"profileImageSizeBytes\"", content);
        var envelope = JsonSerializer.Deserialize<ApiResponse<List<PublicHouseHelpDto>>>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
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
        var content = await detailResp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"userId\"", content);
        Assert.DoesNotContain("\"phone\"", content);
        Assert.DoesNotContain("\"address\"", content);
        Assert.DoesNotContain("\"isActive\"", content);
        Assert.DoesNotContain("\"profileImageStorageKey\"", content);
        Assert.DoesNotContain("\"profileImageContentType\"", content);
        Assert.DoesNotContain("\"profileImageSizeBytes\"", content);
        var envelope = JsonSerializer.Deserialize<ApiResponse<PublicHouseHelpDto>>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
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
        var managerToken = CreateToken("manager", 2);
        managerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var actResp = await managerClient.PutAsync($"/api/househelps/{createdEnvelope.Data!.Id}/activate?active=false", null);
        Assert.Equal(System.Net.HttpStatusCode.OK, actResp.StatusCode);
        var envelope = await actResp.Content.ReadFromJsonAsync<ApiResponse<object?>>();
        Assert.NotNull(envelope);
        Assert.Equal(200, envelope!.StatusCode);
        Assert.Equal("HouseHelp status updated", envelope.Message);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var audit = await db.AuditLogs.AsNoTracking().SingleAsync(log =>
                log.Action == AuditEventTypes.HouseHelpActivationChanged &&
                log.EntityId == createdEnvelope.Data.Id);
            Assert.Equal(2, audit.UserId);
            Assert.Equal("True -> False", audit.Details);
        }

        // Inactive profiles remain available to managers through the administrative API only.
        var anon = _factory.CreateClient();
        var detailResponse = await anon.GetAsync($"/api/househelps/{createdEnvelope.Data!.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, detailResponse.StatusCode);
    }

    [Fact]
    public async Task Put_Update_AuditsPrivateFieldNamesWithoutValues()
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", 1));
        var createResponse = await admin.PostAsJsonAsync("/api/househelps", new CreateHouseHelpRequest
        {
            FirstName = "Directory",
            LastName = "Before",
            Phone = "+256700000020",
            City = "Kampala",
            Address = "Old private address",
            Skills = ["Cleaning"]
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();

        var manager = _factory.CreateClient();
        manager.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("manager", 2));
        var response = await manager.PutAsJsonAsync(
            $"/api/househelps/{created!.Data!.Id}",
            new UpdateHouseHelpRequest
            {
                FirstName = "Directory",
                LastName = "After",
                Phone = "+256700000021",
                City = "Entebbe",
                Address = "New private address",
                Skills = ["Laundry"]
            });

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(log =>
            log.Action == AuditEventTypes.HouseHelpProfileUpdated &&
            log.EntityId == created.Data.Id);
        Assert.Equal(2, audit.UserId);
        Assert.Contains(nameof(HouseManagement.Api.Models.HouseHelp.Phone), audit.Details);
        Assert.Contains(nameof(HouseManagement.Api.Models.HouseHelp.Address), audit.Details);
        Assert.Contains(nameof(HouseManagement.Api.Models.HouseHelp.Skills), audit.Details);
        Assert.DoesNotContain("+256700000021", audit.Details);
        Assert.DoesNotContain("New private address", audit.Details);
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
    public async Task Put_Activate_RejectsManagerWithoutActorClaimBeforeMutation()
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", 1));
        var createResponse = await admin.PostAsJsonAsync("/api/househelps", new CreateHouseHelpRequest
        {
            FirstName = "Actor",
            LastName = "Required",
            Phone = "+256700000022",
            City = "Kampala"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();

        var managerWithoutActor = _factory.CreateClient();
        managerWithoutActor.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("manager"));

        var response = await managerWithoutActor.PutAsync(
            $"/api/househelps/{created!.Data!.Id}/activate?active=false",
            null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        var houseHelp = await db.HouseHelps.AsNoTracking()
            .SingleAsync(item => item.Id == created.Data.Id);
        Assert.True(houseHelp.IsActive);
        Assert.False(await db.AuditLogs.AnyAsync(log =>
            log.Action == AuditEventTypes.HouseHelpActivationChanged &&
            log.EntityId == created.Data.Id));
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

    [Fact]
    public async Task Put_OwnProfile_UpdatesOnlyCurrentHouseHelpAllowedFields()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", 1));

        var createResp = await client.PostAsJsonAsync("/api/househelps", new CreateHouseHelpRequest
        {
            UserId = 501,
            FirstName = "Self",
            LastName = "Before",
            Phone = "+256700000001",
            City = "Kampala",
            Address = "Old address"
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();

        var adminUpdate = await client.PutAsJsonAsync($"/api/househelps/{created!.Data!.Id}/profile", new UpdateHouseHelpProfileRequest
        {
            FirstName = "Self",
            LastName = "Before",
            Phone = "+256700000001",
            City = "Kampala",
            Address = "Old address",
            Bio = "Old bio",
            YearsOfExperience = 2,
            Languages = "English",
            EmergencyContactName = "Private Contact",
            EmergencyContactPhone = "+256700000002",
            NationalIdLast4 = "ABCD",
            VerificationStatus = HouseManagement.Api.Models.HouseHelpVerificationStatus.Verified
        });
        adminUpdate.EnsureSuccessStatusCode();

        var houseHelpClient = _factory.CreateClient();
        houseHelpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("househelp", 501));
        var response = await houseHelpClient.PutAsJsonAsync("/api/househelps/me/profile", new UpdateOwnHouseHelpProfileRequest
        {
            FirstName = "Self",
            LastName = "After",
            Phone = "+256700000003",
            City = "Entebbe",
            Address = "New address",
            Bio = "Updated public bio",
            YearsOfExperience = 4,
            Languages = "English,Luganda"
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"userId\"", content);
        Assert.DoesNotContain("EmergencyContactName", content);
        Assert.DoesNotContain("NationalIdLast4", content);

        var envelope = JsonSerializer.Deserialize<ApiResponse<OwnHouseHelpProfileDto>>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(envelope);
        Assert.Equal("Self", envelope!.Data!.FirstName);
        Assert.Equal("After", envelope.Data.LastName);
        Assert.Equal("+256700000003", envelope.Data.Phone);
        Assert.Equal("Updated public bio", envelope.Data.Bio);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var audit = await db.AuditLogs.AsNoTracking().SingleAsync(log =>
                log.Action == AuditEventTypes.HouseHelpProfileUpdated &&
                log.EntityId == created.Data.Id &&
                log.UserId == 501);
            Assert.Contains("Scope: self-service", audit.Details);
            Assert.Contains(nameof(HouseManagement.Api.Models.HouseHelp.Phone), audit.Details);
            Assert.DoesNotContain("+256700000003", audit.Details);
            Assert.DoesNotContain("New address", audit.Details);
        }

        var adminRead = await client.PutAsJsonAsync($"/api/househelps/{created.Data.Id}/profile", new UpdateHouseHelpProfileRequest
        {
            FirstName = "Self",
            LastName = "After",
            Phone = "+256700000003",
            City = "Entebbe",
            Address = "New address",
            Bio = "Updated public bio",
            YearsOfExperience = 4,
            Languages = "English,Luganda",
            EmergencyContactName = "Private Contact",
            EmergencyContactPhone = "+256700000002",
            NationalIdLast4 = "ABCD",
            VerificationStatus = HouseManagement.Api.Models.HouseHelpVerificationStatus.Verified
        });
        adminRead.EnsureSuccessStatusCode();
        var adminEnvelope = await adminRead.Content.ReadFromJsonAsync<ApiResponse<HouseHelpProfileDto>>();
        Assert.Equal("Private Contact", adminEnvelope!.Data!.EmergencyContactName);
        Assert.Equal("ABCD", adminEnvelope.Data.NationalIdLast4);
        Assert.Equal("Verified", adminEnvelope.Data.VerificationStatus);
    }

    [Fact]
    public async Task Get_OwnProfile_UsesAuthenticatedUserClaim()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", 1));
        var createResp = await client.PostAsJsonAsync("/api/househelps", new CreateHouseHelpRequest
        {
            UserId = 777,
            FirstName = "Claim",
            LastName = "Owner",
            Phone = "+256700000004",
            City = "Kampala"
        });
        createResp.EnsureSuccessStatusCode();

        var houseHelpClient = _factory.CreateClient();
        houseHelpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("househelp", 777));

        var response = await houseHelpClient.GetAsync("/api/househelps/me/profile");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<OwnHouseHelpProfileDto>>();
        Assert.Equal("Claim", envelope!.Data!.FirstName);
        Assert.Equal("Owner", envelope.Data.LastName);
    }

    [Fact]
    public async Task Put_Profile_AllowsManagerToUpdateOperationalProfileFields()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", 1));
        var createResp = await client.PostAsJsonAsync("/api/househelps", new CreateHouseHelpRequest
        {
            FirstName = "Ops",
            LastName = "Before",
            Phone = "+256700000005",
            City = "Kampala"
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();

        var manager = _factory.CreateClient();
        manager.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("manager", 2));
        var response = await manager.PutAsJsonAsync($"/api/househelps/{created!.Data!.Id}/profile", new UpdateHouseHelpProfileRequest
        {
            FirstName = "Ops",
            LastName = "After",
            Phone = "+256700000006",
            City = "Jinja",
            Address = "Operations address",
            Bio = "Operational profile bio",
            YearsOfExperience = 6,
            Languages = "English,Swahili",
            EmergencyContactName = "Ops Emergency",
            EmergencyContactPhone = "+256700000007",
            NationalIdLast4 = "WXYZ",
            VerificationStatus = HouseManagement.Api.Models.HouseHelpVerificationStatus.PendingReview
        });

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<HouseHelpProfileDto>>();
        Assert.Equal("After", envelope!.Data!.LastName);
        Assert.Equal("Jinja", envelope.Data.City);
        Assert.Equal("Ops Emergency", envelope.Data.EmergencyContactName);
        Assert.Equal("WXYZ", envelope.Data.NationalIdLast4);
        Assert.Equal("PendingReview", envelope.Data.VerificationStatus);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(log =>
            log.Action == AuditEventTypes.HouseHelpProfileUpdated &&
            log.EntityId == created.Data.Id);
        Assert.Equal(2, audit.UserId);
        Assert.Contains("Scope: management", audit.Details);
        Assert.Contains(nameof(HouseManagement.Api.Models.HouseHelp.VerificationStatus), audit.Details);
        Assert.Contains(nameof(HouseManagement.Api.Models.HouseHelp.EmergencyContactPhone), audit.Details);
        Assert.DoesNotContain("+256700000007", audit.Details);
        Assert.DoesNotContain("WXYZ", audit.Details);
    }

    [Fact]
    public async Task Put_Profile_RejectsHouseHelpRole()
    {
        var houseHelpClient = _factory.CreateClient();
        houseHelpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("househelp", 123));

        var response = await houseHelpClient.PutAsJsonAsync("/api/househelps/1/profile", new UpdateHouseHelpProfileRequest
        {
            FirstName = "Blocked",
            LastName = "User",
            Phone = "+256700000008",
            City = "Kampala",
            VerificationStatus = HouseManagement.Api.Models.HouseHelpVerificationStatus.Verified
        });

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_OwnProfileImage_UsesClaimOwnershipAndDoesNotExposeStorageKey()
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", 1));
        var createResponse = await admin.PostAsJsonAsync("/api/househelps", new CreateHouseHelpRequest
        {
            UserId = 901,
            FirstName = "Image",
            LastName = "Owner",
            Phone = "+256700000009",
            City = "Kampala"
        });
        createResponse.EnsureSuccessStatusCode();

        var houseHelp = _factory.CreateClient();
        houseHelp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("househelp", 901));
        using var upload = CreateImageUpload(PngBytes(), "image/png", "profile.png");

        var response = await houseHelp.PutAsync("/api/househelps/me/profile-image", upload);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("storageKey", content, StringComparison.OrdinalIgnoreCase);
        var envelope = JsonSerializer.Deserialize<ApiResponse<OwnHouseHelpProfileDto>>(
            content,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("image/png", envelope!.Data!.ProfileImageContentType);
        Assert.True(envelope.Data.ProfileImageSizeBytes > 0);
        Assert.NotNull(envelope.Data.ProfileImageUpdatedAt);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        var stored = await db.HouseHelps.AsNoTracking().SingleAsync(item => item.UserId == 901);
        Assert.NotNull(stored.ProfileImageStorageKey);
        var storedPath = GetStoredImagePath(stored.ProfileImageStorageKey!);
        Assert.True(File.Exists(storedPath));
        Assert.Equal(stored.ProfileImageSizeBytes, new FileInfo(storedPath).Length);

        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(log =>
            log.Action == AuditEventTypes.HouseHelpProfileImageUpdated &&
            log.EntityId == stored.Id);
        Assert.Equal(901, audit.UserId);
        Assert.Contains("Scope: self-service", audit.Details);
        Assert.Contains("Operation: upload", audit.Details);
        Assert.DoesNotContain("ContentType", audit.Details);
        Assert.DoesNotContain("SizeBytes", audit.Details);
        Assert.DoesNotContain(stored.ProfileImageStorageKey!, audit.Details);
    }

    [Fact]
    public async Task Put_ProfileImage_ReplacesPersistedImageAndRemovesSupersededFile()
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", 1));
        var createResponse = await admin.PostAsJsonAsync("/api/househelps", new CreateHouseHelpRequest
        {
            UserId = 902,
            FirstName = "Replace",
            LastName = "Image",
            Phone = "+256700000010",
            City = "Kampala"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();

        var houseHelp = _factory.CreateClient();
        houseHelp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("househelp", 902));
        using (var firstUpload = CreateImageUpload(PngBytes(), "image/png", "first.png"))
        {
            (await houseHelp.PutAsync("/api/househelps/me/profile-image", firstUpload)).EnsureSuccessStatusCode();
        }

        string firstStorageKey;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            firstStorageKey = (await db.HouseHelps.AsNoTracking()
                .SingleAsync(item => item.Id == created!.Data!.Id)).ProfileImageStorageKey!;
        }

        var manager = _factory.CreateClient();
        manager.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("manager", 2));
        using var replacement = CreateImageUpload(PngBytes(), "image/png", "replacement.png");

        var response = await manager.PutAsync(
            $"/api/househelps/{created!.Data!.Id}/profile-image",
            replacement);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<HouseContext>();
        var stored = await verificationDb.HouseHelps.AsNoTracking()
            .SingleAsync(item => item.Id == created.Data.Id);
        Assert.Equal("image/png", stored.ProfileImageContentType);
        Assert.NotEqual(firstStorageKey, stored.ProfileImageStorageKey);
        Assert.False(File.Exists(GetStoredImagePath(firstStorageKey)));
        Assert.True(File.Exists(GetStoredImagePath(stored.ProfileImageStorageKey!)));

        var audit = await verificationDb.AuditLogs.AsNoTracking().SingleAsync(log =>
            log.Action == AuditEventTypes.HouseHelpProfileImageUpdated &&
            log.EntityId == created.Data.Id &&
            log.UserId == 2);
        Assert.Contains("Scope: management", audit.Details);
        Assert.Contains("Operation: replacement", audit.Details);
        Assert.DoesNotContain("ContentType", audit.Details);
        Assert.DoesNotContain("SizeBytes", audit.Details);
        Assert.DoesNotContain(firstStorageKey, audit.Details);
        Assert.DoesNotContain(stored.ProfileImageStorageKey!, audit.Details);
    }

    [Fact]
    public async Task Put_ProfileImage_RejectsInvalidContentAndHouseHelpManagementAccess()
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", 1));
        var createResponse = await admin.PostAsJsonAsync("/api/househelps", new CreateHouseHelpRequest
        {
            UserId = 903,
            FirstName = "Secure",
            LastName = "Upload",
            Phone = "+256700000011",
            City = "Kampala"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();

        var houseHelp = _factory.CreateClient();
        houseHelp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("househelp", 903));
        using var unauthorizedUpload = CreateImageUpload(PngBytes(), "image/png", "profile.png");
        var forbidden = await houseHelp.PutAsync(
            $"/api/househelps/{created!.Data!.Id}/profile-image",
            unauthorizedUpload);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var invalidUpload = CreateImageUpload([0x01, 0x02, 0x03], "image/png", "profile.png");
        var invalid = await houseHelp.PutAsync("/api/househelps/me/profile-image", invalidUpload);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
        var envelope = await invalid.Content.ReadFromJsonAsync<ApiResponse<Dictionary<string, string[]>>>();
        Assert.Contains("content", envelope!.Data!.Keys);
    }

    [Fact]
    public async Task Put_OwnProfileImage_RejectsMultipartBodyAboveHardLimit()
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", 1));
        var createResponse = await admin.PostAsJsonAsync("/api/househelps", new CreateHouseHelpRequest
        {
            UserId = 904,
            FirstName = "Bounded",
            LastName = "Upload",
            Phone = "+256700000012",
            City = "Kampala"
        });
        createResponse.EnsureSuccessStatusCode();

        var houseHelp = _factory.CreateClient();
        houseHelp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("househelp", 904));
        using var upload = CreateImageUpload(PngBytes(), "image/png", "profile.png");
        upload.Add(new ByteArrayContent(new byte[3 * 1024 * 1024]), "extraOne", "extra-one.bin");
        upload.Add(new ByteArrayContent(new byte[3 * 1024 * 1024]), "extraTwo", "extra-two.bin");

        var response = await houseHelp.PutAsync("/api/househelps/me/profile-image", upload);

        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
            response.StatusCode == System.Net.HttpStatusCode.RequestEntityTooLarge);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
        var stored = await db.HouseHelps.AsNoTracking().SingleAsync(item => item.UserId == 904);
        Assert.Null(stored.ProfileImageStorageKey);
    }

    [Fact]
    public async Task PublicProfile_ProjectsSafeImageUrlAndServesOnlyActiveProfileImage()
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", 1));
        var createResponse = await admin.PostAsJsonAsync("/api/househelps", new CreateHouseHelpRequest
        {
            UserId = 905,
            FirstName = "Public",
            LastName = "Profile",
            Phone = "+256700000013",
            City = "Kampala"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();

        var profileUpdate = await admin.PutAsJsonAsync(
            $"/api/househelps/{created!.Data!.Id}/profile",
            new UpdateHouseHelpProfileRequest
            {
                FirstName = "Public",
                LastName = "Profile",
                Phone = "+256700000013",
                City = "Kampala",
                Bio = "Experienced home care professional",
                YearsOfExperience = 5,
                Languages = "English,Luganda",
                VerificationStatus = HouseManagement.Api.Models.HouseHelpVerificationStatus.Verified
            });
        profileUpdate.EnsureSuccessStatusCode();

        using (var upload = CreateImageUpload(PngBytes(), "image/png", "profile.png"))
        {
            (await admin.PutAsync(
                $"/api/househelps/{created.Data.Id}/profile-image",
                upload)).EnsureSuccessStatusCode();
        }

        var anonymous = _factory.CreateClient();
        var detailResponse = await anonymous.GetAsync($"/api/househelps/{created.Data.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detailContent = await detailResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("storageKey", detailContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("profileImageContentType", detailContent, StringComparison.OrdinalIgnoreCase);
        var detail = JsonSerializer.Deserialize<ApiResponse<PublicHouseHelpDto>>(
            detailContent,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("Experienced home care professional", detail!.Data!.Bio);
        Assert.Equal(5, detail.Data.YearsOfExperience);
        Assert.Equal("English,Luganda", detail.Data.Languages);
        Assert.Equal("Verified", detail.Data.VerificationStatus);
        Assert.StartsWith(
            $"/api/househelps/{created.Data.Id}/profile-image?v=",
            detail.Data.ProfileImageUrl,
            StringComparison.Ordinal);

        var imageResponse = await anonymous.GetAsync(detail.Data.ProfileImageUrl);
        Assert.Equal(System.Net.HttpStatusCode.OK, imageResponse.StatusCode);
        Assert.Equal("image/png", imageResponse.Content.Headers.ContentType!.MediaType);
        Assert.True(imageResponse.Headers.CacheControl!.NoStore);
        var imageContent = await imageResponse.Content.ReadAsByteArrayAsync();
        using var decoded = SKBitmap.Decode(imageContent);
        Assert.NotNull(decoded);
        Assert.Equal(1, decoded.Width);
        Assert.Equal(1, decoded.Height);

        (await admin.PutAsync(
            $"/api/househelps/{created.Data.Id}/activate?active=false",
            null)).EnsureSuccessStatusCode();

        var inactiveImageResponse = await anonymous.GetAsync(detail.Data.ProfileImageUrl);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, inactiveImageResponse.StatusCode);
    }

    [Fact]
    public async Task Get_ProfileImage_ReturnsNotFoundWhenStoredFileIsMissing()
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", 1));
        var createResponse = await admin.PostAsJsonAsync("/api/househelps", new CreateHouseHelpRequest
        {
            FirstName = "Missing",
            LastName = "Image",
            Phone = "+256700000014",
            City = "Kampala"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();

        using (var upload = CreateImageUpload(PngBytes(), "image/png", "profile.png"))
        {
            (await admin.PutAsync(
                $"/api/househelps/{created!.Data!.Id}/profile-image",
                upload)).EnsureSuccessStatusCode();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HouseContext>();
            var stored = await db.HouseHelps.AsNoTracking()
                .SingleAsync(item => item.Id == created.Data.Id);
            File.Delete(GetStoredImagePath(stored.ProfileImageStorageKey!));
        }

        var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync(
            $"/api/househelps/{created.Data.Id}/profile-image");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(HouseManagement.Api.Models.HouseHelpVerificationStatus.Unverified, "Unverified")]
    [InlineData(HouseManagement.Api.Models.HouseHelpVerificationStatus.PendingReview, "Unverified")]
    [InlineData(HouseManagement.Api.Models.HouseHelpVerificationStatus.Rejected, "Unverified")]
    [InlineData(HouseManagement.Api.Models.HouseHelpVerificationStatus.Verified, "Verified")]
    public async Task PublicProfile_ProjectsCoarseVerificationStatus(
        HouseManagement.Api.Models.HouseHelpVerificationStatus status,
        string expected)
    {
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("admin", 1));
        var createResponse = await admin.PostAsJsonAsync("/api/househelps", new CreateHouseHelpRequest
        {
            FirstName = $"Status{status}",
            LastName = "Projection",
            Phone = "+256700000015",
            City = "Kampala"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<HouseHelpDto>>();

        var profileUpdate = await admin.PutAsJsonAsync(
            $"/api/househelps/{created!.Data!.Id}/profile",
            new UpdateHouseHelpProfileRequest
            {
                FirstName = $"Status{status}",
                LastName = "Projection",
                Phone = "+256700000015",
                City = "Kampala",
                VerificationStatus = status
            });
        profileUpdate.EnsureSuccessStatusCode();

        var anonymous = _factory.CreateClient();
        var detail = await anonymous.GetFromJsonAsync<ApiResponse<PublicHouseHelpDto>>(
            $"/api/househelps/{created.Data.Id}");

        Assert.Equal(expected, detail!.Data!.VerificationStatus);
    }

    private MultipartFormDataContent CreateImageUpload(
        byte[] content,
        string contentType,
        string fileName)
    {
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent();
        form.Add(fileContent, "File", fileName);
        return form;
    }

    private string GetStoredImagePath(string storageKey)
    {
        return Path.Combine(_profileImageRoot, Path.Combine(storageKey.Split('/')));
    }

    private static byte[] PngBytes()
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var content = image.Encode(SKEncodedImageFormat.Png, 100);
        return content.ToArray();
    }
}