using Microsoft.Extensions.Options;

namespace HouseManagement.Api.Infrastructure.Files;

public sealed class ProfileImageProcessor : IProfileImageProcessor
{
    private const int SignatureProbeLength = 12;
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

        if (content.Length > _options.MaxSizeBytes)
        {
            errors["size"] = [$"The image file must be {_options.MaxSizeBytes} bytes or smaller."];
        }

        var detected = DetectImageType(content);
        if (detected == null)
        {
            errors["content"] = ["The image file signature is not supported."];
        }
        else
        {
            if (!string.Equals(detected.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
            {
                errors["contentType"] = ["The image content type does not match the file content."];
            }

            if (!detected.Extensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase)))
            {
                errors["extension"] = ["The image extension does not match the file content."];
            }
        }

        ThrowIfInvalid(errors);

        return new ProcessedProfileImage(
            content,
            detected!.ContentType,
            extension,
            content.Length);
    }

    private async Task<byte[]> ReadContentAsync(Stream content, CancellationToken cancellationToken)
    {
        var maximumBytesToRead = _options.MaxSizeBytes <= 0 ? 1 : _options.MaxSizeBytes + 1;
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

    private static DetectedImageType? DetectImageType(byte[] content)
    {
        if (content.Length >= 3 &&
            content[0] == 0xFF &&
            content[1] == 0xD8 &&
            content[2] == 0xFF)
        {
            return new DetectedImageType("image/jpeg", [".jpg", ".jpeg"]);
        }

        if (content.Length >= 8 &&
            content[0] == 0x89 &&
            content[1] == 0x50 &&
            content[2] == 0x4E &&
            content[3] == 0x47 &&
            content[4] == 0x0D &&
            content[5] == 0x0A &&
            content[6] == 0x1A &&
            content[7] == 0x0A)
        {
            return new DetectedImageType("image/png", [".png"]);
        }

        if (content.Length >= SignatureProbeLength &&
            content[0] == 0x52 &&
            content[1] == 0x49 &&
            content[2] == 0x46 &&
            content[3] == 0x46 &&
            content[8] == 0x57 &&
            content[9] == 0x45 &&
            content[10] == 0x42 &&
            content[11] == 0x50)
        {
            return new DetectedImageType("image/webp", [".webp"]);
        }

        return null;
    }

    private static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new ProfileImageValidationException(errors);
        }
    }

    private sealed record DetectedImageType(string ContentType, string[] Extensions);
}
