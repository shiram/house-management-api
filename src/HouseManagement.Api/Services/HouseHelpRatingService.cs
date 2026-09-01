using HouseManagement.Api.Common;
using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class HouseHelpRatingService : IHouseHelpRatingService
{
    private readonly HouseContext _db;
    private readonly INotificationService _notifications;
    private readonly IAuditLogService _auditLogs;

    public HouseHelpRatingService(HouseContext db, INotificationService notifications, IAuditLogService auditLogs)
    {
        _db = db;
        _notifications = notifications;
        _auditLogs = auditLogs;
    }

    public async Task<HouseHelpRatingCreationResult> CreateAsync(int clientUserId, int bookingId, int score, string? comment)
    {
        if (score < 1 || score > 5)
        {
            return new HouseHelpRatingCreationResult(null, "Score must be between 1 and 5.");
        }

        var booking = await _db.Bookings
            .Include(item => item.Client)
            .Include(item => item.AssignedHouseHelp)
            .SingleOrDefaultAsync(item => item.Id == bookingId);
        if (booking == null)
        {
            return new HouseHelpRatingCreationResult(null, "The requested booking was not found.");
        }

        if (booking.Client == null || booking.Client.UserId != clientUserId)
        {
            return new HouseHelpRatingCreationResult(null, "You can only rate your own bookings.");
        }

        if (booking.Status != BookingStatus.Completed || booking.AssignedHouseHelpId == null)
        {
            return new HouseHelpRatingCreationResult(null, "Only completed bookings with an assigned HouseHelp can be rated.");
        }

        var alreadyRated = await _db.HouseHelpRatings.AnyAsync(rating => rating.BookingId == bookingId);
        if (alreadyRated)
        {
            return new HouseHelpRatingCreationResult(null, "This booking has already been rated.");
        }

        var rating = new HouseHelpRating
        {
            BookingId = booking.Id,
            HouseHelpId = booking.AssignedHouseHelpId.Value,
            ClientId = booking.Client.Id,
            Score = score,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.HouseHelpRatings.Add(rating);
        await _db.SaveChangesAsync();

        if (booking.AssignedHouseHelp?.UserId is int houseHelpUserId)
        {
            await _notifications.CreateAsync(
                houseHelpUserId,
                NotificationTypes.HouseHelpRatingSubmitted,
                "New rating received",
                $"You received a {score}-star rating for booking {booking.Reference}.",
                nameof(HouseHelpRating),
                rating.Id);
        }

        await _auditLogs.LogAsync(
            AuditEventTypes.HouseHelpRatingSubmitted,
            nameof(HouseHelpRating),
            entityId: rating.Id,
            userId: clientUserId,
            details: $"Booking {booking.Id} rated {score}/5 for HouseHelp {rating.HouseHelpId}.");

        return new HouseHelpRatingCreationResult(rating, null);
    }

    public async Task<IReadOnlyList<HouseHelpRating>> GetForHouseHelpAsync(int houseHelpId, int? page = null, int? pageSize = null)
    {
        var query = _db.HouseHelpRatings
            .AsNoTracking()
            .Include(rating => rating.Client)
            .Where(rating => rating.HouseHelpId == houseHelpId)
            .OrderByDescending(rating => rating.CreatedAt)
            .ThenByDescending(rating => rating.Id)
            .AsQueryable();

        return await query.ApplyPagination(page, pageSize).ToListAsync();
    }

    public async Task<HouseHelpRatingSummary> GetSummaryAsync(int houseHelpId)
    {
        var scores = await _db.HouseHelpRatings
            .AsNoTracking()
            .Where(rating => rating.HouseHelpId == houseHelpId)
            .Select(rating => rating.Score)
            .ToListAsync();

        if (scores.Count == 0)
        {
            return new HouseHelpRatingSummary(null, 0);
        }

        return new HouseHelpRatingSummary(scores.Average(), scores.Count);
    }
}
