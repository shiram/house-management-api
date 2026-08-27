using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
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

    public async Task<bool> ReplaceWeeklyAsync(int houseHelpId, IEnumerable<HouseHelpAvailability> slots)
    {
        if (!await _db.HouseHelps.AnyAsync(houseHelp => houseHelp.Id == houseHelpId))
        {
            return false;
        }

        var existing = await _db.HouseHelpAvailabilities
            .Where(availability => availability.HouseHelpId == houseHelpId)
            .ToListAsync();
        _db.HouseHelpAvailabilities.RemoveRange(existing);

        var replacements = slots.Select(slot => new HouseHelpAvailability
        {
            HouseHelpId = houseHelpId,
            DayOfWeek = slot.DayOfWeek,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            IsActive = slot.IsActive
        });
        await _db.HouseHelpAvailabilities.AddRangeAsync(replacements);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int?> GetHouseHelpIdForUserAsync(int userId)
    {
        return await _db.HouseHelps
            .Where(houseHelp => houseHelp.UserId == userId)
            .Select(houseHelp => (int?)houseHelp.Id)
            .SingleOrDefaultAsync();
    }
}
