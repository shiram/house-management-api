using HouseManagement.Api.Data;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class BookingService : IBookingService
{
    private readonly HouseContext _db;

    public BookingService(HouseContext db)
    {
        _db = db;
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
        if (transaction != null)
        {
            await transaction.CommitAsync();
            await transaction.DisposeAsync();
        }
        return new BookingCreationResult(booking, null);
    }

    public async Task<Booking?> GetByIdAsync(int id)
    {
        return await _db.Bookings
            .AsNoTracking()
            .Include(booking => booking.Service)
            .Include(booking => booking.ServiceAddress)
            .SingleOrDefaultAsync(booking => booking.Id == id);
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
