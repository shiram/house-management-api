using System.Data;
using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class ServicePricingVersionService : IServicePricingVersionService
{
    private readonly HouseContext _db;

    public ServicePricingVersionService(HouseContext db)
    {
        _db = db;
    }

    public async Task<ServicePricingVersion?> CreateDraftAsync(int serviceId, ServicePricingVersion draft)
    {
        var service = await _db.Services.SingleOrDefaultAsync(item => item.Id == serviceId);
        if (service == null)
        {
            return null;
        }

        if (draft.EffectiveFrom == default)
        {
            return null;
        }

        var normalized = NormalizeForMode(service.PricingMode, draft);
        if (normalized == null)
        {
            return null;
        }

        normalized.ServiceId = serviceId;
        normalized.Status = PricingVersionStatus.Draft;
        normalized.PublishedAt = null;
        normalized.CreatedAt = DateTimeOffset.UtcNow;

        _db.ServicePricingVersions.Add(normalized);
        await _db.SaveChangesAsync();
        return normalized;
    }

    public async Task<PricingVersionPublishResult> PublishAsync(int versionId)
    {
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? efTransaction = null;
        if (_db.Database.IsRelational())
        {
            efTransaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        }

        try
        {
            var version = await _db.ServicePricingVersions
                .SingleOrDefaultAsync(item => item.Id == versionId);
            if (version == null)
            {
                return new PricingVersionPublishResult(null, "The requested pricing version was not found.");
            }

            if (version.Status != PricingVersionStatus.Draft)
            {
                return new PricingVersionPublishResult(null, "Only a draft pricing version can be published.");
            }

            var openVersion = await _db.ServicePricingVersions
                .SingleOrDefaultAsync(item =>
                    item.ServiceId == version.ServiceId &&
                    item.Status == PricingVersionStatus.Published &&
                    item.EffectiveTo == null);

            if (openVersion != null)
            {
                if (openVersion.EffectiveFrom >= version.EffectiveFrom)
                {
                    return new PricingVersionPublishResult(
                        null,
                        "The new pricing version must take effect after the currently open published version.");
                }

                openVersion.EffectiveTo = version.EffectiveFrom;
            }

            version.Status = PricingVersionStatus.Published;
            version.PublishedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            if (efTransaction != null)
            {
                await efTransaction.CommitAsync();
            }

            return new PricingVersionPublishResult(version, null);
        }
        catch
        {
            if (efTransaction != null)
            {
                await efTransaction.RollbackAsync();
            }
            throw;
        }
        finally
        {
            if (efTransaction != null)
            {
                await efTransaction.DisposeAsync();
            }
        }
    }

    public async Task<ServicePricingVersion?> GetEffectiveVersionAsync(int serviceId, DateTimeOffset atUtc)
    {
        return await _db.ServicePricingVersions
            .AsNoTracking()
            .Include(version => version.Units)
            .Where(version =>
                version.ServiceId == serviceId &&
                version.Status == PricingVersionStatus.Published &&
                version.EffectiveFrom <= atUtc &&
                (version.EffectiveTo == null || version.EffectiveTo > atUtc))
            .OrderByDescending(version => version.EffectiveFrom)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<ServicePricingVersion>> GetVersionsAsync(int serviceId)
    {
        return await _db.ServicePricingVersions
            .AsNoTracking()
            .Include(version => version.Units)
            .Where(version => version.ServiceId == serviceId)
            .OrderByDescending(version => version.EffectiveFrom)
            .ToListAsync();
    }

    private static ServicePricingVersion? NormalizeForMode(ServicePricingMode mode, ServicePricingVersion draft)
    {
        var normalized = new ServicePricingVersion
        {
            EffectiveFrom = draft.EffectiveFrom,
            EffectiveTo = draft.EffectiveTo,
            PricingMode = mode
        };

        switch (mode)
        {
            case ServicePricingMode.Fixed:
                if (draft.BasePrice is not > 0)
                {
                    return null;
                }
                normalized.BasePrice = draft.BasePrice;
                return normalized;

            case ServicePricingMode.PerUnit:
                if (draft.Units.Count == 0)
                {
                    return null;
                }

                var units = new List<ServicePricingVersionUnit>();
                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var unit in draft.Units)
                {
                    var unitName = unit.UnitName?.Trim();
                    if (string.IsNullOrWhiteSpace(unitName) || unit.UnitPrice <= 0)
                    {
                        return null;
                    }
                    if (!seenNames.Add(unitName))
                    {
                        return null;
                    }
                    units.Add(new ServicePricingVersionUnit { UnitName = unitName, UnitPrice = unit.UnitPrice });
                }
                normalized.Units = units;
                return normalized;

            case ServicePricingMode.TimeBased:
                if (draft.TimeUnitPrice is not > 0 ||
                    draft.MinimumBillableDurationMinutes is not > 0 ||
                    draft.BillingIncrementMinutes is not > 0 ||
                    draft.TimeBillingUnit == null ||
                    draft.TimeRoundingPolicy == null)
                {
                    return null;
                }

                var overtimeThresholdSet = draft.OvertimeThresholdMinutes.HasValue;
                var overtimeRateSet = draft.OvertimeUnitPrice.HasValue;
                if (overtimeThresholdSet != overtimeRateSet)
                {
                    return null;
                }
                if (overtimeThresholdSet &&
                    (draft.OvertimeThresholdMinutes < draft.MinimumBillableDurationMinutes || draft.OvertimeUnitPrice <= 0))
                {
                    return null;
                }

                normalized.TimeBillingUnit = draft.TimeBillingUnit;
                normalized.TimeUnitPrice = draft.TimeUnitPrice;
                normalized.MinimumBillableDurationMinutes = draft.MinimumBillableDurationMinutes;
                normalized.BillingIncrementMinutes = draft.BillingIncrementMinutes;
                normalized.TimeRoundingPolicy = draft.TimeRoundingPolicy;
                normalized.OvertimeThresholdMinutes = draft.OvertimeThresholdMinutes;
                normalized.OvertimeUnitPrice = draft.OvertimeUnitPrice;
                return normalized;

            default:
                return null;
        }
    }
}
