using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.HasKey(promotion => promotion.Id);
        builder.Property(promotion => promotion.Code).HasMaxLength(64).IsRequired();
        builder.Property(promotion => promotion.Name).HasMaxLength(128).IsRequired();
        builder.Property(promotion => promotion.Description).HasMaxLength(1000);
        builder.Property(promotion => promotion.DiscountType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(promotion => promotion.DiscountValue).HasPrecision(18, 2);

        builder.HasOne(promotion => promotion.EligibleService)
            .WithMany()
            .HasForeignKey(promotion => promotion.EligibleServiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(promotion => promotion.Code).IsUnique();
        builder.HasIndex(promotion => promotion.IsActive);
        builder.HasIndex(promotion => new { promotion.StartsAt, promotion.EndsAt });
        builder.HasIndex(promotion => promotion.EligibleServiceId);
    }
}
