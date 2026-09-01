using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface ISystemSettingsService
{
    Task<IEnumerable<SystemSetting>> GetAllAsync(int? page = null, int? pageSize = null);
    Task<SystemSetting?> GetByKeyAsync(string key);
    Task<SystemSetting> UpsertAsync(string key, string value, string? description, int? updatedByUserId);
}
