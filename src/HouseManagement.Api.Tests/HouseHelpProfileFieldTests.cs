using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseManagement.Api.Tests;

public class HouseHelpProfileFieldTests
{
    [Fact]
    public async Task HouseHelpProfileFields_ArePersisted()
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new HouseContext(options);
        context.HouseHelps.Add(new HouseHelp
        {
            FirstName = "Jane",
            LastName = "Helper",
            Phone = "+256700000001",
            City = "Kampala",
            Address = "Private address",
            Bio = "Experienced home care specialist.",
            YearsOfExperience = 5,
            Languages = "English,Luganda",
            EmergencyContactName = "Emergency Contact",
            EmergencyContactPhone = "+256700000002",
            NationalIdLast4 = "1234",
            VerificationStatus = HouseHelpVerificationStatus.PendingReview,
            ProfileImageStorageKey = "househelps/1/profile.jpg",
            ProfileImageContentType = "image/jpeg",
            ProfileImageSizeBytes = 1024,
            ProfileImageUpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var stored = await context.HouseHelps.SingleAsync();

        Assert.Equal("Experienced home care specialist.", stored.Bio);
        Assert.Equal(5, stored.YearsOfExperience);
        Assert.Equal("English,Luganda", stored.Languages);
        Assert.Equal("Emergency Contact", stored.EmergencyContactName);
        Assert.Equal("+256700000002", stored.EmergencyContactPhone);
        Assert.Equal("1234", stored.NationalIdLast4);
        Assert.Equal(HouseHelpVerificationStatus.PendingReview, stored.VerificationStatus);
        Assert.Equal("househelps/1/profile.jpg", stored.ProfileImageStorageKey);
        Assert.Equal("image/jpeg", stored.ProfileImageContentType);
        Assert.Equal(1024, stored.ProfileImageSizeBytes);
        Assert.NotNull(stored.ProfileImageUpdatedAt);
    }
}
