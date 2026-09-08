using HouseManagement.Api.Data;
using HouseManagement.Api.Infrastructure.Files;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace HouseManagement.Api.Tests;

public class HouseHelpProfileImageServiceTests
{
    [Fact]
    public async Task ReplaceAsync_ReturnsNullWithoutProcessingWhenHouseHelpIsMissing()
    {
        await using var context = CreateContext();
        var processor = new Mock<IProfileImageProcessor>();
        var storage = new Mock<IProfileImageStorage>();
        var service = CreateService(context, processor, storage);

        var result = await service.ReplaceAsync(
            999,
            new ProfileImageUpload("profile.png", "image/png", Stream.Null));

        Assert.Null(result);
        processor.Verify(
            item => item.ProcessAsync(It.IsAny<ProfileImageUpload>(), It.IsAny<CancellationToken>()),
            Times.Never);
        storage.Verify(
            item => item.SaveAsync(It.IsAny<int>(), It.IsAny<ProcessedProfileImage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReplaceAsync_PersistsNewMetadataAndReportsUpload()
    {
        await using var context = CreateContext();
        var houseHelp = CreateHouseHelp();
        context.HouseHelps.Add(houseHelp);
        await context.SaveChangesAsync();

        var processor = CreateProcessorMock();
        var storage = new Mock<IProfileImageStorage>();
        storage.Setup(item => item.SaveAsync(
                houseHelp.Id,
                It.IsAny<ProcessedProfileImage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredProfileImage(
                $"househelps/{houseHelp.Id}/new.png",
                "image/png",
                3));
        var service = CreateService(context, processor, storage);

        var result = await service.ReplaceAsync(
            houseHelp.Id,
            new ProfileImageUpload("profile.png", "image/png", new MemoryStream([0x01])));

        Assert.NotNull(result);
        Assert.False(result!.ReplacedExisting);
        context.ChangeTracker.Clear();
        var persisted = await context.HouseHelps.AsNoTracking()
            .SingleAsync(item => item.Id == houseHelp.Id);
        Assert.Equal($"househelps/{houseHelp.Id}/new.png", persisted.ProfileImageStorageKey);
        Assert.Equal("image/png", persisted.ProfileImageContentType);
        Assert.Equal(3, persisted.ProfileImageSizeBytes);
        Assert.NotNull(persisted.ProfileImageUpdatedAt);
        storage.Verify(
            item => item.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReplaceAsync_ReportsReplacementAndDeletesPreviousImage()
    {
        await using var context = CreateContext();
        var houseHelp = CreateHouseHelp();
        houseHelp.ProfileImageStorageKey = "househelps/1/old.png";
        houseHelp.ProfileImageContentType = "image/png";
        houseHelp.ProfileImageSizeBytes = 2;
        context.HouseHelps.Add(houseHelp);
        await context.SaveChangesAsync();

        var processor = CreateProcessorMock();
        var storage = new Mock<IProfileImageStorage>();
        storage.Setup(item => item.SaveAsync(
                houseHelp.Id,
                It.IsAny<ProcessedProfileImage>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredProfileImage(
                $"househelps/{houseHelp.Id}/new.png",
                "image/png",
                3));
        var service = CreateService(context, processor, storage);

        var result = await service.ReplaceAsync(
            houseHelp.Id,
            new ProfileImageUpload("profile.png", "image/png", new MemoryStream([0x01])));

        Assert.NotNull(result);
        Assert.True(result!.ReplacedExisting);
        storage.Verify(
            item => item.DeleteAsync(
                "househelps/1/old.png",
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task OpenPublicAsync_DoesNotOpenStorageForInactiveProfile()
    {
        await using var context = CreateContext();
        var houseHelp = CreateHouseHelp();
        houseHelp.IsActive = false;
        houseHelp.ProfileImageStorageKey = "househelps/1/profile.png";
        houseHelp.ProfileImageContentType = "image/png";
        context.HouseHelps.Add(houseHelp);
        await context.SaveChangesAsync();

        var storage = new Mock<IProfileImageStorage>();
        var service = CreateService(
            context,
            new Mock<IProfileImageProcessor>(),
            storage);

        var result = await service.OpenPublicAsync(houseHelp.Id);

        Assert.Null(result);
        storage.Verify(
            item => item.OpenReadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OpenPublicAsync_ReturnsNullWhenStoredFileIsMissing()
    {
        await using var context = CreateContext();
        var houseHelp = CreateHouseHelp();
        houseHelp.ProfileImageStorageKey = "househelps/1/missing.png";
        houseHelp.ProfileImageContentType = "image/png";
        context.HouseHelps.Add(houseHelp);
        await context.SaveChangesAsync();

        var storage = new Mock<IProfileImageStorage>();
        storage.Setup(item => item.OpenReadAsync(
                houseHelp.ProfileImageStorageKey,
                houseHelp.ProfileImageContentType,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException());
        var service = CreateService(
            context,
            new Mock<IProfileImageProcessor>(),
            storage);

        var result = await service.OpenPublicAsync(houseHelp.Id);

        Assert.Null(result);
    }

    private static HouseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HouseContext(options);
    }

    private static Mock<IProfileImageProcessor> CreateProcessorMock()
    {
        var processor = new Mock<IProfileImageProcessor>();
        processor.Setup(item => item.ProcessAsync(
                It.IsAny<ProfileImageUpload>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedProfileImage(
                [0x01, 0x02, 0x03],
                "image/png",
                ".png",
                3));
        return processor;
    }

    private static HouseHelpProfileImageService CreateService(
        HouseContext context,
        Mock<IProfileImageProcessor> processor,
        Mock<IProfileImageStorage> storage)
    {
        return new HouseHelpProfileImageService(
            context,
            processor.Object,
            storage.Object,
            Mock.Of<ILogger<HouseHelpProfileImageService>>());
    }

    private static HouseHelp CreateHouseHelp()
    {
        return new HouseHelp
        {
            FirstName = "Profile",
            LastName = "Coverage",
            Phone = "+256700000030",
            City = "Kampala"
        };
    }
}
