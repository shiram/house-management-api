using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class NotificationService : INotificationService
{
    private readonly HouseContext _db;

    public NotificationService(HouseContext db)
    {
        _db = db;
    }

    public async Task<Notification> CreateAsync(
        int userId,
        string type,
        string title,
        string message,
        string? relatedEntityType = null,
        int? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type.Trim(),
            Title = title.Trim(),
            Message = message.Trim(),
            RelatedEntityType = string.IsNullOrWhiteSpace(relatedEntityType) ? null : relatedEntityType.Trim(),
            RelatedEntityId = relatedEntityId
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);
        return notification;
    }

    public async Task<IReadOnlyList<Notification>> GetForUserAsync(
        int userId,
        bool? unreadOnly = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);

        if (unreadOnly == true)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        query = query
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id);

        if (page.HasValue && pageSize.HasValue && page.Value > 0 && pageSize.Value > 0)
        {
            var boundedPageSize = Math.Min(pageSize.Value, 100);
            query = query
                .Skip((page.Value - 1) * boundedPageSize)
                .Take(boundedPageSize);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<Notification?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        return await _db.Notifications
            .AsNoTracking()
            .SingleOrDefaultAsync(notification => notification.Id == id && notification.UserId == userId, cancellationToken);
    }
}
