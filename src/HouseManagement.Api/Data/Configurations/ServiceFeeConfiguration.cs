using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class ServiceFeeConfiguration : IEntityTypeConfiguration<ServiceFee>
{
    public void Configure(EntityTypeBuilder<ServiceFee> builder)
    {
        builder.HasKey(fee => fee.Id);
        builder.Property(fee => fee.Name).HasMaxLength(128).IsRequired();
        builder.Property(fee => fee.AdjustmentType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(fee => fee.Amount).HasPrecision(18, 2);

        builder.HasOne(fee => fee.Service)
            .WithMany(service => service.Fees)
            .HasForeignKey(fee => fee.ServiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(fee => new { fee.ServiceId, fee.IsActive });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ServiceFees_AdjustmentType",
                "[AdjustmentType] IN ('Percentage', 'FixedAmount')");
            table.HasCheckConstraint(
                "CK_ServiceFees_Amount",
                "([AdjustmentType] = 'FixedAmount' AND [Amount] > 0) OR " +
                "([AdjustmentType] = 'Percentage' AND [Amount] > 0 AND [Amount] <= 100)");
        });
    }
}
