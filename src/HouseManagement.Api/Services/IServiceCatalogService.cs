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

    // Time-based pricing policy administration (T388). A service has at most one policy; Upsert
    // creates it on first call and updates it thereafter. Validates the same invariants enforced
    // by the ServiceTimePricingPolicies DB check constraints so callers get a clean 400 instead of
    // a database exception.
    Task<ServiceTimePricingPolicyUpsertResult> UpsertTimePricingPolicyAsync(int serviceId, ServiceTimePricingPolicy policy);

    // Service fee administration (T388).
    Task<IEnumerable<ServiceFee>> GetFeesAsync(int serviceId);
    Task<ServiceFeeCreateResult> CreateFeeAsync(int serviceId, ServiceFee fee);
    Task<ServiceFeeUpdateResult> UpdateFeeAsync(int serviceId, int feeId, ServiceFee fee);
    Task<bool> SetFeeActiveAsync(int serviceId, int feeId, bool isActive);

    // Service surcharge administration (T388).
    Task<IEnumerable<ServiceSurcharge>> GetSurchargesAsync(int serviceId);
    Task<ServiceSurchargeCreateResult> CreateSurchargeAsync(int serviceId, ServiceSurcharge surcharge);
    Task<ServiceSurchargeUpdateResult> UpdateSurchargeAsync(int serviceId, int surchargeId, ServiceSurcharge surcharge);
    Task<bool> SetSurchargeActiveAsync(int serviceId, int surchargeId, bool isActive);

    // Public holiday calendar administration (T388). GetHolidayDatesAsync (below) remains the
    // lightweight read used by pricing calculation; these manage the calendar itself.
    Task<IEnumerable<PublicHoliday>> GetPublicHolidaysAsync();
    Task<PublicHoliday?> CreatePublicHolidayAsync(PublicHoliday holiday);
    Task<bool> DeletePublicHolidayAsync(int id);

    // Public holiday calendar used to evaluate the Holiday surcharge trigger. Not service-specific.
    Task<IReadOnlyCollection<DateOnly>> GetHolidayDatesAsync();

    // The platform-wide tax/VAT rate percentage, resolved from the generic system settings store.
    // Callers must still check Service.IsTaxable before applying it to a specific service.
    Task<decimal> GetTaxRatePercentageAsync();
    Task<string> GetCurrencyCodeAsync();
}

public sealed record ServicePriceRuleUpdateResult(bool Exists, bool HasDuplicateUnitName);

public sealed record ServiceTimePricingPolicyUpsertResult(bool ServiceExists, bool IsValid, ServiceTimePricingPolicy? Policy);

public sealed record ServiceFeeCreateResult(bool ServiceExists, bool IsValid, ServiceFee? Fee);

public sealed record ServiceFeeUpdateResult(bool Exists, bool IsValid);

public sealed record ServiceSurchargeCreateResult(bool ServiceExists, bool IsValid, ServiceSurcharge? Surcharge);

public sealed record ServiceSurchargeUpdateResult(bool Exists, bool IsValid);
