using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface INotificationService
{
    // Creates and persists a notification for a single recipient. Intended to be called
    // by other feature services (bookings, assignment, etc.) rather than exposed directly
    // to clients — there is no public "create notification" HTTP endpoint.
    Task<Notification> CreateAsync(
        int userId,
        string type,
        string title,
        string message,
        string? relatedEntityType = null,
        int? relatedEntityId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> GetForUserAsync(
        int userId,
        bool? unreadOnly = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default);

    Task<Notification?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default);

    Task<Notification?> MarkAsReadAsync(int id, int userId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);
}
