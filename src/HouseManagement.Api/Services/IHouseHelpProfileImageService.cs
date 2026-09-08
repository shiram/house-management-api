using HouseManagement.Api.Infrastructure.Files;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public sealed record HouseHelpProfileImageUpdateResult(
    HouseHelp HouseHelp,
    bool ReplacedExisting);

public interface IHouseHelpProfileImageService
{
    Task<ProfileImageReadResult?> OpenPublicAsync(
        int houseHelpId,
        CancellationToken cancellationToken = default);

    Task<HouseHelpProfileImageUpdateResult?> ReplaceAsync(
        int houseHelpId,
        ProfileImageUpload upload,
        CancellationToken cancellationToken = default);
}
