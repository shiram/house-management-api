using HouseManagement.Api.Infrastructure.Files;
using Microsoft.Extensions.Options;
using SkiaSharp;
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
        Assert.Equal(processed.Content.Length, processed.SizeBytes);
        using var decoded = SKBitmap.Decode(processed.Content);
        Assert.NotNull(decoded);
        Assert.Equal(1, decoded.Width);
        Assert.Equal(1, decoded.Height);
    }

    [Theory]
    [InlineData("profile.jpg", "image/jpeg", SKEncodedImageFormat.Jpeg)]
    [InlineData("profile.webp", "image/webp", SKEncodedImageFormat.Webp)]
    public async Task ProcessAsync_AcceptsAndNormalizesOtherAllowedFormats(
        string fileName,
        string contentType,
        SKEncodedImageFormat format)
    {
        var processor = CreateProcessor();

        var processed = await processor.ProcessAsync(new ProfileImageUpload(
            fileName,
            contentType,
            new MemoryStream(ImageBytes(format))));

        Assert.Equal(contentType, processed.ContentType);
        Assert.Equal(Path.GetExtension(fileName), processed.Extension);
        Assert.True(processed.SizeBytes > 0);
        using var encodedStream = new SKMemoryStream(processed.Content);
        using var codec = SKCodec.Create(encodedStream);
        Assert.NotNull(codec);
        Assert.Equal(format, codec.EncodedFormat);
        using var decoded = SKBitmap.Decode(processed.Content);
        Assert.NotNull(decoded);
        Assert.Equal(1, decoded.Width);
        Assert.Equal(1, decoded.Height);
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
    public async Task ProcessAsync_RejectsEmptyFile()
    {
        var processor = CreateProcessor();

        var exception = await Assert.ThrowsAsync<ProfileImageValidationException>(() =>
            processor.ProcessAsync(new ProfileImageUpload(
                "profile.png",
                "image/png",
                new MemoryStream())));

        Assert.Contains("content", exception.Errors.Keys);
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
    public async Task ProcessAsync_RejectsTruncatedImageWithValidSignature()
    {
        var processor = CreateProcessor();

        var exception = await Assert.ThrowsAsync<ProfileImageValidationException>(() =>
            processor.ProcessAsync(new ProfileImageUpload(
                "profile.png",
                "image/png",
                new MemoryStream(
                [
                    0x89, 0x50, 0x4E, 0x47,
                    0x0D, 0x0A, 0x1A, 0x0A
                ]))));

        Assert.Contains("content", exception.Errors.Keys);
    }

    [Fact]
    public async Task ProcessAsync_RejectsImageAboveConfiguredDimensions()
    {
        var processor = CreateProcessor(new ProfileImageOptions
        {
            MaxWidth = 1,
            MaxHeight = 1
        });

        var exception = await Assert.ThrowsAsync<ProfileImageValidationException>(() =>
            processor.ProcessAsync(new ProfileImageUpload(
                "profile.png",
                "image/png",
                new MemoryStream(PngBytes(2, 1)))));

        Assert.Contains("dimensions", exception.Errors.Keys);
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

    [Fact]
    public async Task ProcessAsync_EnforcesHardLimitWhenConfiguredLimitIsHigher()
    {
        var processor = CreateProcessor(new ProfileImageOptions
        {
            MaxSizeBytes = ProfileImageOptions.HardMaxSizeBytes + 1024
        });
        var content = new byte[ProfileImageOptions.HardMaxSizeBytes + 1];
        Array.Copy(PngBytes(), content, PngBytes().Length);

        var exception = await Assert.ThrowsAsync<ProfileImageValidationException>(() =>
            processor.ProcessAsync(new ProfileImageUpload(
                "profile.png",
                "image/png",
                new MemoryStream(content))));

        Assert.Contains("size", exception.Errors.Keys);
    }

    private static ProfileImageProcessor CreateProcessor(ProfileImageOptions? options = null)
    {
        return new ProfileImageProcessor(Options.Create(options ?? new ProfileImageOptions()));
    }

    private static byte[] PngBytes(int width = 1, int height = 1)
    {
        return ImageBytes(SKEncodedImageFormat.Png, width, height);
    }

    private static byte[] ImageBytes(
        SKEncodedImageFormat format,
        int width = 1,
        int height = 1)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var content = image.Encode(format, 100);
        return content.ToArray();
    }
}
