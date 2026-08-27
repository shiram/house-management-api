using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IBookingService
{
    Task<BookingCreationResult> CreateAnonymousAsync(CreateAnonymousBookingRequest request);
}

public sealed record BookingCreationResult(Booking? Booking, string? Error);
