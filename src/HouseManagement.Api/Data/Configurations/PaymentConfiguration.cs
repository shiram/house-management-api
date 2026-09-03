using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
        builder.Property(payment => payment.MethodType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(payment => payment.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(payment => payment.ProviderReference).HasMaxLength(128).IsRequired();
        builder.Property(payment => payment.ProviderCheckoutUrl).HasMaxLength(2048);
        builder.Property(payment => payment.FailureReason).HasMaxLength(512);
        builder.Property(payment => payment.IdempotencyKey).HasMaxLength(128).IsRequired();

        builder.HasOne(payment => payment.Booking)
            .WithMany(booking => booking.Payments)
            .HasForeignKey(payment => payment.BookingId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(payment => payment.BookingId);
        builder.HasIndex(payment => payment.Status);
        builder.HasIndex(payment => new { payment.ProviderName, payment.ProviderReference }).IsUnique();
        builder.HasIndex(payment => payment.IdempotencyKey).IsUnique();
    }
}
