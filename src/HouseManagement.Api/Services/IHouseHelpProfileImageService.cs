using HouseManagement.Api.Infrastructure.Files;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IHouseHelpProfileImageService
{
    Task<HouseHelp?> ReplaceAsync(
        int houseHelpId,
        ProfileImageUpload upload,
        CancellationToken cancellationToken = default);
}
