using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class ServiceCatalogService : IServiceCatalogService
{
    private readonly HouseContext _db;

    public ServiceCatalogService(HouseContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Service>> GetActiveAsync()
    {
        return await _db.Services
            .AsNoTracking()
            .Where(service => service.IsActive)
            .OrderBy(service => service.Name)
            .ThenBy(service => service.Code)
            .ToListAsync();
    }
}
