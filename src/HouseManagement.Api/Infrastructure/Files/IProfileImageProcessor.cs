namespace HouseManagement.Api.Infrastructure.Files;

public interface IProfileImageProcessor
{
    Task<ProcessedProfileImage> ProcessAsync(
        ProfileImageUpload upload,
        CancellationToken cancellationToken = default);
}
