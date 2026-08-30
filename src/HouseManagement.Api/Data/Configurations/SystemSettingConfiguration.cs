using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.HasKey(setting => setting.Id);
        builder.Property(setting => setting.Key).HasMaxLength(128).IsRequired();
        builder.Property(setting => setting.Value).HasMaxLength(2000).IsRequired();
        builder.Property(setting => setting.Description).HasMaxLength(500);
        builder.HasIndex(setting => setting.Key).IsUnique();
    }
}
