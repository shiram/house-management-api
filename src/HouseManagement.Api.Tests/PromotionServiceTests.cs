using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class PromotionServiceTests
{
    [Fact]
    public async Task CreateAsync_NormalizesValuesAndRejectsDuplicateCode()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        var service = new PromotionService(context);

        var created = await service.CreateAsync(new Promotion
        {
            Code = " save10 ",
            Name = " Save 10 ",
            Description = " Intro discount ",
            DiscountType = PromotionDiscountType.Percentage,
            DiscountValue = 10,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            EndsAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        var duplicate = await service.CreateAsync(new Promotion
        {
            Code = "SAVE10",
            Name = "Duplicate",
            DiscountType = PromotionDiscountType.FixedAmount,
            DiscountValue = 5,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        Assert.NotNull(created.Promotion);
        Assert.Equal("SAVE10", created.Promotion!.Code);
        Assert.Equal("Save 10", created.Promotion.Name);
        Assert.Equal("Intro discount", created.Promotion.Description);
        Assert.Null(duplicate.Promotion);
        Assert.Contains("already exists", duplicate.Error);
    }

    [Fact]
    public async Task CreateAsync_ValidatesPercentageRangeDatesAndEligibleService()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        var service = new PromotionService(context);

        var tooLarge = await service.CreateAsync(new Promotion
        {
            Code = "TOOLARGE",
            Name = "Too Large",
            DiscountType = PromotionDiscountType.Percentage,
            DiscountValue = 101,
            StartsAt = DateTimeOffset.UtcNow
        });
        var invalidDates = await service.CreateAsync(new Promotion
        {
            Code = "DATES",
            Name = "Dates",
            DiscountType = PromotionDiscountType.FixedAmount,
            DiscountValue = 5,
            StartsAt = DateTimeOffset.UtcNow,
            EndsAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        var missingService = await service.CreateAsync(new Promotion
        {
            Code = "SERVICE",
            Name = "Service",
            DiscountType = PromotionDiscountType.FixedAmount,
            DiscountValue = 5,
            StartsAt = DateTimeOffset.UtcNow,
            EligibleServiceId = 999
        });

        Assert.Null(tooLarge.Promotion);
        Assert.Contains("cannot exceed", tooLarge.Error);
        Assert.Null(invalidDates.Promotion);
        Assert.Contains("end time", invalidDates.Error);
        Assert.Null(missingService.Promotion);
        Assert.Contains("eligible service", missingService.Error);
    }

    [Fact]
    public async Task SetActiveAsync_ChangesStatusAndTimestamp()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        var originalTimestamp = DateTimeOffset.UtcNow.AddMinutes(-1);
        context.Promotions.Add(new Promotion
        {
            Id = 1,
            Code = "SAVE",
            Name = "Save",
            DiscountType = PromotionDiscountType.FixedAmount,
            DiscountValue = 5,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAt = originalTimestamp,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new PromotionService(context);

        Assert.True(await service.SetActiveAsync(1, false));
        var stored = await context.Promotions.SingleAsync(promotion => promotion.Id == 1);
        Assert.False(stored.IsActive);
        Assert.True(stored.UpdatedAt > originalTimestamp);
        Assert.False(await service.SetActiveAsync(999, true));
    }
}
