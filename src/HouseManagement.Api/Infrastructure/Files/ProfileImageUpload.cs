namespace HouseManagement.Api.Infrastructure.Files;

public sealed record ProfileImageUpload(
    string FileName,
    string ContentType,
    Stream Content);

public sealed record ProcessedProfileImage(
    byte[] Content,
    string ContentType,
    string Extension,
    long SizeBytes);

public sealed record StoredProfileImage(
    string StorageKey,
    string ContentType,
    long SizeBytes);

public sealed record ProfileImageReadResult(
    Stream Content,
    string ContentType,
    long SizeBytes);
