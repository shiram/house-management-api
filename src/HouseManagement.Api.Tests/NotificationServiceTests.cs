using HouseManagement.Api.Common;
using HouseManagement.Api.Data;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class NotificationServiceTests
{
    private static HouseContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new HouseContext(options);
    }

    [Fact]
    public async Task CreateAsync_PersistsNotification_WithDefaultsUnread()
    {
        await using var context = CreateContext(nameof(CreateAsync_PersistsNotification_WithDefaultsUnread));
        var service = new NotificationService(context);

        var created = await service.CreateAsync(
            userId: 1,
            type: NotificationTypes.BookingAssigned,
            title: "You have a new assignment",
            message: "You were assigned to booking BK-1.",
            relatedEntityType: "Booking",
            relatedEntityId: 1);

        Assert.False(created.IsRead);
        Assert.Null(created.ReadAt);

        var saved = await context.Notifications.SingleAsync();
        Assert.Equal(1, saved.UserId);
        Assert.Equal(NotificationTypes.BookingAssigned, saved.Type);
    }

    [Fact]
    public async Task GetForUserAsync_ReturnsOnlyOwnNotifications_NewestFirst()
    {
        await using var context = CreateContext(nameof(GetForUserAsync_ReturnsOnlyOwnNotifications_NewestFirst));
        var service = new NotificationService(context);

        await service.CreateAsync(1, NotificationTypes.BookingCreated, "T1", "M1");
        await service.CreateAsync(1, NotificationTypes.BookingConfirmed, "T2", "M2");
        await service.CreateAsync(2, NotificationTypes.BookingAssigned, "Other", "Other message");

        var results = await service.GetForUserAsync(1);

        Assert.Equal(2, results.Count);
        Assert.Equal("T2", results[0].Title);
        Assert.Equal("T1", results[1].Title);
    }

    [Fact]
    public async Task GetForUserAsync_UnreadOnly_FiltersReadNotifications()
    {
        await using var context = CreateContext(nameof(GetForUserAsync_UnreadOnly_FiltersReadNotifications));
        var service = new NotificationService(context);

        var readOne = await service.CreateAsync(1, NotificationTypes.BookingCreated, "Read", "Message");
        await service.CreateAsync(1, NotificationTypes.BookingConfirmed, "Unread", "Message");

        readOne.IsRead = true;
        readOne.ReadAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        var unread = await service.GetForUserAsync(1, unreadOnly: true);

        Assert.Single(unread);
        Assert.Equal("Unread", unread[0].Title);
    }

    [Fact]
    public async Task GetByIdForUserAsync_ReturnsNull_WhenOwnedByAnotherUser()
    {
        await using var context = CreateContext(nameof(GetByIdForUserAsync_ReturnsNull_WhenOwnedByAnotherUser));
        var service = new NotificationService(context);

        var created = await service.CreateAsync(1, NotificationTypes.BookingCreated, "Title", "Message");

        var asOwner = await service.GetByIdForUserAsync(created.Id, 1);
        var asOther = await service.GetByIdForUserAsync(created.Id, 2);

        Assert.NotNull(asOwner);
        Assert.Null(asOther);
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesOnlyOwnedUnreadNotification()
    {
        await using var context = CreateContext(nameof(MarkAsReadAsync_UpdatesOnlyOwnedUnreadNotification));
        var service = new NotificationService(context);
        var own = await service.CreateAsync(1, NotificationTypes.BookingCreated, "Own", "Message");
        var other = await service.CreateAsync(2, NotificationTypes.BookingCreated, "Other", "Message");

        var marked = await service.MarkAsReadAsync(own.Id, 1);
        var inaccessible = await service.MarkAsReadAsync(other.Id, 1);

        Assert.NotNull(marked);
        Assert.True(marked!.IsRead);
        Assert.NotNull(marked.ReadAt);
        Assert.Null(inaccessible);
        Assert.False((await context.Notifications.SingleAsync(notification => notification.Id == other.Id)).IsRead);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsOnlyUnreadNotificationsForUser()
    {
        await using var context = CreateContext(nameof(GetUnreadCountAsync_ReturnsOnlyUnreadNotificationsForUser));
        var service = new NotificationService(context);
        var read = await service.CreateAsync(1, NotificationTypes.BookingCreated, "Read", "Message");
        await service.CreateAsync(1, NotificationTypes.BookingConfirmed, "Unread", "Message");
        await service.CreateAsync(2, NotificationTypes.BookingAssigned, "Other", "Message");

        await service.MarkAsReadAsync(read.Id, 1);

        Assert.Equal(1, await service.GetUnreadCountAsync(1));
    }
}
