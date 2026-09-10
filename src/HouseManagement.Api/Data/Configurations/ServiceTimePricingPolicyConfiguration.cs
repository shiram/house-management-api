using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class ServiceTimePricingPolicyConfiguration : IEntityTypeConfiguration<ServiceTimePricingPolicy>
{
    public void Configure(EntityTypeBuilder<ServiceTimePricingPolicy> builder)
    {
        builder.HasKey(policy => policy.Id);
        builder.Property(policy => policy.BillingUnit)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(policy => policy.UnitPrice).HasPrecision(18, 2);
        builder.Property(policy => policy.RoundingPolicy)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(policy => policy.OvertimeUnitPrice).HasPrecision(18, 2);

        builder.HasOne(policy => policy.Service)
            .WithOne(service => service.TimePricingPolicy)
            .HasForeignKey<ServiceTimePricingPolicy>(policy => policy.ServiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(policy => policy.ServiceId).IsUnique();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ServiceTimePricingPolicies_UnitPrice",
                "[UnitPrice] > 0");
            table.HasCheckConstraint(
                "CK_ServiceTimePricingPolicies_MinimumDuration",
                "[MinimumBillableDurationMinutes] > 0");
            table.HasCheckConstraint(
                "CK_ServiceTimePricingPolicies_BillingIncrement",
                "[BillingIncrementMinutes] > 0");
            table.HasCheckConstraint(
                "CK_ServiceTimePricingPolicies_BillingUnit",
                "[BillingUnit] IN ('Minute', 'Hour', 'Day')");
            table.HasCheckConstraint(
                "CK_ServiceTimePricingPolicies_Overtime",
                "([OvertimeThresholdMinutes] IS NULL AND [OvertimeUnitPrice] IS NULL) OR " +
                "([OvertimeThresholdMinutes] IS NOT NULL AND [OvertimeUnitPrice] IS NOT NULL " +
                "AND [OvertimeThresholdMinutes] >= [MinimumBillableDurationMinutes] AND [OvertimeUnitPrice] > 0)");
            table.HasCheckConstraint(
                "CK_ServiceTimePricingPolicies_RoundingPolicy",
                "[RoundingPolicy] IN ('None', 'Up', 'Down', 'Nearest')");
        });
    }
}
