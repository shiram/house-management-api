using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using HouseManagement.Api.Data;
using HouseManagement.Api.Controllers;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Services;
using HouseManagement.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;

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
        var controller = new AuthController(context, hasher, tokenMock.Object);

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

        var controller = new AuthController(context, hasher, tokenMock.Object);

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
        var controller = new AuthController(context, hasher, tokenMock.Object);

        var req = new LoginRequest { Email = "login2@example.com", Password = "WrongPassword" };
        var result = await controller.Login(req);
        Assert.IsType<UnauthorizedObjectResult>(result);

        var missingResult = await controller.Login(new LoginRequest { Email = "noone@example.com", Password = "whatever" });
        Assert.IsType<UnauthorizedObjectResult>(missingResult);
    }
}