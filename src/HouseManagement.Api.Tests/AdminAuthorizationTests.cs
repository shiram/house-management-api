using System.Reflection;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace HouseManagement.Api.Tests;

public class AdminAuthorizationTests
{
    public static TheoryData<Type, string> AdminOnlyEndpoints => new()
    {
        { typeof(AdministrationController), nameof(AdministrationController.GetUsers) },
        { typeof(AdministrationController), nameof(AdministrationController.GetUser) },
        { typeof(AdministrationController), nameof(AdministrationController.UpdateRole) },
        { typeof(AdministrationController), nameof(AdministrationController.SetActive) },
        { typeof(SystemSettingsController), nameof(SystemSettingsController.GetAll) },
        { typeof(SystemSettingsController), nameof(SystemSettingsController.Get) },
        { typeof(SystemSettingsController), nameof(SystemSettingsController.Upsert) },
        { typeof(AuditLogsController), nameof(AuditLogsController.GetList) }
    };

    [Theory]
    [MemberData(nameof(AdminOnlyEndpoints))]
    public void AdminOnlyEndpoints_RequireAdminOnlyPolicy(Type controllerType, string methodName)
    {
        var method = controllerType.GetMethod(methodName);
        Assert.NotNull(method);

        // These endpoints rely on a class-level [Authorize(Policy = AdminOnly)] rather than
        // repeating the attribute on every action, so fall back to the declaring type when
        // no method-level attribute is present.
        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>()
            ?? controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(AuthorizationPolicies.AdminOnly, attribute!.Policy);
    }

    public static TheoryData<Type, string> ManagerOrAdminScopedAdminEndpoints => new()
    {
        { typeof(AdminServicesController), nameof(AdminServicesController.GetAll) },
        { typeof(AdminServicesController), nameof(AdminServicesController.Get) },
        { typeof(AdminHouseHelpsController), nameof(AdminHouseHelpsController.GetAll) },
        { typeof(AdminHouseHelpsController), nameof(AdminHouseHelpsController.Get) }
    };

    [Theory]
    [MemberData(nameof(ManagerOrAdminScopedAdminEndpoints))]
    public void ManagerOrAdminScopedAdminEndpoints_AreNotAccidentallyAnonymous(Type controllerType, string methodName)
    {
        var method = controllerType.GetMethod(methodName);
        Assert.NotNull(method);

        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>()
            ?? controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(AuthorizationPolicies.ManagerOrAdmin, attribute!.Policy);
    }
}
