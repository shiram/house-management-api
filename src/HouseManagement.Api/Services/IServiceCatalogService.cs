using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IServiceCatalogService
{
    Task<IEnumerable<Service>> GetActiveAsync();
    Task<Service?> GetActiveByIdAsync(int id);
    Task<Service?> CreateAsync(Service service);
}
