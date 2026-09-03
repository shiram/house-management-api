using HouseManagement.Api.Infrastructure.Files;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace HouseManagement.Api.Tests;

public class LocalProfileImageStorageTests
{
    [Fact]
    public async Task SaveAsync_GeneratesSafeStorageKeyAndPersistsContent()
    {
        var root = CreateTempDirectory();
        var storage = CreateStorage(root);
        var image = new ProcessedProfileImage([0x01, 0x02, 0x03], "image/png", ".png", 3);

        var stored = await storage.SaveAsync(42, image);

        Assert.StartsWith("househelps/42/", stored.StorageKey, StringComparison.Ordinal);
        Assert.EndsWith(".png", stored.StorageKey, StringComparison.Ordinal);
        Assert.DoesNotContain("..", stored.StorageKey, StringComparison.Ordinal);
        Assert.Equal("image/png", stored.ContentType);
        Assert.Equal(3, stored.SizeBytes);

        var path = Path.Combine(root, Path.Combine(stored.StorageKey.Split('/')));
        Assert.True(File.Exists(path));
        Assert.Equal(image.Content, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsStoredImageContent()
    {
        var root = CreateTempDirectory();
        var storage = CreateStorage(root);
        var stored = await storage.SaveAsync(
            7,
            new ProcessedProfileImage([0x0A, 0x0B], "image/jpeg", ".jpg", 2));

        await using var content = (await storage.OpenReadAsync(stored.StorageKey, stored.ContentType)).Content;
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory);

        Assert.Equal(new byte[] { 0x0A, 0x0B }, memory.ToArray());
    }

    [Fact]
    public async Task DeleteAsync_RemovesStoredImage()
    {
        var root = CreateTempDirectory();
        var storage = CreateStorage(root);
        var stored = await storage.SaveAsync(
            7,
            new ProcessedProfileImage([0x0A, 0x0B], "image/jpeg", ".jpg", 2));
        var path = Path.Combine(root, Path.Combine(stored.StorageKey.Split('/')));

        await storage.DeleteAsync(stored.StorageKey);

        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("househelps/1/../../outside.png")]
    public async Task OpenReadAsync_RejectsUnsafeStorageKeys(string storageKey)
    {
        var storage = CreateStorage(CreateTempDirectory());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.OpenReadAsync(storageKey, "image/png"));
    }

    private static LocalProfileImageStorage CreateStorage(string rootPath)
    {
        return new LocalProfileImageStorage(
            Options.Create(new ProfileImageOptions { StorageRootPath = rootPath }),
            new TestWebHostEnvironment());
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "house-profile-images", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HouseManagement.Api.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
