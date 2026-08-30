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

    public async Task<AvailabilityUpdateResult> ReplaceWeeklyAsync(int houseHelpId, IEnumerable<HouseHelpAvailability> slots)
    {
        if (!await _db.HouseHelps.AnyAsync(houseHelp => houseHelp.Id == houseHelpId))
        {
            return AvailabilityUpdateResult.HouseHelpNotFound;
        }

        var replacements = slots.ToList();
        if (replacements.Any(slot => slot.StartTime >= slot.EndTime) ||
            replacements
                .GroupBy(slot => slot.DayOfWeek)
                .SelectMany(group => group.OrderBy(slot => slot.StartTime).Zip(
                    group.OrderBy(slot => slot.StartTime).Skip(1),
                    (current, next) => new { Current = current, Next = next }))
                .Any(pair => pair.Next.StartTime < pair.Current.EndTime))
        {
            return AvailabilityUpdateResult.Invalid;
        }

        var existing = await _db.HouseHelpAvailabilities
            .Where(availability => availability.HouseHelpId == houseHelpId)
            .ToListAsync();
        _db.HouseHelpAvailabilities.RemoveRange(existing);

        var replacementEntities = replacements.Select(slot => new HouseHelpAvailability
        {
            HouseHelpId = houseHelpId,
            DayOfWeek = slot.DayOfWeek,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            IsActive = slot.IsActive
        });
        await _db.HouseHelpAvailabilities.AddRangeAsync(replacementEntities);
        await _db.SaveChangesAsync();
        return AvailabilityUpdateResult.Updated;
    }

    public async Task<int?> GetHouseHelpIdForUserAsync(int userId)
    {
        return await _db.HouseHelps
            .Where(houseHelp => houseHelp.UserId == userId)
            .Select(houseHelp => (int?)houseHelp.Id)
            .SingleOrDefaultAsync();
    }
}
