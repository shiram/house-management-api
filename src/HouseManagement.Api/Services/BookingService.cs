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
    private readonly IAuditLogService _auditLogs;

    // In-process guard to serialize concurrent assignment attempts for the same househelp.
    // This complements the database-level serializable transaction: the in-memory EF provider
    // used in unit/integration tests does not enforce real transaction isolation, and even with
    // a relational provider this avoids unnecessary contention/retries for the common case where
    // two requests target the same househelp at (almost) the same time.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> AssignmentLocks = new();

    public BookingService(
        HouseContext db,
        INotificationService notifications,
        IAuditLogService auditLogs)
    {
        _db = db;
        _notifications = notifications;
        _auditLogs = auditLogs;
    }

    public sealed record BookingPricingResult(
        List<BookingPriceLine> PriceLines,
        decimal Subtotal,
        string? Error);

    private sealed record BookingDiscountResult(
        Promotion? Promotion,
        decimal DiscountAmount,
        BookingPriceLine? PriceLine,
        string? Error);

    public async Task<BookingCreationResult> CreateAnonymousAsync(CreateAnonymousBookingRequest request)
    {
        if (request.ScheduledStart >= request.ScheduledEnd || request.ScheduledStart <= DateTimeOffset.UtcNow)
        {
            return new BookingCreationResult(null, "The requested service time must be a future range.");
        }

        var service = await _db.Services
            .Include(item => item.PriceRules)
            .Include(item => item.TimePricingPolicy)
            .SingleOrDefaultAsync(item => item.Id == request.ServiceId && item.IsActive);
        if (service == null)
        {
            return new BookingCreationResult(null, "The requested service is not available.");
        }

        var pricing = CreatePriceSnapshot(service, request.PricingItems);
        if (pricing.Error != null)
        {
            return new BookingCreationResult(null, pricing.Error);
        }

        var discount = await CreateDiscountSnapshotAsync(
            request.PromotionCode,
            service.Id,
            pricing.Subtotal,
            request.ScheduledStart);
        if (discount.Error != null)
        {
            return new BookingCreationResult(null, discount.Error);
        }

        if (discount.PriceLine != null)
        {
            pricing.PriceLines.Add(discount.PriceLine);
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
            AppliedPromotionCode = discount.Promotion?.Code,
            AppliedPromotionName = discount.Promotion?.Name,
            DiscountAmount = discount.DiscountAmount,
            TotalPrice = pricing.Subtotal - discount.DiscountAmount,
            PriceLines = pricing.PriceLines,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (discount.Promotion != null)
        {
            discount.Promotion.TimesUsed += 1;
            discount.Promotion.UpdatedAt = DateTimeOffset.UtcNow;
        }

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        await NotifyManagersOfNewBookingAsync(booking, isRepeat: false);

        if (transaction != null)
        {
            await transaction.CommitAsync();
            await transaction.DisposeAsync();
        }
        return new BookingCreationResult(booking, null);
    }

    public async Task<BookingCreationResult> RepeatAsync(int clientUserId, int bookingId, RepeatBookingRequest request)
    {
        if (request.ScheduledStart >= request.ScheduledEnd || request.ScheduledStart <= DateTimeOffset.UtcNow)
        {
            return new BookingCreationResult(null, "The requested service time must be a future range.");
        }

        var sourceBooking = await _db.Bookings
            .Include(booking => booking.Client)
            .Include(booking => booking.Service)
                .ThenInclude(service => service.PriceRules)
            .Include(booking => booking.Service)
                .ThenInclude(service => service.TimePricingPolicy)
            .Include(booking => booking.ServiceAddress)
            .SingleOrDefaultAsync(booking => booking.Id == bookingId);

        if (sourceBooking == null ||
            sourceBooking.Client?.UserId != clientUserId ||
            sourceBooking.Status != BookingStatus.Completed)
        {
            return new BookingCreationResult(null, "The requested completed booking was not found.");
        }

        if (!sourceBooking.Service.IsActive)
        {
            return new BookingCreationResult(null, "The requested service is not available.");
        }

        var pricing = CreatePriceSnapshot(sourceBooking.Service, request.PricingItems);
        if (pricing.Error != null)
        {
            return new BookingCreationResult(null, pricing.Error);
        }

        var discount = await CreateDiscountSnapshotAsync(
            request.PromotionCode,
            sourceBooking.ServiceId,
            pricing.Subtotal,
            request.ScheduledStart);
        if (discount.Error != null)
        {
            return new BookingCreationResult(null, discount.Error);
        }

        if (discount.PriceLine != null)
        {
            pricing.PriceLines.Add(discount.PriceLine);
        }

        var sourceAddress = sourceBooking.ServiceAddress;
        var booking = new Booking
        {
            Reference = await GenerateReferenceAsync(),
            ServiceId = sourceBooking.ServiceId,
            ClientId = sourceBooking.ClientId,
            ServiceAddress = new ServiceAddress
            {
                Line1 = sourceAddress.Line1,
                Line2 = sourceAddress.Line2,
                City = sourceAddress.City,
                Region = sourceAddress.Region,
                PostalCode = sourceAddress.PostalCode,
                Country = sourceAddress.Country
            },
            ScheduledStart = request.ScheduledStart,
            ScheduledEnd = request.ScheduledEnd,
            Status = BookingStatus.Requested,
            Notes = sourceBooking.Notes,
            AppliedPromotionCode = discount.Promotion?.Code,
            AppliedPromotionName = discount.Promotion?.Name,
            DiscountAmount = discount.DiscountAmount,
            TotalPrice = pricing.Subtotal - discount.DiscountAmount,
            PriceLines = pricing.PriceLines,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (discount.Promotion != null)
        {
            discount.Promotion.TimesUsed += 1;
            discount.Promotion.UpdatedAt = DateTimeOffset.UtcNow;
        }

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        await NotifyManagersOfNewBookingAsync(booking, isRepeat: true);

        return new BookingCreationResult(booking, null);
    }

    private async Task NotifyManagersOfNewBookingAsync(Booking booking, bool isRepeat)
    {
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
                isRepeat
                    ? $"A repeat booking request ({booking.Reference}) has been submitted."
                    : $"A new booking request ({booking.Reference}) has been submitted.",
                "Booking",
                booking.Id);
        }
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
                .Include(item => item.Client)
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

            if (houseHelp.UserId is int houseHelpUserId)
            {
                await _notifications.CreateAsync(
                    houseHelpUserId,
                    NotificationTypes.BookingAssigned,
                    "New booking assignment",
                    $"You have been assigned to booking ({booking.Reference}).",
                    "Booking",
                    booking.Id);
            }

            if (booking.Client?.UserId is int clientUserId)
            {
                await _notifications.CreateAsync(
                    clientUserId,
                    NotificationTypes.BookingStatusChanged,
                    "Booking status updated",
                    $"Your booking ({booking.Reference}) status changed to {BookingStatus.Assigned}.",
                    "Booking",
                    booking.Id);
            }

            await _auditLogs.LogAsync(
                AuditEventTypes.BookingAssigned,
                nameof(Booking),
                entityId: booking.Id,
                userId: assignedByUserId,
                details: $"HouseHelpId: {houseHelpId}");

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
            .Include(booking => booking.PriceLines)
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
            .Include(booking => booking.PriceLines)
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
            .Include(booking => booking.PriceLines)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(booking => booking.Status == status.Value);
        }

        query = query
            .OrderByDescending(booking => booking.CreatedAt)
            .ThenByDescending(booking => booking.Id);

        return query.ApplyPagination(page, pageSize);
    }

    private static BookingPricingResult CreatePriceSnapshot(
        Service service,
        IEnumerable<BookingPriceItemRequest>? requestedItems)
    {
        const decimal maximumPersistablePrice = 9999999999999999.99m;
        var items = requestedItems?.ToList() ?? [];

        if (service.PricingMode == ServicePricingMode.Fixed)
        {
            if (items.Count > 0)
            {
                return new BookingPricingResult([], 0, "This service uses fixed pricing and does not accept pricing items.");
            }

            return new BookingPricingResult(
                [new BookingPriceLine
                {
                    Description = service.Name,
                    Quantity = 1,
                    UnitPrice = service.BasePrice,
                    LineTotal = service.BasePrice
                }],
                service.BasePrice,
                null);
        }

        if (service.PricingMode == ServicePricingMode.TimeBased)
        {
            var error = service.TimePricingPolicy == null
                ? "Time-based pricing is not configured for this service."
                : "Time-based pricing calculation is not available yet.";
            return new BookingPricingResult([], 0, error);
        }

        if (service.PricingMode != ServicePricingMode.PerUnit)
        {
            return new BookingPricingResult([], 0, "The service pricing mode is not supported.");
        }

        if (items.Count == 0)
        {
            return new BookingPricingResult([], 0, "At least one pricing item is required for this service.");
        }

        if (items.GroupBy(item => item.PriceRuleId).Any(group => group.Count() > 1))
        {
            return new BookingPricingResult([], 0, "Each pricing item can only be selected once.");
        }

        var rulesById = service.PriceRules
            .Where(rule => rule.IsActive)
            .ToDictionary(rule => rule.Id);
        var lines = new List<BookingPriceLine>();
        decimal totalPrice = 0;

        foreach (var item in items)
        {
            if (item.Quantity is < 1 or > 100000)
            {
                return new BookingPricingResult([], 0, "Each pricing item quantity must be between 1 and 100000.");
            }

            if (!rulesById.TryGetValue(item.PriceRuleId, out var rule))
            {
                return new BookingPricingResult([], 0, "One or more selected pricing items are not available for this service.");
            }

            if (rule.UnitPrice > maximumPersistablePrice / item.Quantity)
            {
                return new BookingPricingResult([], 0, "The calculated booking price is too large.");
            }

            var lineTotal = rule.UnitPrice * item.Quantity;
            if (lineTotal > maximumPersistablePrice - totalPrice)
            {
                return new BookingPricingResult([], 0, "The calculated booking price is too large.");
            }

            lines.Add(new BookingPriceLine
            {
                Description = rule.UnitName,
                Quantity = item.Quantity,
                UnitPrice = rule.UnitPrice,
                LineTotal = lineTotal
            });
            totalPrice += lineTotal;
        }

        return new BookingPricingResult(lines, totalPrice, null);
    }

    private async Task<BookingDiscountResult> CreateDiscountSnapshotAsync(
        string? promotionCode,
        int serviceId,
        decimal subtotal,
        DateTimeOffset scheduledStart)
    {
        if (string.IsNullOrWhiteSpace(promotionCode))
        {
            return new BookingDiscountResult(null, 0, null, null);
        }

        var normalizedCode = promotionCode.Trim().ToUpperInvariant();
        var promotion = await _db.Promotions.SingleOrDefaultAsync(item => item.Code == normalizedCode);
        if (promotion == null ||
            !promotion.IsActive ||
            promotion.StartsAt > scheduledStart ||
            (promotion.EndsAt.HasValue && promotion.EndsAt.Value < scheduledStart) ||
            (promotion.EligibleServiceId.HasValue && promotion.EligibleServiceId.Value != serviceId) ||
            (promotion.UsageLimit.HasValue && promotion.TimesUsed >= promotion.UsageLimit.Value))
        {
            return new BookingDiscountResult(null, 0, null, "The promotion code is not valid for this booking.");
        }

        var discountAmount = promotion.DiscountType == PromotionDiscountType.Percentage
            ? Math.Round(subtotal * promotion.DiscountValue / 100m, 2, MidpointRounding.AwayFromZero)
            : promotion.DiscountValue;

        if (discountAmount <= 0)
        {
            return new BookingDiscountResult(null, 0, null, "The promotion code is not valid for this booking.");
        }

        if (discountAmount > subtotal)
        {
            discountAmount = subtotal;
        }

        return new BookingDiscountResult(
            promotion,
            discountAmount,
            new BookingPriceLine
            {
                Description = $"Promotion: {promotion.Code}",
                Quantity = 1,
                UnitPrice = -discountAmount,
                LineTotal = -discountAmount
            },
            null);
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
