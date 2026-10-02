using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using HouseManagement.Api.Common;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class ServiceCatalogService : IServiceCatalogService
{
    private readonly HouseContext _db;

    public ServiceCatalogService(HouseContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Service>> GetActiveAsync(int? page = null, int? pageSize = null)
    {
        return await _db.Services
            .AsNoTracking()
            .Include(service => service.PriceRules.Where(rule => rule.IsActive))
            .Where(service => service.IsActive)
            .OrderBy(service => service.Name)
            .ThenBy(service => service.Code)
            .ApplyPagination(page, pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<Service>> GetAllAsync(int? page = null, int? pageSize = null, bool? isActive = null)
    {
        var query = _db.Services
            .AsNoTracking()
            .Include(service => service.PriceRules)
            .AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(service => service.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(service => service.Name)
            .ThenBy(service => service.Code)
            .ApplyPagination(page, pageSize)
            .ToListAsync();
    }

    public async Task<Service?> GetActiveByIdAsync(int id)
    {
        return await _db.Services
            .AsNoTracking()
            .Include(service => service.PriceRules.Where(rule => rule.IsActive))
            .Include(service => service.TimePricingPolicy)
            .Include(service => service.Fees.Where(fee => fee.IsActive))
            .Include(service => service.Surcharges.Where(surcharge => surcharge.IsActive))
            .SingleOrDefaultAsync(service => service.Id == id && service.IsActive);
    }

    public async Task<Service?> GetByIdAsync(int id)
    {
        return await _db.Services
            .AsNoTracking()
            .Include(service => service.PriceRules)
            .SingleOrDefaultAsync(service => service.Id == id);
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
        existing.PricingMode = service.PricingMode;
        existing.IsTaxable = service.IsTaxable;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetActiveAsync(int id, bool isActive)
    {
        var existing = await _db.Services.SingleOrDefaultAsync(item => item.Id == id);
        if (existing == null) return false;

        existing.IsActive = isActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ServicePriceRule?> CreatePriceRuleAsync(int serviceId, ServicePriceRule rule)
    {
        var serviceExists = await _db.Services.AnyAsync(service => service.Id == serviceId);
        if (!serviceExists)
        {
            return null;
        }

        var unitName = rule.UnitName.Trim();
        if (string.IsNullOrWhiteSpace(unitName))
        {
            return null;
        }

        var exists = await _db.ServicePriceRules.AnyAsync(item =>
            item.ServiceId == serviceId &&
            item.UnitName != null &&
            item.UnitName.Trim().Equals(unitName, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            return null;
        }

        rule.ServiceId = serviceId;
        rule.UnitName = unitName;
        _db.ServicePriceRules.Add(rule);
        await _db.SaveChangesAsync();
        return rule;
    }

    public async Task<ServicePriceRuleUpdateResult> UpdatePriceRuleAsync(int serviceId, int ruleId, ServicePriceRule rule)
    {
        var existing = await _db.ServicePriceRules
            .SingleOrDefaultAsync(item => item.Id == ruleId && item.ServiceId == serviceId);
        if (existing == null)
        {
            return new ServicePriceRuleUpdateResult(false, false);
        }

        var unitName = rule.UnitName.Trim();
        if (string.IsNullOrWhiteSpace(unitName))
        {
            return new ServicePriceRuleUpdateResult(true, false);
        }

        var duplicate = await _db.ServicePriceRules.AnyAsync(item =>
            item.ServiceId == serviceId &&
            item.Id != ruleId &&
            item.UnitName != null &&
            item.UnitName.Trim().Equals(unitName, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            return new ServicePriceRuleUpdateResult(true, true);
        }

        existing.UnitName = unitName;
        existing.UnitPrice = rule.UnitPrice;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return new ServicePriceRuleUpdateResult(true, false);
    }

    public async Task<bool> SetPriceRuleActiveAsync(int serviceId, int ruleId, bool isActive)
    {
        var existing = await _db.ServicePriceRules
            .SingleOrDefaultAsync(item => item.Id == ruleId && item.ServiceId == serviceId);
        if (existing == null)
        {
            return false;
        }

        existing.IsActive = isActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ServiceTimePricingPolicyUpsertResult> UpsertTimePricingPolicyAsync(int serviceId, ServiceTimePricingPolicy policy)
    {
        var serviceExists = await _db.Services.AnyAsync(service => service.Id == serviceId);
        if (!serviceExists)
        {
            return new ServiceTimePricingPolicyUpsertResult(false, false, null);
        }

        if (!IsValidTimePricingPolicy(policy))
        {
            return new ServiceTimePricingPolicyUpsertResult(true, false, null);
        }

        var existing = await _db.ServiceTimePricingPolicies
            .SingleOrDefaultAsync(item => item.ServiceId == serviceId);

        if (existing == null)
        {
            existing = new ServiceTimePricingPolicy { ServiceId = serviceId };
            _db.ServiceTimePricingPolicies.Add(existing);
        }

        existing.BillingUnit = policy.BillingUnit;
        existing.UnitPrice = policy.UnitPrice;
        existing.MinimumBillableDurationMinutes = policy.MinimumBillableDurationMinutes;
        existing.BillingIncrementMinutes = policy.BillingIncrementMinutes;
        existing.RoundingPolicy = policy.RoundingPolicy;
        existing.OvertimeThresholdMinutes = policy.OvertimeThresholdMinutes;
        existing.OvertimeUnitPrice = policy.OvertimeUnitPrice;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return new ServiceTimePricingPolicyUpsertResult(true, true, existing);
    }

    public async Task<IEnumerable<ServiceFee>> GetFeesAsync(int serviceId)
    {
        return await _db.ServiceFees
            .AsNoTracking()
            .Where(fee => fee.ServiceId == serviceId)
            .OrderBy(fee => fee.Name)
            .ToListAsync();
    }

    public async Task<ServiceFeeCreateResult> CreateFeeAsync(int serviceId, ServiceFee fee)
    {
        var serviceExists = await _db.Services.AnyAsync(service => service.Id == serviceId);
        if (!serviceExists)
        {
            return new ServiceFeeCreateResult(false, false, null);
        }

        var name = fee.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || !IsValidAdjustmentAmount(fee.AdjustmentType, fee.Amount))
        {
            return new ServiceFeeCreateResult(true, false, null);
        }

        fee.ServiceId = serviceId;
        fee.Name = name;
        _db.ServiceFees.Add(fee);
        await _db.SaveChangesAsync();
        return new ServiceFeeCreateResult(true, true, fee);
    }

    public async Task<ServiceFeeUpdateResult> UpdateFeeAsync(int serviceId, int feeId, ServiceFee fee)
    {
        var existing = await _db.ServiceFees
            .SingleOrDefaultAsync(item => item.Id == feeId && item.ServiceId == serviceId);
        if (existing == null)
        {
            return new ServiceFeeUpdateResult(false, false);
        }

        var name = fee.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || !IsValidAdjustmentAmount(fee.AdjustmentType, fee.Amount))
        {
            return new ServiceFeeUpdateResult(true, false);
        }

        existing.Name = name;
        existing.AdjustmentType = fee.AdjustmentType;
        existing.Amount = fee.Amount;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return new ServiceFeeUpdateResult(true, true);
    }

    public async Task<bool> SetFeeActiveAsync(int serviceId, int feeId, bool isActive)
    {
        var existing = await _db.ServiceFees
            .SingleOrDefaultAsync(item => item.Id == feeId && item.ServiceId == serviceId);
        if (existing == null)
        {
            return false;
        }

        existing.IsActive = isActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ServiceSurcharge>> GetSurchargesAsync(int serviceId)
    {
        return await _db.ServiceSurcharges
            .AsNoTracking()
            .Where(surcharge => surcharge.ServiceId == serviceId)
            .OrderBy(surcharge => surcharge.Name)
            .ToListAsync();
    }

    public async Task<ServiceSurchargeCreateResult> CreateSurchargeAsync(int serviceId, ServiceSurcharge surcharge)
    {
        var serviceExists = await _db.Services.AnyAsync(service => service.Id == serviceId);
        if (!serviceExists)
        {
            return new ServiceSurchargeCreateResult(false, false, null);
        }

        var name = surcharge.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) ||
            !IsValidAdjustmentAmount(surcharge.AdjustmentType, surcharge.Amount) ||
            !IsValidSurchargeTriggerFields(surcharge))
        {
            return new ServiceSurchargeCreateResult(true, false, null);
        }

        surcharge.ServiceId = serviceId;
        surcharge.Name = name;
        NormalizeSurchargeTriggerFields(surcharge);
        _db.ServiceSurcharges.Add(surcharge);
        await _db.SaveChangesAsync();
        return new ServiceSurchargeCreateResult(true, true, surcharge);
    }

    public async Task<ServiceSurchargeUpdateResult> UpdateSurchargeAsync(int serviceId, int surchargeId, ServiceSurcharge surcharge)
    {
        var existing = await _db.ServiceSurcharges
            .SingleOrDefaultAsync(item => item.Id == surchargeId && item.ServiceId == serviceId);
        if (existing == null)
        {
            return new ServiceSurchargeUpdateResult(false, false);
        }

        var name = surcharge.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) ||
            !IsValidAdjustmentAmount(surcharge.AdjustmentType, surcharge.Amount) ||
            !IsValidSurchargeTriggerFields(surcharge))
        {
            return new ServiceSurchargeUpdateResult(true, false);
        }

        NormalizeSurchargeTriggerFields(surcharge);
        existing.Name = name;
        existing.TriggerType = surcharge.TriggerType;
        existing.AdjustmentType = surcharge.AdjustmentType;
        existing.Amount = surcharge.Amount;
        existing.AfterHoursStartMinutes = surcharge.AfterHoursStartMinutes;
        existing.AfterHoursEndMinutes = surcharge.AfterHoursEndMinutes;
        existing.UrgentLeadTimeMinutes = surcharge.UrgentLeadTimeMinutes;
        existing.LocationMatch = surcharge.LocationMatch;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return new ServiceSurchargeUpdateResult(true, true);
    }

    public async Task<bool> SetSurchargeActiveAsync(int serviceId, int surchargeId, bool isActive)
    {
        var existing = await _db.ServiceSurcharges
            .SingleOrDefaultAsync(item => item.Id == surchargeId && item.ServiceId == serviceId);
        if (existing == null)
        {
            return false;
        }

        existing.IsActive = isActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<PublicHoliday>> GetPublicHolidaysAsync()
    {
        return await _db.PublicHolidays
            .AsNoTracking()
            .OrderBy(holiday => holiday.Date)
            .ToListAsync();
    }

    public async Task<PublicHoliday?> CreatePublicHolidayAsync(PublicHoliday holiday)
    {
        var exists = await _db.PublicHolidays.AnyAsync(item => item.Date == holiday.Date);
        if (exists)
        {
            return null;
        }

        holiday.Name = holiday.Name.Trim();
        _db.PublicHolidays.Add(holiday);
        await _db.SaveChangesAsync();
        return holiday;
    }

    public async Task<bool> DeletePublicHolidayAsync(int id)
    {
        var existing = await _db.PublicHolidays.SingleOrDefaultAsync(item => item.Id == id);
        if (existing == null)
        {
            return false;
        }

        _db.PublicHolidays.Remove(existing);
        await _db.SaveChangesAsync();
        return true;
    }

    // Mirrors CK_ServiceFees_Amount / CK_ServiceSurcharges_Amount: a fixed amount must be
    // positive; a percentage must be positive and no greater than 100.
    private static bool IsValidAdjustmentAmount(PricingAdjustmentType adjustmentType, decimal amount)
    {
        return adjustmentType switch
        {
            PricingAdjustmentType.FixedAmount => amount > 0,
            PricingAdjustmentType.Percentage => amount is > 0 and <= 100,
            _ => false
        };
    }

    // Mirrors CK_ServiceTimePricingPolicies_*: positive rate/durations, and overtime fields are
    // either both set (with the threshold at or after the minimum duration) or both null.
    private static bool IsValidTimePricingPolicy(ServiceTimePricingPolicy policy)
    {
        if (policy.UnitPrice <= 0 || policy.MinimumBillableDurationMinutes <= 0 || policy.BillingIncrementMinutes <= 0)
        {
            return false;
        }

        var thresholdSet = policy.OvertimeThresholdMinutes.HasValue;
        var rateSet = policy.OvertimeUnitPrice.HasValue;
        if (thresholdSet != rateSet)
        {
            return false;
        }

        if (thresholdSet &&
            (policy.OvertimeThresholdMinutes < policy.MinimumBillableDurationMinutes || policy.OvertimeUnitPrice <= 0))
        {
            return false;
        }

        return true;
    }

    // Mirrors CK_ServiceSurcharges_TriggerFields: only the fields matching the trigger type may
    // be set; every other trigger-specific field must be null, so a surcharge cannot be
    // configured ambiguously.
    private static bool IsValidSurchargeTriggerFields(ServiceSurcharge surcharge)
    {
        return surcharge.TriggerType switch
        {
            SurchargeTriggerType.AfterHours =>
                surcharge.AfterHoursStartMinutes is >= 0 and <= 1439 &&
                surcharge.AfterHoursEndMinutes is >= 0 and <= 1439 &&
                surcharge.UrgentLeadTimeMinutes == null &&
                string.IsNullOrWhiteSpace(surcharge.LocationMatch),
            SurchargeTriggerType.Urgent =>
                surcharge.UrgentLeadTimeMinutes is > 0 &&
                surcharge.AfterHoursStartMinutes == null &&
                surcharge.AfterHoursEndMinutes == null &&
                string.IsNullOrWhiteSpace(surcharge.LocationMatch),
            SurchargeTriggerType.Location =>
                !string.IsNullOrWhiteSpace(surcharge.LocationMatch) &&
                surcharge.AfterHoursStartMinutes == null &&
                surcharge.AfterHoursEndMinutes == null &&
                surcharge.UrgentLeadTimeMinutes == null,
            SurchargeTriggerType.Weekend or SurchargeTriggerType.Holiday =>
                surcharge.AfterHoursStartMinutes == null &&
                surcharge.AfterHoursEndMinutes == null &&
                surcharge.UrgentLeadTimeMinutes == null &&
                string.IsNullOrWhiteSpace(surcharge.LocationMatch),
            _ => false
        };
    }

    // Clears any trigger-specific fields that do not apply to the surcharge's trigger type, so a
    // client cannot smuggle stale values through from a previous trigger-type edit.
    private static void NormalizeSurchargeTriggerFields(ServiceSurcharge surcharge)
    {
        if (surcharge.TriggerType != SurchargeTriggerType.AfterHours)
        {
            surcharge.AfterHoursStartMinutes = null;
            surcharge.AfterHoursEndMinutes = null;
        }
        if (surcharge.TriggerType != SurchargeTriggerType.Urgent)
        {
            surcharge.UrgentLeadTimeMinutes = null;
        }
        if (surcharge.TriggerType != SurchargeTriggerType.Location)
        {
            surcharge.LocationMatch = null;
        }
    }

    public async Task<IReadOnlyCollection<DateOnly>> GetHolidayDatesAsync()
    {
        return await _db.PublicHolidays
            .AsNoTracking()
            .Select(holiday => holiday.Date)
            .ToListAsync();
    }

    public async Task<decimal> GetTaxRatePercentageAsync()
    {
        var setting = await _db.SystemSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == PricingSettings.TaxRatePercentageKey);
        return PricingSettings.ParseTaxRatePercentage(setting?.Value);
    }

    public async Task<string> GetCurrencyCodeAsync()
    {
        var setting = await _db.SystemSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == PricingSettings.CurrencyCodeKey);
        return PricingSettings.ParseCurrencyCode(setting?.Value);
    }
}
