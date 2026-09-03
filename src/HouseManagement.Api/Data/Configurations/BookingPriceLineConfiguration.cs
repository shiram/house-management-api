using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class BookingPriceLineConfiguration : IEntityTypeConfiguration<BookingPriceLine>
{
    public void Configure(EntityTypeBuilder<BookingPriceLine> builder)
    {
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Description).HasMaxLength(128).IsRequired();
        builder.Property(line => line.UnitPrice).HasPrecision(18, 2);
        builder.Property(line => line.LineTotal).HasPrecision(18, 2);

        builder.HasOne(line => line.Booking)
            .WithMany(booking => booking.PriceLines)
            .HasForeignKey(line => line.BookingId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
