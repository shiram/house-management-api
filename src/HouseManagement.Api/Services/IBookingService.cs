using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IBookingService
{
    Task<BookingCreationResult> CreateAnonymousAsync(CreateAnonymousBookingRequest request);
    Task<Booking?> GetByIdAsync(int id);
}

public sealed record BookingCreationResult(Booking? Booking, string? Error);
