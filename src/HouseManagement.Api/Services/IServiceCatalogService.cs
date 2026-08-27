using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IServiceCatalogService
{
    Task<IEnumerable<Service>> GetActiveAsync();
    Task<Service?> GetActiveByIdAsync(int id);
    Task<Service?> GetByIdAsync(int id);
    Task<bool> CodeExistsAsync(string code, int? excludingId = null);
    Task<Service?> CreateAsync(Service service);
    Task<bool> UpdateAsync(Service service);
}
