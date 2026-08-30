using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Type).HasMaxLength(64).IsRequired();
        builder.Property(notification => notification.Title).HasMaxLength(200).IsRequired();
        builder.Property(notification => notification.Message).HasMaxLength(1000).IsRequired();
        builder.Property(notification => notification.RelatedEntityType).HasMaxLength(128);

        builder.HasIndex(notification => notification.UserId);
        builder.HasIndex(notification => new { notification.UserId, notification.IsRead });
        builder.HasIndex(notification => notification.CreatedAt);
    }
}
