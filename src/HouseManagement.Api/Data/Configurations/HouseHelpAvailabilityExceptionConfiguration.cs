using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class HouseHelpAvailabilityExceptionConfiguration : IEntityTypeConfiguration<HouseHelpAvailabilityException>
{
    public void Configure(EntityTypeBuilder<HouseHelpAvailabilityException> builder)
    {
        builder.HasKey(exception => exception.Id);
        builder.Property(exception => exception.Reason).HasMaxLength(500);
        builder.HasOne(exception => exception.HouseHelp)
            .WithMany(houseHelp => houseHelp.AvailabilityExceptions)
            .HasForeignKey(exception => exception.HouseHelpId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(exception => new { exception.HouseHelpId, exception.StartsAt, exception.EndsAt });
        builder.HasIndex(exception => new { exception.HouseHelpId, exception.IsActive });
    }
}
