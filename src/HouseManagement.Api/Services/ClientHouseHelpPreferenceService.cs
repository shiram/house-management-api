using HouseManagement.Api.Common;
using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class ClientHouseHelpPreferenceService : IClientHouseHelpPreferenceService
{
    private readonly HouseContext _db;

    public ClientHouseHelpPreferenceService(HouseContext db)
    {
        _db = db;
    }

    public async Task<ClientHouseHelpPreferenceResult> AddAsync(int clientUserId, int houseHelpId)
    {
        var client = await _db.Clients.SingleOrDefaultAsync(item => item.UserId == clientUserId);
        if (client == null)
        {
            return new ClientHouseHelpPreferenceResult(null, "The current user does not have a client profile.");
        }

        var houseHelp = await _db.HouseHelps
            .SingleOrDefaultAsync(item => item.Id == houseHelpId && item.IsActive);
        if (houseHelp == null)
        {
            return new ClientHouseHelpPreferenceResult(null, "The requested active HouseHelp was not found.");
        }

        var exists = await _db.ClientHouseHelpPreferences
            .AnyAsync(item => item.ClientId == client.Id && item.HouseHelpId == houseHelpId);
        if (exists)
        {
            return new ClientHouseHelpPreferenceResult(null, "This HouseHelp is already preferred.");
        }

        var preference = new ClientHouseHelpPreference
        {
            ClientId = client.Id,
            HouseHelpId = houseHelp.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.ClientHouseHelpPreferences.Add(preference);
        await _db.SaveChangesAsync();

        return new ClientHouseHelpPreferenceResult(preference, null);
    }

    public async Task<bool> RemoveAsync(int clientUserId, int houseHelpId)
    {
        var preference = await _db.ClientHouseHelpPreferences
            .Include(item => item.Client)
            .SingleOrDefaultAsync(item =>
                item.HouseHelpId == houseHelpId &&
                item.Client.UserId == clientUserId);
        if (preference == null)
        {
            return false;
        }

        _db.ClientHouseHelpPreferences.Remove(preference);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<ClientHouseHelpPreference>> GetForClientAsync(int clientUserId, int? page = null, int? pageSize = null)
    {
        var query = _db.ClientHouseHelpPreferences
            .AsNoTracking()
            .Include(item => item.Client)
            .Include(item => item.HouseHelp)
                .ThenInclude(houseHelp => houseHelp.Skills)
            .Where(item => item.Client.UserId == clientUserId && item.HouseHelp.IsActive)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id);

        return await query.ApplyPagination(page, pageSize).ToListAsync();
    }
}
