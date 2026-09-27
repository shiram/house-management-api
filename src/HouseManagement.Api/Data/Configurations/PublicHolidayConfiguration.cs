using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>
{
    public void Configure(EntityTypeBuilder<PublicHoliday> builder)
    {
        builder.HasKey(holiday => holiday.Id);
        builder.Property(holiday => holiday.Name).HasMaxLength(128).IsRequired();
        builder.Property(holiday => holiday.Date).IsRequired();

        builder.HasIndex(holiday => holiday.Date).IsUnique();
    }
}
