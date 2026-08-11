using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HouseManagement.Api.Data;
using HouseManagement.Api.Models;

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