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

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HouseContext).Assembly);
    }
    public DbSet<HouseHelp> HouseHelps { get; set; }
    public DbSet<HouseHelpSkill> HouseHelpSkills { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<ServiceAddress> ServiceAddresses { get; set; }
    public DbSet<HouseHelpAvailability> HouseHelpAvailabilities { get; set; }
    public DbSet<HouseHelpAvailabilityException> HouseHelpAvailabilityExceptions { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Notification> Notifications { get; set; }
}
