using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class HouseHelpRatingConfiguration : IEntityTypeConfiguration<HouseHelpRating>
{
    public void Configure(EntityTypeBuilder<HouseHelpRating> builder)
    {
        builder.HasKey(rating => rating.Id);
        builder.Property(rating => rating.Score).IsRequired();
        builder.Property(rating => rating.Comment).HasMaxLength(1000);
        builder.ToTable(table => table.HasCheckConstraint("CK_HouseHelpRatings_Score", "[Score] BETWEEN 1 AND 5"));

        builder.HasOne(rating => rating.Booking)
            .WithMany()
            .HasForeignKey(rating => rating.BookingId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(rating => rating.HouseHelp)
            .WithMany()
            .HasForeignKey(rating => rating.HouseHelpId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(rating => rating.Client)
            .WithMany()
            .HasForeignKey(rating => rating.ClientId)
            .OnDelete(DeleteBehavior.NoAction);

        // Exactly one rating per booking.
        builder.HasIndex(rating => rating.BookingId).IsUnique();
        builder.HasIndex(rating => rating.HouseHelpId);
        builder.HasIndex(rating => rating.CreatedAt);
    }
}
