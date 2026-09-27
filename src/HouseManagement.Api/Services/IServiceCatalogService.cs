using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IServiceCatalogService
{
    Task<IEnumerable<Service>> GetActiveAsync(int? page = null, int? pageSize = null);
    Task<IEnumerable<Service>> GetAllAsync(int? page = null, int? pageSize = null, bool? isActive = null);
    Task<Service?> GetActiveByIdAsync(int id);
    Task<Service?> GetByIdAsync(int id);
    Task<bool> CodeExistsAsync(string code, int? excludingId = null);
    Task<Service?> CreateAsync(Service service);
    Task<bool> UpdateAsync(Service service);
    Task<bool> SetActiveAsync(int id, bool isActive);
    Task<ServicePriceRule?> CreatePriceRuleAsync(int serviceId, ServicePriceRule rule);
    Task<ServicePriceRuleUpdateResult> UpdatePriceRuleAsync(int serviceId, int ruleId, ServicePriceRule rule);
    Task<bool> SetPriceRuleActiveAsync(int serviceId, int ruleId, bool isActive);

    // Public holiday calendar used to evaluate the Holiday surcharge trigger. Not service-specific.
    Task<IReadOnlyCollection<DateOnly>> GetHolidayDatesAsync();
}

public sealed record ServicePriceRuleUpdateResult(bool Exists, bool HasDuplicateUnitName);
