using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface ITokenService
{
    string CreateToken(User user);
}