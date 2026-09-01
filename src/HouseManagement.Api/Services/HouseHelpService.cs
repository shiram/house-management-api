using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using HouseManagement.Api.Common;

namespace HouseManagement.Api.Services;

public class HouseHelpService : IHouseHelpService
{
    private readonly HouseContext _db;

    public HouseHelpService(HouseContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<HouseHelp>> GetAllAsync()
    {
        return await _db.HouseHelps.Include(h => h.Skills).AsNoTracking().ToListAsync();
    }

    public async Task<IEnumerable<HouseHelp>> GetFilteredAsync(string? city = null, string? skill = null, bool? isActive = null, int? page = null, int? pageSize = null, int? userId = null)
    {
        var query = _db.HouseHelps.Include(h => h.Skills).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(city))
        {
            var c = city.Trim();
            query = query.Where(h => h.City == c);
        }

        if (!string.IsNullOrWhiteSpace(skill))
        {
            var s = skill.Trim();
            query = query.Where(h => h.Skills.Any(sk => sk.ServiceName == s));
        }

        if (isActive.HasValue)
        {
            query = query.Where(h => h.IsActive == isActive.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(h => h.UserId == userId.Value);
        }

        return await query
            .OrderBy(h => h.LastName)
            .ThenBy(h => h.FirstName)
            .ThenBy(h => h.Id)
            .ApplyPagination(page, pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<HouseHelp>> GetEligibleAsync(int serviceId, string? city = null)
    {
        var service = await _db.Services.AsNoTracking().SingleOrDefaultAsync(item => item.Id == serviceId);
        if (service == null)
        {
            return Enumerable.Empty<HouseHelp>();
        }

        var serviceName = service.Name.Trim();
        var serviceCode = service.Code.Trim();
        var normalizedCity = string.IsNullOrWhiteSpace(city) ? null : city.Trim();

        var query = _db.HouseHelps
            .Include(h => h.Skills)
            .AsNoTracking()
            .Where(h => h.IsActive);

        if (normalizedCity != null)
        {
            query = query.Where(h => h.City == normalizedCity);
        }

        query = query.Where(h => h.Skills.Any(skill =>
            skill.ServiceName.ToLower() == serviceName.ToLower() ||
            skill.ServiceName.ToLower() == serviceCode.ToLower()));

        return await query
            .OrderBy(h => h.LastName)
            .ThenBy(h => h.FirstName)
            .ToListAsync();
    }

    public async Task<HouseHelp?> GetByIdAsync(int id)
    {
        return await _db.HouseHelps.Include(h => h.Skills).SingleOrDefaultAsync(h => h.Id == id);
    }

    public async Task<HouseHelp> CreateAsync(HouseHelp houseHelp, IEnumerable<string>? skills)
    {
        _db.HouseHelps.Add(houseHelp);
        if (skills != null)
        {
            foreach (var s in skills.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                _db.HouseHelpSkills.Add(new HouseHelpSkill { HouseHelp = houseHelp, ServiceName = s.Trim() });
            }
        }

        await _db.SaveChangesAsync();
        return houseHelp;
    }

    public async Task<bool> UpdateAsync(HouseHelp houseHelp, IEnumerable<string>? skills)
    {
        var existing = await _db.HouseHelps.Include(h => h.Skills).SingleOrDefaultAsync(h => h.Id == houseHelp.Id);
        if (existing == null) return false;

        existing.FirstName = houseHelp.FirstName;
        existing.LastName = houseHelp.LastName;
        existing.Phone = houseHelp.Phone;
        existing.City = houseHelp.City;
        existing.Address = houseHelp.Address;

        // update skills: simple replace
        var incoming = (skills ?? Enumerable.Empty<string>()).Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        // remove skills not present
        var toRemove = existing.Skills.Where(sk => !incoming.Contains(sk.ServiceName)).ToList();
        _db.HouseHelpSkills.RemoveRange(toRemove);

        var existingNames = existing.Skills.Select(sk => sk.ServiceName).ToHashSet();
        var toAdd = incoming.Where(s => !existingNames.Contains(s)).Select(s => new HouseHelpSkill { HouseHelpId = existing.Id, ServiceName = s }).ToList();
        if (toAdd.Any()) _db.HouseHelpSkills.AddRange(toAdd);

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetActiveAsync(int id, bool isActive)
    {
        var existing = await _db.HouseHelps.SingleOrDefaultAsync(h => h.Id == id);
        if (existing == null) return false;
        existing.IsActive = isActive;
        await _db.SaveChangesAsync();
        return true;
    }
}