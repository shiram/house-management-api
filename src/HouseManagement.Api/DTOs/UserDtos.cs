using System.ComponentModel.DataAnnotations;

namespace HouseManagement.Api.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}

public class UpdateUserRoleRequest
{
    [Required]
    public string Role { get; set; } = null!;
}
