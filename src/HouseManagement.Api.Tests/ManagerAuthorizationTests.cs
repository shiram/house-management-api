using System.Reflection;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace HouseManagement.Api.Tests;

public class ManagerAuthorizationTests
{
    public static TheoryData<Type, string> ManagementEndpoints => new()
    {
        { typeof(HouseHelpsController), nameof(HouseHelpsController.Create) },
        { typeof(HouseHelpsController), nameof(HouseHelpsController.Update) },
        { typeof(HouseHelpsController), nameof(HouseHelpsController.SetActive) },
        { typeof(AvailabilityController), nameof(AvailabilityController.ReplaceWeekly) },
        { typeof(BookingsController), nameof(BookingsController.Get) },
        { typeof(BookingsController), nameof(BookingsController.Assign) },
        { typeof(BookingsController), nameof(BookingsController.Cancel) },
        { typeof(BookingsController), nameof(BookingsController.Reject) },
        { typeof(BookingsController), nameof(BookingsController.Confirm) },
        { typeof(BookingsController), nameof(BookingsController.Complete) },
        { typeof(BookingsController), nameof(BookingsController.GetList) }
    };

    [Theory]
    [MemberData(nameof(ManagementEndpoints))]
    public void ManagementEndpoints_RequireManagerOrAdmin(Type controllerType, string methodName)
    {
        var method = controllerType.GetMethod(methodName);
        Assert.NotNull(method);

        var attribute = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(AuthorizationPolicies.ManagerOrAdmin, attribute.Policy);
    }
}
