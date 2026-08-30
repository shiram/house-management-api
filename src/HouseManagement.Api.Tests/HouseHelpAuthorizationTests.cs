using System.Reflection;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace HouseManagement.Api.Tests;

public class HouseHelpAuthorizationTests
{
    public static TheoryData<Type, string> HouseHelpEndpoints => new()
    {
        { typeof(AvailabilityController), nameof(AvailabilityController.ReplaceOwnWeekly) },
        { typeof(BookingsController), nameof(BookingsController.GetAssignedForCurrentHouseHelp) }
    };

    [Theory]
    [MemberData(nameof(HouseHelpEndpoints))]
    public void HouseHelpSpecificEndpoints_RequireHouseHelpOnly(Type controllerType, string methodName)
    {
        var method = controllerType.GetMethod(methodName);
        Assert.NotNull(method);

        var attribute = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(AuthorizationPolicies.HouseHelpOnly, attribute.Policy);
    }
}
