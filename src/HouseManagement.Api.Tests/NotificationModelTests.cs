using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class NotificationModelTests
{
    [Fact]
    public async Task Notification_CanBePersistedAndRetrieved_WithDefaults()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(nameof(Notification_CanBePersistedAndRetrieved_WithDefaults))
            .Options;

        await using var context = new HouseContext(options);

        var notification = new Notification
        {
            UserId = 42,
            Type = "booking.assigned",
            Title = "You have a new assignment",
            Message = "You were assigned to booking BK-1001.",
            RelatedEntityType = "Booking",
            RelatedEntityId = 1001
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var saved = await context.Notifications.SingleAsync();
        Assert.Equal(42, saved.UserId);
        Assert.Equal("booking.assigned", saved.Type);
        Assert.False(saved.IsRead);
        Assert.Null(saved.ReadAt);
        Assert.Equal("Booking", saved.RelatedEntityType);
        Assert.Equal(1001, saved.RelatedEntityId);
    }
}
