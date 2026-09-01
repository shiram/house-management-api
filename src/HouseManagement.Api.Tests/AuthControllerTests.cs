using System.IdentityModel.Tokens.Jwt;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HouseManagement.Api.Common;
using HouseManagement.Api.Common.Api;
using Microsoft.AspNetCore.Http;
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

        var controller = new AuthController(context, hasher, tokenMock.Object, new AuditLogService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "register-request-id"
                }
            }
        };

        var req = new RegisterRequest
        {
            UserName = "testuser",
            Email = "test@example.com",
            Password = "Password123!"
        };

        var result = await controller.Register(req);

        Assert.IsType<OkObjectResult>(result);
        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<AuthResponse>>(ok.Value);
        Assert.Equal(200, envelope.StatusCode);
        Assert.Equal("User registered successfully", envelope.Message);
        Assert.Equal("register-request-id", envelope.RequestId);
        Assert.NotNull(envelope.Data);
        Assert.Equal("househelp", envelope.Data!.Role);

        // verify persisted
        var user = await context.Users.SingleOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.NotNull(user);
        Assert.Equal("househelp", user!.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(databaseName: "test_db_register_duplicate")
            .Options;

        await using var context = new HouseContext(options);
        var hasher = new PasswordHasher();

        // Seed existing user
        var existing = new User
        {
            UserName = "existing",
            Email = "dup@example.com",
            PasswordHash = hasher.Hash("Password123!"),
            Role = "househelp"
        };
        context.Users.Add(existing);
        await context.SaveChangesAsync();

        var tokenMock = new Mock<ITokenService>();
        var controller = new AuthController(context, hasher, tokenMock.Object, new AuditLogService(context));

        var req = new RegisterRequest
        {
            UserName = "newuser",
            Email = "dup@example.com",
            Password = "Password123!"
        };

        var result = await controller.Register(req);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Login_ReturnsEnvelope_WithAuthResponse()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(databaseName: "test_db_login_envelope")
            .Options;

        await using var context = new HouseContext(options);
        var hasher = new PasswordHasher();
        var password = "Password123!";

        context.Users.Add(new User
        {
            UserName = "loginuser",
            Email = "login@example.com",
            PasswordHash = hasher.Hash(password),
            Role = "manager"
        });
        await context.SaveChangesAsync();

        var tokenMock = new Mock<ITokenService>();
        tokenMock.Setup(t => t.CreateToken(It.IsAny<User>())).Returns("test-token");

        var controller = new AuthController(context, hasher, tokenMock.Object, new AuditLogService(context));

        var result = await controller.Login(new LoginRequest
        {
            Email = "login@example.com",
            Password = password
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<AuthResponse>>(ok.Value);
        Assert.Equal(200, envelope.StatusCode);
        Assert.Equal("Login successful", envelope.Message);
        Assert.NotNull(envelope.Data);
        Assert.Equal("test-token", envelope.Data!.Token);
        Assert.Equal("manager", envelope.Data.Role);

        var audit = await context.AuditLogs.SingleAsync();
        Assert.Equal(AuditEventTypes.AuthenticationLoginSucceeded, audit.Action);
        Assert.Equal("User", audit.EntityType);
        Assert.Equal((await context.Users.SingleAsync(user => user.Email == "login@example.com")).Id, audit.EntityId);
        Assert.Equal(audit.EntityId, audit.UserId);
        Assert.Null(audit.Details);
    }

    [Fact]
    public async Task Login_UpdatesLastLogin_OnSuccess()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(databaseName: "test_db_login_lastlogin")
            .Options;

        await using var context = new HouseContext(options);
        var hasher = new PasswordHasher();

        var user = new User
        {
            UserName = "loginuser",
            Email = "login@example.com",
            PasswordHash = hasher.Hash("Password123!"),
            Role = "househelp"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tokenMock = new Mock<ITokenService>();
        tokenMock.Setup(t => t.CreateToken(It.IsAny<User>())).Returns("token123");

        var controller = new AuthController(context, hasher, tokenMock.Object, new AuditLogService(context));

        var req = new LoginRequest { Email = "login@example.com", Password = "Password123!" };
        var before = System.DateTime.UtcNow;

        var result = await controller.Login(req);
        Assert.IsType<OkObjectResult>(result);

        var dbUser = await context.Users.SingleAsync(u => u.Email == "login@example.com");
        Assert.NotNull(dbUser.LastLogin);
        Assert.True(dbUser.LastLogin >= before);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(databaseName: "test_db_login_invalid")
            .Options;

        await using var context = new HouseContext(options);
        var hasher = new PasswordHasher();

        var user = new User
        {
            UserName = "loginuser2",
            Email = "login2@example.com",
            PasswordHash = hasher.Hash("Password123!"),
            Role = "househelp"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tokenMock = new Mock<ITokenService>();
        var controller = new AuthController(context, hasher, tokenMock.Object, new AuditLogService(context));

        var req = new LoginRequest { Email = "login2@example.com", Password = "WrongPassword" };
        var result = await controller.Login(req);
        Assert.IsType<UnauthorizedObjectResult>(result);

        var missingResult = await controller.Login(new LoginRequest { Email = "noone@example.com", Password = "whatever" });
        Assert.IsType<UnauthorizedObjectResult>(missingResult);

        var failedAudits = await context.AuditLogs.ToListAsync();
        Assert.Equal(2, failedAudits.Count);
        Assert.All(failedAudits, audit =>
        {
            Assert.Equal(AuditEventTypes.AuthenticationLoginFailed, audit.Action);
            Assert.Equal("User", audit.EntityType);
            Assert.Null(audit.EntityId);
            Assert.Null(audit.UserId);
            Assert.Null(audit.Details);
        });
    }

    [Fact]
    public async Task Login_DeactivatedAccount_ReturnsUnauthorized()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(databaseName: "test_db_login_deactivated")
            .Options;

        await using var context = new HouseContext(options);
        var hasher = new PasswordHasher();

        context.Users.Add(new User
        {
            UserName = "deactivateduser",
            Email = "deactivated@example.com",
            PasswordHash = hasher.Hash("Password123!"),
            Role = "manager",
            IsActive = false
        });
        await context.SaveChangesAsync();

        var tokenMock = new Mock<ITokenService>();
        var controller = new AuthController(context, hasher, tokenMock.Object, new AuditLogService(context));

        var result = await controller.Login(new LoginRequest { Email = "deactivated@example.com", Password = "Password123!" });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Register_ReturnsValidationEnvelope_WhenModelStateInvalid()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(databaseName: "test_db_register_validation")
            .Options;

        await using var context = new HouseContext(options);
        var hasher = new PasswordHasher();
        var tokenMock = new Mock<ITokenService>();
        var controller = new AuthController(context, hasher, tokenMock.Object, new AuditLogService(context));
        controller.ModelState.AddModelError("Email", "The Email field is required.");

        var result = await controller.Register(new RegisterRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<Dictionary<string, string[]>>>(badRequest.Value);
        Assert.Equal(400, envelope.StatusCode);
        Assert.Equal("Validation failed", envelope.Message);
        Assert.Contains("Email", envelope.Data.Keys);
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
