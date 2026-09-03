using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class HouseHelpConfiguration : IEntityTypeConfiguration<HouseHelp>
{
    public void Configure(EntityTypeBuilder<HouseHelp> builder)
    {
        builder.HasKey(houseHelp => houseHelp.Id);
        builder.Property(houseHelp => houseHelp.Bio).HasMaxLength(1000);
        builder.Property(houseHelp => houseHelp.Languages).HasMaxLength(500);
        builder.Property(houseHelp => houseHelp.EmergencyContactName).HasMaxLength(200);
        builder.Property(houseHelp => houseHelp.EmergencyContactPhone).HasMaxLength(32);
        builder.Property(houseHelp => houseHelp.NationalIdLast4).HasMaxLength(4);
        builder.Property(houseHelp => houseHelp.VerificationStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(HouseHelpVerificationStatus.Unverified)
            .IsRequired();
        builder.Property(houseHelp => houseHelp.ProfileImageStorageKey).HasMaxLength(512);
        builder.Property(houseHelp => houseHelp.ProfileImageContentType).HasMaxLength(100);
        builder.HasOne(houseHelp => houseHelp.User)
            .WithMany()
            .HasForeignKey(houseHelp => houseHelp.UserId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(houseHelp => houseHelp.UserId);
        builder.HasIndex(houseHelp => houseHelp.City);
        builder.HasIndex(houseHelp => houseHelp.IsActive);
        builder.HasIndex(houseHelp => houseHelp.VerificationStatus);
    }
}
