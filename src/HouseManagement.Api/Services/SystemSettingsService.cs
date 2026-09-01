using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using HouseManagement.Api.Common;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class SystemSettingsService : ISystemSettingsService
{
    private readonly HouseContext _db;

    public SystemSettingsService(HouseContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<SystemSetting>> GetAllAsync(int? page = null, int? pageSize = null)
    {
        return await _db.SystemSettings
            .AsNoTracking()
            .OrderBy(setting => setting.Key)
            .ApplyPagination(page, pageSize)
            .ToListAsync();
    }

    public async Task<SystemSetting?> GetByKeyAsync(string key)
    {
        var normalizedKey = key.Trim();
        return await _db.SystemSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(setting => setting.Key == normalizedKey);
    }

    public async Task<SystemSetting> UpsertAsync(string key, string value, string? description, int? updatedByUserId)
    {
        var normalizedKey = key.Trim();
        var existing = await _db.SystemSettings.SingleOrDefaultAsync(setting => setting.Key == normalizedKey);

        if (existing == null)
        {
            existing = new SystemSetting
            {
                Key = normalizedKey,
                Value = value.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                UpdatedByUserId = updatedByUserId
            };
            _db.SystemSettings.Add(existing);
        }
        else
        {
            existing.Value = value.Trim();
            existing.Description = string.IsNullOrWhiteSpace(description) ? existing.Description : description.Trim();
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedByUserId = updatedByUserId;
        }

        await _db.SaveChangesAsync();
        return existing;
    }
}
