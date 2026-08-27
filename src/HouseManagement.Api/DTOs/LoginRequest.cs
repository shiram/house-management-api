using System.ComponentModel.DataAnnotations;

namespace HouseManagement.Api.DTOs;

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string Password { get; set; } = null!;
}