using Microsoft.Extensions.Options;

namespace HouseManagement.Api.Infrastructure.Files;

public sealed class LocalProfileImageStorage : IProfileImageStorage
{
    private readonly ProfileImageOptions _options;
    private readonly IWebHostEnvironment _environment;

    public LocalProfileImageStorage(
        IOptions<ProfileImageOptions> options,
        IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public async Task<StoredProfileImage> SaveAsync(
        int houseHelpId,
        ProcessedProfileImage image,
        CancellationToken cancellationToken = default)
    {
        if (houseHelpId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(houseHelpId), "HouseHelp id must be greater than zero.");
        }

        var storageKey = $"househelps/{houseHelpId}/{Guid.NewGuid():N}{image.Extension}";
        var physicalPath = GetPhysicalPath(storageKey);
        var directory = Path.GetDirectoryName(physicalPath)
            ?? throw new InvalidOperationException("Profile image storage path is invalid.");

        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(physicalPath, image.Content, cancellationToken);

        return new StoredProfileImage(storageKey, image.ContentType, image.SizeBytes);
    }

    public Task<ProfileImageReadResult> OpenReadAsync(
        string storageKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var physicalPath = GetPhysicalPath(storageKey);
        if (!File.Exists(physicalPath))
        {
            throw new FileNotFoundException("Profile image was not found.", storageKey);
        }

        Stream content = File.OpenRead(physicalPath);
        return Task.FromResult(new ProfileImageReadResult(content, contentType, content.Length));
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var physicalPath = GetPhysicalPath(storageKey);
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }

    private string GetPhysicalPath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) ||
            storageKey.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(storageKey))
        {
            throw new ArgumentException("Storage key must be a relative generated key.", nameof(storageKey));
        }

        var root = _options.StorageRootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("Profile image storage root path is not configured.");
        }

        var rootPath = Path.IsPathRooted(root)
            ? root
            : Path.Combine(_environment.ContentRootPath, root);
        var combinedPath = Path.GetFullPath(Path.Combine(rootPath, Path.Combine(storageKey.Split('/'))));
        var fullRoot = Path.GetFullPath(rootPath);
        var rootWithSeparator = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        if (!combinedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Storage key resolves outside the configured storage root.", nameof(storageKey));
        }

        return combinedPath;
    }
}
