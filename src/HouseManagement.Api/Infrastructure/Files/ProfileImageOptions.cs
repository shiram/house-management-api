namespace HouseManagement.Api.Infrastructure.Files;

public sealed class ProfileImageOptions
{
    public const long HardMaxSizeBytes = 5 * 1024 * 1024;
    public const long HardMaxMultipartBodyBytes = HardMaxSizeBytes + 64 * 1024;

    public long MaxSizeBytes { get; set; } = 2 * 1024 * 1024;
    public int MaxWidth { get; set; } = 4096;
    public int MaxHeight { get; set; } = 4096;
    public string[] AllowedContentTypes { get; set; } = ["image/jpeg", "image/png", "image/webp"];
    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp"];
    public string StorageRootPath { get; set; } = Path.Combine("App_Data", "ProfileImages");
}
