using System.ComponentModel.DataAnnotations;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.DTOs;

public sealed class PromotionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public PromotionDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public int? EligibleServiceId { get; set; }
    public int? UsageLimit { get; set; }
    public int TimesUsed { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class CreatePromotionRequest : IValidatableObject
{
    [Required]
    [StringLength(64, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]*$")]
    public string Code { get; set; } = null!;

    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string Name { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    public PromotionDiscountType DiscountType { get; set; } = PromotionDiscountType.Percentage;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal DiscountValue { get; set; }

    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }

    [Range(1, int.MaxValue)]
    public int? EligibleServiceId { get; set; }

    [Range(1, int.MaxValue)]
    public int? UsageLimit { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DiscountType == PromotionDiscountType.Percentage && DiscountValue > 100m)
        {
            yield return new ValidationResult(
                "Percentage discounts cannot exceed 100.",
                [nameof(DiscountValue)]);
        }

        if (EndsAt.HasValue && EndsAt.Value <= StartsAt)
        {
            yield return new ValidationResult(
                "Promotion end time must be after the start time.",
                [nameof(EndsAt)]);
        }
    }
}

public sealed class UpdatePromotionRequest : IValidatableObject
{
    [Required]
    [StringLength(64, MinimumLength = 2)]
    [RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]*$")]
    public string Code { get; set; } = null!;

    [Required]
    [StringLength(128, MinimumLength = 2)]
    public string Name { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    public PromotionDiscountType DiscountType { get; set; } = PromotionDiscountType.Percentage;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal DiscountValue { get; set; }

    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }

    [Range(1, int.MaxValue)]
    public int? EligibleServiceId { get; set; }

    [Range(1, int.MaxValue)]
    public int? UsageLimit { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DiscountType == PromotionDiscountType.Percentage && DiscountValue > 100m)
        {
            yield return new ValidationResult(
                "Percentage discounts cannot exceed 100.",
                [nameof(DiscountValue)]);
        }

        if (EndsAt.HasValue && EndsAt.Value <= StartsAt)
        {
            yield return new ValidationResult(
                "Promotion end time must be after the start time.",
                [nameof(EndsAt)]);
        }
    }
}
