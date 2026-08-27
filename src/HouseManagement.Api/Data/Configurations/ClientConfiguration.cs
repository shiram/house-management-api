using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.HasKey(client => client.Id);
        builder.Property(client => client.Name).HasMaxLength(200).IsRequired();
        builder.Property(client => client.Phone).HasMaxLength(32).IsRequired();
        builder.Property(client => client.Email).HasMaxLength(256);
        builder.HasOne(client => client.User)
            .WithMany()
            .HasForeignKey(client => client.UserId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(client => client.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        builder.HasIndex(client => client.Phone);
    }
}
