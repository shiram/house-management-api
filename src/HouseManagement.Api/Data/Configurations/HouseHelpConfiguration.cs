using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class HouseHelpConfiguration : IEntityTypeConfiguration<HouseHelp>
{
    public void Configure(EntityTypeBuilder<HouseHelp> builder)
    {
        builder.HasKey(houseHelp => houseHelp.Id);
        builder.HasOne(houseHelp => houseHelp.User)
            .WithMany()
            .HasForeignKey(houseHelp => houseHelp.UserId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(houseHelp => houseHelp.UserId);
        builder.HasIndex(houseHelp => houseHelp.City);
        builder.HasIndex(houseHelp => houseHelp.IsActive);
    }
}
