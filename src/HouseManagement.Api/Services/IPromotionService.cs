using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IPromotionService
{
    Task<IEnumerable<Promotion>> GetAllAsync(int? page = null, int? pageSize = null, bool? isActive = null);
    Task<Promotion?> GetByIdAsync(int id);
    Task<bool> CodeExistsAsync(string code, int? excludingId = null);
    Task<PromotionCreateResult> CreateAsync(Promotion promotion);
    Task<PromotionUpdateResult> UpdateAsync(Promotion promotion);
    Task<bool> SetActiveAsync(int id, bool isActive);
}

public sealed record PromotionCreateResult(Promotion? Promotion, string? Error);
public sealed record PromotionUpdateResult(bool Exists, string? Error);
