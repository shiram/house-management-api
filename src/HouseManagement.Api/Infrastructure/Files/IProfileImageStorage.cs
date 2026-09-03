namespace HouseManagement.Api.Infrastructure.Files;

public interface IProfileImageStorage
{
    Task<StoredProfileImage> SaveAsync(
        int houseHelpId,
        ProcessedProfileImage image,
        CancellationToken cancellationToken = default);

    Task<ProfileImageReadResult> OpenReadAsync(
        string storageKey,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
