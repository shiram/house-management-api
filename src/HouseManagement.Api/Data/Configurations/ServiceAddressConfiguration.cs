using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class ServiceAddressConfiguration : IEntityTypeConfiguration<ServiceAddress>
{
    public void Configure(EntityTypeBuilder<ServiceAddress> builder)
    {
        builder.HasKey(address => address.Id);
        builder.Property(address => address.Line1).HasMaxLength(250).IsRequired();
        builder.Property(address => address.Line2).HasMaxLength(250);
        builder.Property(address => address.City).HasMaxLength(100).IsRequired();
        builder.Property(address => address.Region).HasMaxLength(100);
        builder.Property(address => address.PostalCode).HasMaxLength(32);
        builder.Property(address => address.Country).HasMaxLength(100).IsRequired();
        builder.HasIndex(address => address.City);
    }
}
