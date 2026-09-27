using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class ServiceSurchargeConfiguration : IEntityTypeConfiguration<ServiceSurcharge>
{
    public void Configure(EntityTypeBuilder<ServiceSurcharge> builder)
    {
        builder.HasKey(surcharge => surcharge.Id);
        builder.Property(surcharge => surcharge.Name).HasMaxLength(128).IsRequired();
        builder.Property(surcharge => surcharge.TriggerType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(surcharge => surcharge.AdjustmentType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(surcharge => surcharge.Amount).HasPrecision(18, 2);
        builder.Property(surcharge => surcharge.LocationMatch).HasMaxLength(128);

        builder.HasOne(surcharge => surcharge.Service)
            .WithMany(service => service.Surcharges)
            .HasForeignKey(surcharge => surcharge.ServiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(surcharge => new { surcharge.ServiceId, surcharge.IsActive });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ServiceSurcharges_TriggerType",
                "[TriggerType] IN ('Weekend', 'Holiday', 'AfterHours', 'Urgent', 'Location')");
            table.HasCheckConstraint(
                "CK_ServiceSurcharges_AdjustmentType",
                "[AdjustmentType] IN ('Percentage', 'FixedAmount')");
            table.HasCheckConstraint(
                "CK_ServiceSurcharges_Amount",
                "([AdjustmentType] = 'FixedAmount' AND [Amount] > 0) OR " +
                "([AdjustmentType] = 'Percentage' AND [Amount] > 0 AND [Amount] <= 100)");
            // Trigger-specific fields must be present only for their matching trigger type, and
            // null for every other trigger, so a surcharge cannot be configured ambiguously.
            table.HasCheckConstraint(
                "CK_ServiceSurcharges_TriggerFields",
                "([TriggerType] = 'AfterHours' AND [AfterHoursStartMinutes] BETWEEN 0 AND 1439 AND [AfterHoursEndMinutes] BETWEEN 0 AND 1439 " +
                "AND [UrgentLeadTimeMinutes] IS NULL AND [LocationMatch] IS NULL) OR " +
                "([TriggerType] = 'Urgent' AND [UrgentLeadTimeMinutes] > 0 " +
                "AND [AfterHoursStartMinutes] IS NULL AND [AfterHoursEndMinutes] IS NULL AND [LocationMatch] IS NULL) OR " +
                "([TriggerType] = 'Location' AND [LocationMatch] IS NOT NULL " +
                "AND [AfterHoursStartMinutes] IS NULL AND [AfterHoursEndMinutes] IS NULL AND [UrgentLeadTimeMinutes] IS NULL) OR " +
                "([TriggerType] IN ('Weekend', 'Holiday') " +
                "AND [AfterHoursStartMinutes] IS NULL AND [AfterHoursEndMinutes] IS NULL AND [UrgentLeadTimeMinutes] IS NULL AND [LocationMatch] IS NULL)");
        });
    }
}
