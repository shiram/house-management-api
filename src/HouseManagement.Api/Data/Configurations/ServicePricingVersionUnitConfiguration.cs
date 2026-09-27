using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class ServicePricingVersionUnitConfiguration : IEntityTypeConfiguration<ServicePricingVersionUnit>
{
    public void Configure(EntityTypeBuilder<ServicePricingVersionUnit> builder)
    {
        builder.HasKey(unit => unit.Id);
        builder.Property(unit => unit.UnitName).HasMaxLength(64).IsRequired();
        builder.Property(unit => unit.UnitPrice).HasPrecision(18, 2);

        builder.HasOne(unit => unit.PricingVersion)
            .WithMany(version => version.Units)
            .HasForeignKey(unit => unit.ServicePricingVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(unit => new { unit.ServicePricingVersionId, unit.UnitName }).IsUnique();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ServicePricingVersionUnits_UnitPrice", "[UnitPrice] > 0");
        });
    }
}
