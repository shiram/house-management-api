using System.Reflection;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace HouseManagement.Api.Tests;

public class ServiceAuthorizationTests
{
    [Fact]
    public void PublicReadEndpoints_AreAnonymous()
    {
        Assert.Null(GetMethod(nameof(ServicesController.GetActive)).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(GetMethod(nameof(ServicesController.Get)).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Theory]
    [InlineData(nameof(ServicesController.Create))]
    [InlineData(nameof(ServicesController.Update))]
    [InlineData(nameof(ServicesController.SetActive))]
    public void ManagementEndpoints_RequireManagerOrAdmin(string methodName)
    {
        var authorize = GetMethod(methodName).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AuthorizationPolicies.ManagerOrAdmin, authorize.Policy);
    }

    private static MethodInfo GetMethod(string name)
    {
        return typeof(ServicesController).GetMethod(name)!;
    }
}
