using HouseManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseManagement.Api.Data.Configurations;

public sealed class HouseHelpSkillConfiguration : IEntityTypeConfiguration<HouseHelpSkill>
{
    public void Configure(EntityTypeBuilder<HouseHelpSkill> builder)
    {
        builder.HasKey(skill => skill.Id);
        builder.HasOne(skill => skill.HouseHelp)
            .WithMany(houseHelp => houseHelp.Skills)
            .HasForeignKey(skill => skill.HouseHelpId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(skill => skill.HouseHelpId);
        builder.HasIndex(skill => new { skill.HouseHelpId, skill.ServiceName }).IsUnique();
        builder.Property(skill => skill.ServiceName).HasMaxLength(128).IsRequired();
    }
}
