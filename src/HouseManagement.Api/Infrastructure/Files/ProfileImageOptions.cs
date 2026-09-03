namespace HouseManagement.Api.Infrastructure.Files;

public sealed class ProfileImageOptions
{
    public long MaxSizeBytes { get; set; } = 2 * 1024 * 1024;
    public string[] AllowedContentTypes { get; set; } = ["image/jpeg", "image/png", "image/webp"];
    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp"];
    public string StorageRootPath { get; set; } = Path.Combine("App_Data", "ProfileImages");
}
