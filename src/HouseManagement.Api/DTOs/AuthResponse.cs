namespace HouseManagement.Api.DTOs;

public class AuthResponse
{
    public string Token { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
}