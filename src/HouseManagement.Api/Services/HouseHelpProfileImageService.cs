using System.Data;
using HouseManagement.Api.Data;
using HouseManagement.Api.Infrastructure.Files;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseManagement.Api.Services;

public sealed class HouseHelpProfileImageService : IHouseHelpProfileImageService
{
    private readonly HouseContext _db;
    private readonly IProfileImageProcessor _processor;
    private readonly IProfileImageStorage _storage;
    private readonly ILogger<HouseHelpProfileImageService> _logger;

    public HouseHelpProfileImageService(
        HouseContext db,
        IProfileImageProcessor processor,
        IProfileImageStorage storage,
        ILogger<HouseHelpProfileImageService> logger)
    {
        _db = db;
        _processor = processor;
        _storage = storage;
        _logger = logger;
    }

    public async Task<ProfileImageReadResult?> OpenPublicAsync(
        int houseHelpId,
        CancellationToken cancellationToken = default)
    {
        var image = await _db.HouseHelps
            .AsNoTracking()
            .Where(item =>
                item.Id == houseHelpId &&
                item.IsActive &&
                item.ProfileImageStorageKey != null &&
                item.ProfileImageContentType != null)
            .Select(item => new
            {
                item.ProfileImageStorageKey,
                item.ProfileImageContentType
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (image == null)
        {
            return null;
        }

        try
        {
            return await _storage.OpenReadAsync(
                image.ProfileImageStorageKey!,
                image.ProfileImageContentType!,
                cancellationToken);
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning(
                "Stored profile image is missing for HouseHelp {HouseHelpId}",
                houseHelpId);
            return null;
        }
    }

    public async Task<HouseHelp?> ReplaceAsync(
        int houseHelpId,
        ProfileImageUpload upload,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.HouseHelps
            .AsNoTracking()
            .AnyAsync(item => item.Id == houseHelpId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var processed = await _processor.ProcessAsync(upload, cancellationToken);
        var stored = await _storage.SaveAsync(houseHelpId, processed, cancellationToken);
        var persisted = false;
        HouseHelp? houseHelp = null;
        string? previousStorageKey = null;

        try
        {
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                : null;

            houseHelp = _db.Database.IsSqlServer()
                ? await _db.HouseHelps
                    .FromSqlInterpolated($"SELECT * FROM HouseHelps WITH (UPDLOCK, ROWLOCK) WHERE Id = {houseHelpId}")
                    .Include(item => item.Skills)
                    .SingleOrDefaultAsync(cancellationToken)
                : await _db.HouseHelps
                    .Include(item => item.Skills)
                    .SingleOrDefaultAsync(item => item.Id == houseHelpId, cancellationToken);
            if (houseHelp == null)
            {
                return null;
            }

            previousStorageKey = houseHelp.ProfileImageStorageKey;
            houseHelp.ProfileImageStorageKey = stored.StorageKey;
            houseHelp.ProfileImageContentType = stored.ContentType;
            houseHelp.ProfileImageSizeBytes = stored.SizeBytes;
            houseHelp.ProfileImageUpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            persisted = true;
        }
        finally
        {
            if (!persisted)
            {
                await DeleteBestEffortAsync(stored.StorageKey);
            }
        }

        if (!string.IsNullOrWhiteSpace(previousStorageKey))
        {
            await DeleteBestEffortAsync(previousStorageKey);
        }

        return houseHelp;
    }

    private async Task DeleteBestEffortAsync(string storageKey)
    {
        try
        {
            await _storage.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch (IOException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not remove a profile image during replacement cleanup");
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not remove a profile image during replacement cleanup");
        }
    }
}
