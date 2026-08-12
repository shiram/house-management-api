using Microsoft.EntityFrameworkCore;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Data;

public class HouseContext : DbContext
{
    public HouseContext(DbContextOptions<HouseContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enforce uniqueness at the database level for email and username.
        // This will be created via EF Core migrations (AddUserUniqueIndexes).
        modelBuilder.Entity<User>(b =>
        {
            b.HasIndex(u => u.Email).IsUnique();
            b.HasIndex(u => u.UserName).IsUnique();

            // Configure string lengths if desired (keeps schema consistent)
            b.Property(u => u.Email).HasMaxLength(256).IsRequired();
            b.Property(u => u.UserName).HasMaxLength(128).IsRequired();
        });
    }
    public DbSet<HouseHelp> HouseHelps { get; set; }
    public DbSet<HouseHelpSkill> HouseHelpSkills { get; set; }
}
