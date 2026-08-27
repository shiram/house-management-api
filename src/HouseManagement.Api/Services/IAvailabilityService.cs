using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IAvailabilityService
{
    Task<AvailabilityQueryResult?> GetAsync(int houseHelpId, DateTimeOffset? from = null, DateTimeOffset? to = null);
    Task<bool> ReplaceWeeklyAsync(int houseHelpId, IEnumerable<HouseHelpAvailability> slots);
}

public sealed record AvailabilityQueryResult(
    int HouseHelpId,
    IEnumerable<HouseHelpAvailability> WeeklySlots,
    IEnumerable<HouseHelpAvailabilityException> Exceptions);
