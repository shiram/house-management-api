using System.ComponentModel.DataAnnotations;

namespace HouseManagement.Api.DTOs;

public sealed class ServiceDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class CreateServiceRequest
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

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal BasePrice { get; set; }
}

public sealed class UpdateServiceRequest
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

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal BasePrice { get; set; }
}
