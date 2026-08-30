using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Action).HasMaxLength(128).IsRequired();
        builder.Property(log => log.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(log => log.Details).HasMaxLength(2000);

        builder.HasIndex(log => log.CreatedAt);
        builder.HasIndex(log => new { log.EntityType, log.EntityId });
        builder.HasIndex(log => log.UserId);
    }
}
