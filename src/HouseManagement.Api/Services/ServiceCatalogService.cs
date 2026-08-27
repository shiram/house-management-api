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

    public async Task<Service?> GetActiveByIdAsync(int id)
    {
        return await _db.Services
            .AsNoTracking()
            .SingleOrDefaultAsync(service => service.Id == id && service.IsActive);
    }

    public async Task<Service?> GetByIdAsync(int id)
    {
        return await _db.Services.AsNoTracking().SingleOrDefaultAsync(service => service.Id == id);
    }

    public async Task<bool> CodeExistsAsync(string code, int? excludingId = null)
    {
        var normalizedCode = code.Trim();
        return await _db.Services.AnyAsync(service =>
            service.Code == normalizedCode && (!excludingId.HasValue || service.Id != excludingId.Value));
    }

    public async Task<Service?> CreateAsync(Service service)
    {
        service.Code = service.Code.Trim();
        service.Name = service.Name.Trim();
        service.Description = string.IsNullOrWhiteSpace(service.Description) ? null : service.Description.Trim();

        if (await CodeExistsAsync(service.Code))
        {
            return null;
        }

        _db.Services.Add(service);
        await _db.SaveChangesAsync();
        return service;
    }

    public async Task<bool> UpdateAsync(Service service)
    {
        var existing = await _db.Services.SingleOrDefaultAsync(item => item.Id == service.Id);
        if (existing == null) return false;

        existing.Code = service.Code.Trim();
        existing.Name = service.Name.Trim();
        existing.Description = string.IsNullOrWhiteSpace(service.Description) ? null : service.Description.Trim();
        existing.BasePrice = service.BasePrice;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }
}
