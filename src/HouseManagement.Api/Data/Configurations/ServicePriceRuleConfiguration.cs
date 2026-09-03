using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class ServicePriceRuleConfiguration : IEntityTypeConfiguration<ServicePriceRule>
{
    public void Configure(EntityTypeBuilder<ServicePriceRule> builder)
    {
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.UnitName).HasMaxLength(128).IsRequired();
        builder.Property(rule => rule.UnitPrice).HasPrecision(18, 2);

        builder.HasOne(rule => rule.Service)
            .WithMany(service => service.PriceRules)
            .HasForeignKey(rule => rule.ServiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(rule => new { rule.ServiceId, rule.UnitName }).IsUnique();
        builder.HasIndex(rule => new { rule.ServiceId, rule.IsActive });
    }
}
