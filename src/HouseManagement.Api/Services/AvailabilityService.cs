using HouseManagement.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class AvailabilityService : IAvailabilityService
{
    private readonly HouseContext _db;

    public AvailabilityService(HouseContext db)
    {
        _db = db;
    }

    public async Task<AvailabilityQueryResult?> GetAsync(int houseHelpId, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        if (!await _db.HouseHelps.AnyAsync(houseHelp => houseHelp.Id == houseHelpId))
        {
            return null;
        }

        var weeklySlots = await _db.HouseHelpAvailabilities
            .AsNoTracking()
            .Where(availability => availability.HouseHelpId == houseHelpId && availability.IsActive)
            .OrderBy(availability => availability.DayOfWeek)
            .ThenBy(availability => availability.StartTime)
            .ToListAsync();

        var exceptions = await _db.HouseHelpAvailabilityExceptions
            .AsNoTracking()
            .Where(exception =>
                exception.HouseHelpId == houseHelpId &&
                exception.IsActive &&
                (!from.HasValue || exception.EndsAt > from.Value) &&
                (!to.HasValue || exception.StartsAt < to.Value))
            .OrderBy(exception => exception.StartsAt)
            .ToListAsync();

        return new AvailabilityQueryResult(houseHelpId, weeklySlots, exceptions);
    }
}
