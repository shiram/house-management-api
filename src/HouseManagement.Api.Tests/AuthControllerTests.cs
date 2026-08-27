using System.IdentityModel.Tokens.Jwt;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using HouseManagement.Api.Controllers;
using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;

namespace HouseManagement.Api.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task Register_CreatesUser_WithDefaultRoleHousehelp()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(databaseName: "test_db_register_role")
            .Options;

        await using var context = new HouseContext(options);

        var hasher = new PasswordHasher();
        var tokenMock = new Mock<ITokenService>();
        tokenMock.Setup(t => t.CreateToken(It.IsAny<User>())).Returns("test-token");

        var controller = new AuthController(context, hasher, tokenMock.Object);

        var req = new RegisterRequest
        {
            UserName = "testuser",
            Email = "test@example.com",
            Password = "Password123!"
        };

        var result = await controller.Register(req);

        Assert.IsType<OkObjectResult>(result);
        var ok = result as OkObjectResult;
        Assert.NotNull(ok);

        var authResp = ok!.Value as dynamic; // AuthResponse type from DTOs
        Assert.NotNull(authResp);
        Assert.Equal("househelp", (string)authResp.Role!);

        // verify persisted
        var user = await context.Users.SingleOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.NotNull(user);
        Assert.Equal("househelp", user!.Role);
    }

    [Fact]
    public void CreateToken_EmitsRoleClaimAndStandardClaims()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "01234567890123456789012345678901",
                ["Jwt:Issuer"] = "HouseManagement",
                ["Jwt:Audience"] = "HouseManagement",
                ["Jwt:ExpireMinutes"] = "30"
            })
            .Build();

        var service = new TokenService(config);
        var token = service.CreateToken(new User
        {
            Id = 42,
            UserName = "adminuser",
            Email = "admin@example.com",
            Role = "admin"
        });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "42");
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == "adminuser");
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "admin@example.com");
        Assert.Contains(jwt.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "admin");
    }
}