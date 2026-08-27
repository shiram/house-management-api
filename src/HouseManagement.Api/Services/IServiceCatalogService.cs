using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IServiceCatalogService
{
    Task<IEnumerable<Service>> GetActiveAsync();
}
