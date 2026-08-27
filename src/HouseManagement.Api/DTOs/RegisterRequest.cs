using System.ComponentModel.DataAnnotations;

namespace HouseManagement.Api.DTOs;

public class RegisterRequest
{
    [Required]
    [StringLength(100)]
    public string UserName { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = null!;
}