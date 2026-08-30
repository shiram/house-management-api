using System.ComponentModel.DataAnnotations;

namespace HouseManagement.Api.DTOs;

public class SystemSettingDto
{
    public int Id { get; set; }
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class UpsertSystemSettingRequest
{
    [Required]
    [StringLength(2000)]
    public string Value { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }
}
