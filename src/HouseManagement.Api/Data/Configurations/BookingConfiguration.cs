using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.Reference).HasMaxLength(32).IsRequired();
        builder.Property(booking => booking.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(booking => booking.Notes).HasMaxLength(2000);
        builder.Property(booking => booking.AppliedPromotionCode).HasMaxLength(64);
        builder.Property(booking => booking.AppliedPromotionName).HasMaxLength(128);
        builder.Property(booking => booking.DiscountAmount).HasPrecision(18, 2);
        builder.Property(booking => booking.TaxRatePercentage).HasPrecision(5, 2);
        builder.Property(booking => booking.TaxAmount).HasPrecision(18, 2);
        builder.Property(booking => booking.TotalPrice).HasPrecision(18, 2);
        builder.Property(booking => booking.Currency)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue(HouseManagement.Api.Common.PricingSettings.DefaultCurrencyCode);

        builder.HasOne(booking => booking.Service)
            .WithMany(service => service.Bookings)
            .HasForeignKey(booking => booking.ServiceId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(booking => booking.Client)
            .WithMany(client => client.Bookings)
            .HasForeignKey(booking => booking.ClientId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(booking => booking.AssignedHouseHelp)
            .WithMany()
            .HasForeignKey(booking => booking.AssignedHouseHelpId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(booking => booking.ServiceAddress)
            .WithMany(address => address.Bookings)
            .HasForeignKey(booking => booking.ServiceAddressId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(booking => booking.Reference).IsUnique();
        builder.HasIndex(booking => booking.Status);
        builder.HasIndex(booking => booking.ClientId);
        builder.HasIndex(booking => booking.AssignedHouseHelpId);
        builder.HasIndex(booking => new { booking.AssignedHouseHelpId, booking.ScheduledStart, booking.ScheduledEnd });
        builder.HasIndex(booking => booking.CreatedAt);
    }
}
