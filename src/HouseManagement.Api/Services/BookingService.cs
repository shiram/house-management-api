using System.Collections.Concurrent;
using System.Data;
using HouseManagement.Api.Common;
using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class BookingService : IBookingService
{
    private readonly HouseContext _db;
    private readonly INotificationService _notifications;

    // In-process guard to serialize concurrent assignment attempts for the same househelp.
    // This complements the database-level serializable transaction: the in-memory EF provider
    // used in unit/integration tests does not enforce real transaction isolation, and even with
    // a relational provider this avoids unnecessary contention/retries for the common case where
    // two requests target the same househelp at (almost) the same time.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> AssignmentLocks = new();

    public BookingService(HouseContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<BookingCreationResult> CreateAnonymousAsync(CreateAnonymousBookingRequest request)
    {
        if (request.ScheduledStart >= request.ScheduledEnd || request.ScheduledStart <= DateTimeOffset.UtcNow)
        {
            return new BookingCreationResult(null, "The requested service time must be a future range.");
        }

        var service = await _db.Services
            .SingleOrDefaultAsync(item => item.Id == request.ServiceId && item.IsActive);
        if (service == null)
        {
            return new BookingCreationResult(null, "The requested service is not available.");
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        if (_db.Database.IsRelational())
        {
            transaction = await _db.Database.BeginTransactionAsync();
        }
        var client = new Client
        {
            Name = request.ContactName.Trim(),
            Phone = request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var address = new ServiceAddress
        {
            Line1 = request.Address.Line1.Trim(),
            Line2 = string.IsNullOrWhiteSpace(request.Address.Line2) ? null : request.Address.Line2.Trim(),
            City = request.Address.City.Trim(),
            Region = string.IsNullOrWhiteSpace(request.Address.Region) ? null : request.Address.Region.Trim(),
            PostalCode = string.IsNullOrWhiteSpace(request.Address.PostalCode) ? null : request.Address.PostalCode.Trim(),
            Country = request.Address.Country.Trim()
        };
        var booking = new Booking
        {
            Reference = await GenerateReferenceAsync(),
            Service = service,
            Client = client,
            ServiceAddress = address,
            ScheduledStart = request.ScheduledStart,
            ScheduledEnd = request.ScheduledEnd,
            Status = BookingStatus.Requested,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        var managerIds = await _db.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.Role == Roles.Manager)
            .Select(user => user.Id)
            .ToListAsync();

        foreach (var managerId in managerIds)
        {
            await _notifications.CreateAsync(
                managerId,
                NotificationTypes.BookingCreated,
                "New booking request",
                $"A new booking request ({booking.Reference}) has been submitted.",
                "Booking",
                booking.Id);
        }

        if (transaction != null)
        {
            await transaction.CommitAsync();
            await transaction.DisposeAsync();
        }
        return new BookingCreationResult(booking, null);
    }

    public async Task<BookingAssignmentResult> AssignHouseHelpAsync(int bookingId, int houseHelpId, int? assignedByUserId = null)
    {
        var assignmentLock = AssignmentLocks.GetOrAdd(houseHelpId, _ => new SemaphoreSlim(1, 1));
        await assignmentLock.WaitAsync();
        try
        {
            return await AssignHouseHelpCoreAsync(bookingId, houseHelpId, assignedByUserId);
        }
        finally
        {
            assignmentLock.Release();
        }
    }

    private async Task<BookingAssignmentResult> AssignHouseHelpCoreAsync(int bookingId, int houseHelpId, int? assignedByUserId)
    {
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        if (_db.Database.IsRelational())
        {
            transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        }

        try
        {
            var booking = await _db.Bookings
                .Include(item => item.Service)
                .SingleOrDefaultAsync(item => item.Id == bookingId);

            if (booking == null)
            {
                return new BookingAssignmentResult(null, "The requested booking was not found.");
            }

            if (booking.Status != BookingStatus.Confirmed)
            {
                return new BookingAssignmentResult(null, "The booking must be confirmed before assignment.");
            }

            var houseHelp = await _db.HouseHelps
                .Include(item => item.Skills)
                .Include(item => item.Availabilities)
                .SingleOrDefaultAsync(item => item.Id == houseHelpId);

            if (houseHelp == null)
            {
                return new BookingAssignmentResult(null, "The requested househelp was not found.");
            }

            if (!houseHelp.IsActive)
            {
                return new BookingAssignmentResult(null, "The requested househelp is not active.");
            }

            var supportsService = houseHelp.Skills.Any(skill =>
                string.Equals(skill.ServiceName, booking.Service?.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skill.ServiceName, booking.Service?.Code, StringComparison.OrdinalIgnoreCase));

            if (!supportsService)
            {
                return new BookingAssignmentResult(null, "The selected househelp does not support the requested service.");
            }

            if (!IsAvailableForBooking(houseHelp, booking))
            {
                return new BookingAssignmentResult(null, "The selected househelp is not available during the requested service window.");
            }

            if (await HasOverlappingAssignedBookingAsync(bookingId, houseHelpId, booking.ScheduledStart, booking.ScheduledEnd))
            {
                return new BookingAssignmentResult(null, "The selected househelp is already assigned for a conflicting booking.");
            }

            booking.AssignedHouseHelpId = houseHelpId;
            booking.AssignedByUserId = assignedByUserId;
            booking.AssignedAt = DateTimeOffset.UtcNow;
            booking.Status = BookingStatus.Assigned;
            booking.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return new BookingAssignmentResult(booking, null);
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<Booking?> GetByIdAsync(int id)
    {
        return await _db.Bookings
            .AsNoTracking()
            .Include(booking => booking.Service)
            .Include(booking => booking.ServiceAddress)
            .SingleOrDefaultAsync(booking => booking.Id == id);
    }

    public async Task<Booking?> GetByReferenceAsync(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var normalized = reference.Trim();
        return await _db.Bookings
            .AsNoTracking()
            .Include(booking => booking.Service)
            .Include(booking => booking.ServiceAddress)
            .SingleOrDefaultAsync(booking => booking.Reference == normalized);
    }

    public async Task<int?> GetHouseHelpIdForUserAsync(int userId)
    {
        return await _db.HouseHelps
            .Where(houseHelp => houseHelp.UserId == userId)
            .Select(houseHelp => (int?)houseHelp.Id)
            .SingleOrDefaultAsync();
    }

    public async Task<int?> GetClientIdForUserAsync(int userId)
    {
        return await _db.Clients
            .Where(client => client.UserId == userId)
            .Select(client => (int?)client.Id)
            .SingleOrDefaultAsync();
    }

    public async Task<IReadOnlyList<Booking>> GetListAsync(BookingStatus? status = null, int? page = null, int? pageSize = null, int? houseHelpId = null, int? clientId = null)
    {
        var query = _db.Bookings.AsNoTracking().AsQueryable();

        if (houseHelpId.HasValue)
        {
            query = query.Where(booking => booking.AssignedHouseHelpId == houseHelpId.Value);
        }

        if (clientId.HasValue)
        {
            query = query.Where(booking => booking.ClientId == clientId.Value);
        }

        return await ApplyListQuery(query, status, page, pageSize).ToListAsync();
    }

    public async Task<IReadOnlyList<Booking>> GetListForHouseHelpAsync(int houseHelpId, BookingStatus? status = null, int? page = null, int? pageSize = null)
    {
        return await ApplyListQuery(
            _db.Bookings
                .AsNoTracking()
                .Where(booking => booking.AssignedHouseHelpId == houseHelpId),
            status,
            page,
            pageSize).ToListAsync();
    }

    public async Task<IReadOnlyList<Booking>> GetListForClientAsync(int clientId, BookingStatus? status = null, int? page = null, int? pageSize = null)
    {
        return await ApplyListQuery(
            _db.Bookings
                .AsNoTracking()
                .Where(booking => booking.ClientId == clientId),
            status,
            page,
            pageSize).ToListAsync();
    }

    private IQueryable<Booking> ApplyListQuery(IQueryable<Booking> query, BookingStatus? status, int? page, int? pageSize)
    {
        query = query
            .Include(booking => booking.Service)
            .Include(booking => booking.ServiceAddress)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(booking => booking.Status == status.Value);
        }

        query = query
            .OrderByDescending(booking => booking.CreatedAt)
            .ThenByDescending(booking => booking.Id);

        if (page.HasValue && pageSize.HasValue && page.Value > 0 && pageSize.Value > 0)
        {
            var boundedPageSize = Math.Min(pageSize.Value, 100);
            query = query
                .Skip((page.Value - 1) * boundedPageSize)
                .Take(boundedPageSize);
        }

        return query;
    }

    private static bool IsAvailableForBooking(HouseHelp houseHelp, Booking booking)
    {
        if (booking.ScheduledStart.Date != booking.ScheduledEnd.Date)
        {
            return false;
        }

        var day = booking.ScheduledStart.DayOfWeek;
        var start = TimeOnly.FromTimeSpan(booking.ScheduledStart.TimeOfDay);
        var end = TimeOnly.FromTimeSpan(booking.ScheduledEnd.TimeOfDay);

        return houseHelp.Availabilities
            .Where(availability => availability.IsActive && availability.DayOfWeek == day)
            .Any(availability => availability.StartTime <= start && availability.EndTime >= end);
    }

    private async Task<bool> HasOverlappingAssignedBookingAsync(int bookingId, int houseHelpId, DateTimeOffset scheduledStart, DateTimeOffset scheduledEnd)
    {
        return await _db.Bookings.AnyAsync(booking =>
            booking.AssignedHouseHelpId == houseHelpId &&
            booking.Id != bookingId &&
            booking.Status != BookingStatus.Cancelled &&
            booking.Status != BookingStatus.Rejected &&
            booking.Status != BookingStatus.Completed &&
            booking.ScheduledStart < scheduledEnd &&
            scheduledStart < booking.ScheduledEnd);
    }

    private async Task<string> GenerateReferenceAsync()
    {
        string reference;
        do
        {
            reference = $"BK-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
        }
        while (await _db.Bookings.AnyAsync(booking => booking.Reference == reference));

        return reference;
    }
}
