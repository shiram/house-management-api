using HouseManagement.Api.Common;
using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class PromotionService : IPromotionService
{
    private readonly HouseContext _db;

    public PromotionService(HouseContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Promotion>> GetAllAsync(int? page = null, int? pageSize = null, bool? isActive = null)
    {
        var query = _db.Promotions.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(promotion => promotion.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(promotion => promotion.Code)
            .ApplyPagination(page, pageSize)
            .ToListAsync();
    }

    public async Task<Promotion?> GetByIdAsync(int id)
    {
        return await _db.Promotions
            .AsNoTracking()
            .SingleOrDefaultAsync(promotion => promotion.Id == id);
    }

    public async Task<bool> CodeExistsAsync(string code, int? excludingId = null)
    {
        var normalizedCode = NormalizeCode(code);
        return await _db.Promotions.AnyAsync(promotion =>
            promotion.Code == normalizedCode && (!excludingId.HasValue || promotion.Id != excludingId.Value));
    }

    public async Task<PromotionCreateResult> CreateAsync(Promotion promotion)
    {
        Normalize(promotion);

        var error = await ValidateAsync(promotion);
        if (error != null)
        {
            return new PromotionCreateResult(null, error);
        }

        if (await CodeExistsAsync(promotion.Code))
        {
            return new PromotionCreateResult(null, "A promotion with this code already exists.");
        }

        _db.Promotions.Add(promotion);
        await _db.SaveChangesAsync();
        return new PromotionCreateResult(promotion, null);
    }

    public async Task<PromotionUpdateResult> UpdateAsync(Promotion promotion)
    {
        var existing = await _db.Promotions.SingleOrDefaultAsync(item => item.Id == promotion.Id);
        if (existing == null)
        {
            return new PromotionUpdateResult(false, null);
        }

        Normalize(promotion);

        var error = await ValidateAsync(promotion);
        if (error != null)
        {
            return new PromotionUpdateResult(true, error);
        }

        if (await CodeExistsAsync(promotion.Code, promotion.Id))
        {
            return new PromotionUpdateResult(true, "A promotion with this code already exists.");
        }

        existing.Code = promotion.Code;
        existing.Name = promotion.Name;
        existing.Description = promotion.Description;
        existing.DiscountType = promotion.DiscountType;
        existing.DiscountValue = promotion.DiscountValue;
        existing.StartsAt = promotion.StartsAt;
        existing.EndsAt = promotion.EndsAt;
        existing.EligibleServiceId = promotion.EligibleServiceId;
        existing.UsageLimit = promotion.UsageLimit;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return new PromotionUpdateResult(true, null);
    }

    public async Task<bool> SetActiveAsync(int id, bool isActive)
    {
        var existing = await _db.Promotions.SingleOrDefaultAsync(item => item.Id == id);
        if (existing == null)
        {
            return false;
        }

        existing.IsActive = isActive;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<string?> ValidateAsync(Promotion promotion)
    {
        if (promotion.DiscountType == PromotionDiscountType.Percentage && promotion.DiscountValue > 100m)
        {
            return "Percentage discounts cannot exceed 100.";
        }

        if (promotion.EndsAt.HasValue && promotion.EndsAt.Value <= promotion.StartsAt)
        {
            return "Promotion end time must be after the start time.";
        }

        if (promotion.EligibleServiceId.HasValue)
        {
            var serviceExists = await _db.Services.AnyAsync(service => service.Id == promotion.EligibleServiceId.Value);
            if (!serviceExists)
            {
                return "The eligible service was not found.";
            }
        }

        return null;
    }

    private static void Normalize(Promotion promotion)
    {
        promotion.Code = NormalizeCode(promotion.Code);
        promotion.Name = promotion.Name.Trim();
        promotion.Description = string.IsNullOrWhiteSpace(promotion.Description) ? null : promotion.Description.Trim();
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }
}
