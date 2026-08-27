using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class HouseHelpAvailabilityConfiguration : IEntityTypeConfiguration<HouseHelpAvailability>
{
    public void Configure(EntityTypeBuilder<HouseHelpAvailability> builder)
    {
        builder.HasKey(availability => availability.Id);
        builder.Property(availability => availability.StartTime).HasColumnType("time");
        builder.Property(availability => availability.EndTime).HasColumnType("time");
        builder.HasOne(availability => availability.HouseHelp)
            .WithMany(houseHelp => houseHelp.Availabilities)
            .HasForeignKey(availability => availability.HouseHelpId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(availability => new { availability.HouseHelpId, availability.DayOfWeek });
    }
}
