using HouseManagement.Api.Infrastructure.Files;
using Microsoft.Extensions.Options;
using Xunit;

namespace HouseManagement.Api.Tests;

public class ProfileImageProcessorTests
{
    [Fact]
    public async Task ProcessAsync_AcceptsAllowedPngWhenMetadataMatchesSignature()
    {
        var processor = CreateProcessor();

        var processed = await processor.ProcessAsync(new ProfileImageUpload(
            "profile.PNG",
            "image/png",
            new MemoryStream(PngBytes())));

        Assert.Equal("image/png", processed.ContentType);
        Assert.Equal(".png", processed.Extension);
        Assert.Equal(PngBytes().Length, processed.SizeBytes);
        Assert.Equal(PngBytes(), processed.Content);
    }

    [Fact]
    public async Task ProcessAsync_RejectsDisallowedExtension()
    {
        var processor = CreateProcessor();

        var exception = await Assert.ThrowsAsync<ProfileImageValidationException>(() =>
            processor.ProcessAsync(new ProfileImageUpload(
                "profile.gif",
                "image/png",
                new MemoryStream(PngBytes()))));

        Assert.Contains("extension", exception.Errors.Keys);
    }

    [Fact]
    public async Task ProcessAsync_RejectsContentTypeThatDoesNotMatchSignature()
    {
        var processor = CreateProcessor();

        var exception = await Assert.ThrowsAsync<ProfileImageValidationException>(() =>
            processor.ProcessAsync(new ProfileImageUpload(
                "profile.jpg",
                "image/jpeg",
                new MemoryStream(PngBytes()))));

        Assert.Contains("contentType", exception.Errors.Keys);
        Assert.Contains("extension", exception.Errors.Keys);
    }

    [Fact]
    public async Task ProcessAsync_RejectsFilesAboveConfiguredLimit()
    {
        var processor = CreateProcessor(new ProfileImageOptions
        {
            MaxSizeBytes = 4
        });

        var exception = await Assert.ThrowsAsync<ProfileImageValidationException>(() =>
            processor.ProcessAsync(new ProfileImageUpload(
                "profile.png",
                "image/png",
                new MemoryStream(PngBytes()))));

        Assert.Contains("size", exception.Errors.Keys);
    }

    private static ProfileImageProcessor CreateProcessor(ProfileImageOptions? options = null)
    {
        return new ProfileImageProcessor(Options.Create(options ?? new ProfileImageOptions()));
    }

    private static byte[] PngBytes()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x00
        ];
    }
}
