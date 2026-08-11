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
}