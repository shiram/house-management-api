using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using HouseManagement.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseManagement.Api.Tests;

public class AuthorizationPolicyTests
{
    [Theory]
    [InlineData("RequireAdmin", Roles.Admin, true)]
    [InlineData("RequireAdmin", Roles.Manager, false)]
    [InlineData("RequireManagerOrAdmin", Roles.Admin, true)]
    [InlineData("RequireManagerOrAdmin", Roles.Manager, true)]
    [InlineData("RequireManagerOrAdmin", Roles.HouseHelp, false)]
    [InlineData("RequireHouseHelp", Roles.HouseHelp, true)]
    [InlineData("RequireHouseHelp", Roles.Admin, false)]
    public async Task Policies_AllowExpectedRoles(string policyName, string role, bool expected)
    {
        var services = new ServiceCollection();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdmin", policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy("RequireManagerOrAdmin", policy => policy.RequireRole(Roles.Manager, Roles.Admin));
            options.AddPolicy("RequireHouseHelp", policy => policy.RequireRole(Roles.HouseHelp));
        });
        services.AddLogging();

        using var provider = services.BuildServiceProvider();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "Test"));

        var result = await auth.AuthorizeAsync(user, null, policyName);

        Assert.Equal(expected, result.Succeeded);
    }
}
