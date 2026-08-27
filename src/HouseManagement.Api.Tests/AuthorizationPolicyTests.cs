using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using HouseManagement.Api.Common;
using HouseManagement.Api.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseManagement.Api.Tests;

public class AuthorizationPolicyTests
{
    [Theory]
    [InlineData(AuthorizationPolicies.AdminOnly, Roles.Admin, true)]
    [InlineData(AuthorizationPolicies.AdminOnly, Roles.Manager, false)]
    [InlineData(AuthorizationPolicies.ManagerOrAdmin, Roles.Admin, true)]
    [InlineData(AuthorizationPolicies.ManagerOrAdmin, Roles.Manager, true)]
    [InlineData(AuthorizationPolicies.ManagerOrAdmin, Roles.HouseHelp, false)]
    [InlineData(AuthorizationPolicies.HouseHelpOnly, Roles.HouseHelp, true)]
    [InlineData(AuthorizationPolicies.HouseHelpOnly, Roles.Admin, false)]
    public async Task Policies_AllowExpectedRoles(string policyName, string role, bool expected)
    {
        var services = new ServiceCollection();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(Roles.Admin));
            options.AddPolicy(AuthorizationPolicies.ManagerOrAdmin, policy => policy.RequireRole(Roles.Manager, Roles.Admin));
            options.AddPolicy(AuthorizationPolicies.HouseHelpOnly, policy => policy.RequireRole(Roles.HouseHelp));
        });
        services.AddLogging();

        using var provider = services.BuildServiceProvider();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "Test"));

        var result = await auth.AuthorizeAsync(user, null, policyName);

        Assert.Equal(expected, result.Succeeded);
    }
}
