using Microsoft.Extensions.Options;
using SkiaSharp;

namespace HouseManagement.Api.Infrastructure.Files;

public sealed class ProfileImageProcessor : IProfileImageProcessor
{
    private readonly ProfileImageOptions _options;

    public ProfileImageProcessor(IOptions<ProfileImageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ProcessedProfileImage> ProcessAsync(
        ProfileImageUpload upload,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(upload.FileName))
        {
            errors["fileName"] = ["A file name is required."];
        }

        if (string.IsNullOrWhiteSpace(upload.ContentType))
        {
            errors["contentType"] = ["A content type is required."];
        }

        if (upload.Content == Stream.Null)
        {
            errors["content"] = ["A file is required."];
        }

        ThrowIfInvalid(errors);

        var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
        if (!_options.AllowedExtensions.Any(allowed => string.Equals(allowed, extension, StringComparison.OrdinalIgnoreCase)))
        {
            errors["extension"] = ["The image extension is not allowed."];
        }

        var contentType = upload.ContentType.Trim().ToLowerInvariant();
        if (!_options.AllowedContentTypes.Any(allowed => string.Equals(allowed, contentType, StringComparison.OrdinalIgnoreCase)))
        {
            errors["contentType"] = ["The image content type is not allowed."];
        }

        var content = await ReadContentAsync(upload.Content, cancellationToken);
        if (content.Length == 0)
        {
            errors["content"] = ["The image file is empty."];
        }

        var maximumSize = Math.Min(
            Math.Max(_options.MaxSizeBytes, 0),
            ProfileImageOptions.HardMaxSizeBytes);
        if (content.Length > maximumSize)
        {
            errors["size"] = [$"The image file must be {maximumSize} bytes or smaller."];
        }

        ThrowIfInvalid(errors);

        using var encodedStream = new SKMemoryStream(content);
        using var codec = SKCodec.Create(encodedStream);
        if (codec == null)
        {
            errors["content"] = ["The image file is malformed or its format is not supported."];
            throw new ProfileImageValidationException(errors);
        }

        var detectedContentType = ContentTypeFor(codec.EncodedFormat);
        if (detectedContentType == null)
        {
            errors["content"] = ["The image file format is not supported."];
        }

        if (!string.Equals(detectedContentType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            errors["contentType"] = ["The image content type does not match the file content."];
        }

        if (!AllowedExtensionsFor(detectedContentType).Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            errors["extension"] = ["The image extension does not match the file content."];
        }

        if (codec.Info.Width <= 0 ||
            codec.Info.Height <= 0 ||
            codec.Info.Width > _options.MaxWidth ||
            codec.Info.Height > _options.MaxHeight)
        {
            errors["dimensions"] = [$"The image dimensions must not exceed {_options.MaxWidth}x{_options.MaxHeight} pixels."];
        }

        if (codec.FrameCount > 1)
        {
            errors["frames"] = ["Animated or multi-frame profile images are not allowed."];
        }

        ThrowIfInvalid(errors);

        using var bitmap = new SKBitmap(codec.Info);
        var decodeResult = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (decodeResult != SKCodecResult.Success)
        {
            errors["content"] = ["The image file could not be decoded safely."];
            throw new ProfileImageValidationException(errors);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var normalized = image.Encode(EncodedFormatFor(detectedContentType!), 85);
        if (normalized == null)
        {
            errors["content"] = ["The image file could not be processed safely."];
            throw new ProfileImageValidationException(errors);
        }

        var normalizedContent = normalized.ToArray();
        if (normalizedContent.Length > maximumSize)
        {
            errors["size"] = [$"The processed image must be {maximumSize} bytes or smaller."];
            ThrowIfInvalid(errors);
        }

        return new ProcessedProfileImage(
            normalizedContent,
            detectedContentType!,
            extension,
            normalizedContent.Length);
    }

    private async Task<byte[]> ReadContentAsync(Stream content, CancellationToken cancellationToken)
    {
        var configuredMaximum = _options.MaxSizeBytes <= 0 ? 0 : _options.MaxSizeBytes;
        var effectiveMaximum = Math.Min(configuredMaximum, ProfileImageOptions.HardMaxSizeBytes);
        var maximumBytesToRead = effectiveMaximum + 1;
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        while (memory.Length <= maximumBytesToRead)
        {
            var read = await content.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            var remaining = maximumBytesToRead - memory.Length;
            var bytesToWrite = (int)Math.Min(read, remaining);
            await memory.WriteAsync(buffer.AsMemory(0, bytesToWrite), cancellationToken);
            if (bytesToWrite < read)
            {
                break;
            }
        }

        return memory.ToArray();
    }

    private static string? ContentTypeFor(SKEncodedImageFormat format)
    {
        return format switch
        {
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Webp => "image/webp",
            _ => null
        };
    }

    private static SKEncodedImageFormat EncodedFormatFor(string contentType)
    {
        return contentType switch
        {
            "image/jpeg" => SKEncodedImageFormat.Jpeg,
            "image/png" => SKEncodedImageFormat.Png,
            "image/webp" => SKEncodedImageFormat.Webp,
            _ => throw new InvalidOperationException("The detected image type is not supported.")
        };
    }

    private static string[] AllowedExtensionsFor(string? contentType)
    {
        return contentType switch
        {
            "image/jpeg" => [".jpg", ".jpeg"],
            "image/png" => [".png"],
            "image/webp" => [".webp"],
            _ => []
        };
    }

    private static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new ProfileImageValidationException(errors);
        }
    }
}
