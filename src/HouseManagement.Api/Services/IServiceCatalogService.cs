using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IServiceCatalogService
{
    Task<IEnumerable<Service>> GetActiveAsync();
    Task<IEnumerable<Service>> GetAllAsync(int? page = null, int? pageSize = null);
    Task<Service?> GetActiveByIdAsync(int id);
    Task<Service?> GetByIdAsync(int id);
    Task<bool> CodeExistsAsync(string code, int? excludingId = null);
    Task<Service?> CreateAsync(Service service);
    Task<bool> UpdateAsync(Service service);
    Task<bool> SetActiveAsync(int id, bool isActive);
}
