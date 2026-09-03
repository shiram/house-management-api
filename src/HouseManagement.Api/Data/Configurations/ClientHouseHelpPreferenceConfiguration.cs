using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class ClientHouseHelpPreferenceConfiguration : IEntityTypeConfiguration<ClientHouseHelpPreference>
{
    public void Configure(EntityTypeBuilder<ClientHouseHelpPreference> builder)
    {
        builder.HasKey(preference => preference.Id);

        builder.HasOne(preference => preference.Client)
            .WithMany()
            .HasForeignKey(preference => preference.ClientId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(preference => preference.HouseHelp)
            .WithMany()
            .HasForeignKey(preference => preference.HouseHelpId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(preference => new { preference.ClientId, preference.HouseHelpId }).IsUnique();
        builder.HasIndex(preference => preference.HouseHelpId);
    }
}
