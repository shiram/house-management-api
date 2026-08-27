using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IAvailabilityService
{
    Task<AvailabilityQueryResult?> GetAsync(int houseHelpId, DateTimeOffset? from = null, DateTimeOffset? to = null);
}

public sealed record AvailabilityQueryResult(
    int HouseHelpId,
    IEnumerable<HouseHelpAvailability> WeeklySlots,
    IEnumerable<HouseHelpAvailabilityException> Exceptions);
