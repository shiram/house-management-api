using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class ServicePricingVersionConfiguration : IEntityTypeConfiguration<ServicePricingVersion>
{
    public void Configure(EntityTypeBuilder<ServicePricingVersion> builder)
    {
        builder.HasKey(version => version.Id);

        builder.Property(version => version.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(version => version.PricingMode).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(version => version.BasePrice).HasPrecision(18, 2);
        builder.Property(version => version.TimeBillingUnit).HasConversion<string>().HasMaxLength(16);
        builder.Property(version => version.TimeUnitPrice).HasPrecision(18, 2);
        builder.Property(version => version.TimeRoundingPolicy).HasConversion<string>().HasMaxLength(16);
        builder.Property(version => version.OvertimeUnitPrice).HasPrecision(18, 2);

        builder.HasOne(version => version.Service)
            .WithMany(service => service.PricingVersions)
            .HasForeignKey(version => version.ServiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(version => new { version.ServiceId, version.EffectiveFrom });

        // At most one open-ended (still effective) published version may exist per service at a time.
        // This is the database-enforced half of the "no overlapping published windows" invariant; the
        // other half (closing the prior version before publishing the next one) is enforced transactionally
        // in ServicePricingVersionService.
        builder.HasIndex(version => version.ServiceId)
            .IsUnique()
            .HasFilter("[Status] = 'Published' AND [EffectiveTo] IS NULL")
            .HasDatabaseName("IX_ServicePricingVersions_ServiceId_OpenPublished");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ServicePricingVersions_EffectiveWindow",
                "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]");
            table.HasCheckConstraint(
                "CK_ServicePricingVersions_BasePrice",
                "[BasePrice] IS NULL OR [BasePrice] > 0");
            table.HasCheckConstraint(
                "CK_ServicePricingVersions_TimeUnitPrice",
                "[TimeUnitPrice] IS NULL OR [TimeUnitPrice] > 0");
            table.HasCheckConstraint(
                "CK_ServicePricingVersions_MinimumDuration",
                "[MinimumBillableDurationMinutes] IS NULL OR [MinimumBillableDurationMinutes] > 0");
            table.HasCheckConstraint(
                "CK_ServicePricingVersions_BillingIncrement",
                "[BillingIncrementMinutes] IS NULL OR [BillingIncrementMinutes] > 0");
            table.HasCheckConstraint(
                "CK_ServicePricingVersions_Overtime",
                "([OvertimeThresholdMinutes] IS NULL AND [OvertimeUnitPrice] IS NULL) OR " +
                "([OvertimeThresholdMinutes] IS NOT NULL AND [OvertimeUnitPrice] IS NOT NULL " +
                "AND [OvertimeThresholdMinutes] >= [MinimumBillableDurationMinutes] AND [OvertimeUnitPrice] > 0)");
            table.HasCheckConstraint(
                "CK_ServicePricingVersions_Status",
                "[Status] IN ('Draft', 'Published')");
            table.HasCheckConstraint(
                "CK_ServicePricingVersions_PricingMode",
                "[PricingMode] IN ('Fixed', 'PerUnit', 'TimeBased')");
        });
    }
}
