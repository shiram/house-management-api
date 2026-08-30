using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IBookingService
{
    Task<BookingCreationResult> CreateAnonymousAsync(CreateAnonymousBookingRequest request);
    Task<BookingAssignmentResult> AssignHouseHelpAsync(int bookingId, int houseHelpId, int? assignedByUserId = null);
    Task<Booking?> GetByIdAsync(int id);
    Task<Booking?> GetByReferenceAsync(string reference);
    Task<int?> GetHouseHelpIdForUserAsync(int userId);
    Task<int?> GetClientIdForUserAsync(int userId);
    Task<IReadOnlyList<Booking>> GetListAsync(BookingStatus? status = null, int? page = null, int? pageSize = null, int? houseHelpId = null, int? clientId = null);
    Task<IReadOnlyList<Booking>> GetListForHouseHelpAsync(int houseHelpId, BookingStatus? status = null, int? page = null, int? pageSize = null);
    Task<IReadOnlyList<Booking>> GetListForClientAsync(int clientId, BookingStatus? status = null, int? page = null, int? pageSize = null);
}

public sealed record BookingCreationResult(Booking? Booking, string? Error);
public sealed record BookingAssignmentResult(Booking? Booking, string? Error);
