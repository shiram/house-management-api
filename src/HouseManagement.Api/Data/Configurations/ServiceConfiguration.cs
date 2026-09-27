using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.HasKey(service => service.Id);
        builder.Property(service => service.Code).HasMaxLength(64).IsRequired();
        builder.Property(service => service.Name).HasMaxLength(128).IsRequired();
        builder.Property(service => service.Description).HasMaxLength(1000);
        builder.Property(service => service.BasePrice).HasPrecision(18, 2);
        builder.Property(service => service.PricingMode).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(service => service.IsTaxable).HasDefaultValue(true);
        builder.HasIndex(service => service.Code).IsUnique();
        builder.HasIndex(service => service.IsActive);
    }
}
