using HouseManagement.Api.Data;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class AuditLogServiceTests
{
    private static HouseContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new HouseContext(options);
    }

    [Fact]
    public async Task LogAsync_PersistsEntry_WithProvidedFields()
    {
        await using var context = CreateContext(nameof(LogAsync_PersistsEntry_WithProvidedFields));
        var service = new AuditLogService(context);

        await service.LogAsync("user.role_changed", "User", entityId: 5, userId: 1, details: "manager -> admin");

        var entry = await context.AuditLogs.SingleAsync();
        Assert.Equal("user.role_changed", entry.Action);
        Assert.Equal("User", entry.EntityType);
        Assert.Equal(5, entry.EntityId);
        Assert.Equal(1, entry.UserId);
        Assert.Equal("manager -> admin", entry.Details);
    }

    [Fact]
    public async Task GetListAsync_FiltersByActionEntityTypeAndUserId()
    {
        await using var context = CreateContext(nameof(GetListAsync_FiltersByActionEntityTypeAndUserId));
        var service = new AuditLogService(context);

        await service.LogAsync("user.role_changed", "User", 1, userId: 10);
        await service.LogAsync("user.activated", "User", 2, userId: 10);
        await service.LogAsync("booking.assigned", "Booking", 3, userId: 20);

        var byAction = await service.GetListAsync(action: "user.role_changed");
        Assert.Single(byAction);
        Assert.Equal("user.role_changed", byAction[0].Action);

        var byEntityType = await service.GetListAsync(entityType: "Booking");
        Assert.Single(byEntityType);
        Assert.Equal("Booking", byEntityType[0].EntityType);

        var byUserId = await service.GetListAsync(userId: 10);
        Assert.Equal(2, byUserId.Count);
    }

    [Fact]
    public async Task GetListAsync_OrdersNewestFirst_AndSupportsPagination()
    {
        await using var context = CreateContext(nameof(GetListAsync_OrdersNewestFirst_AndSupportsPagination));
        var service = new AuditLogService(context);

        for (var i = 0; i < 5; i++)
        {
            await service.LogAsync($"action.{i}", "Entity", i);
        }

        var page1 = await service.GetListAsync(page: 1, pageSize: 2);
        Assert.Equal(2, page1.Count);
        Assert.Equal("action.4", page1[0].Action);
        Assert.Equal("action.3", page1[1].Action);

        var page2 = await service.GetListAsync(page: 2, pageSize: 2);
        Assert.Equal("action.2", page2[0].Action);
    }
}
