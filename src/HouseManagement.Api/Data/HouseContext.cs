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
    public DbSet<HouseHelpRating> HouseHelpRatings { get; set; }
    public DbSet<ClientHouseHelpPreference> ClientHouseHelpPreferences { get; set; }
    public DbSet<ServicePriceRule> ServicePriceRules { get; set; }
    public DbSet<ServiceTimePricingPolicy> ServiceTimePricingPolicies { get; set; }
    public DbSet<ServicePricingVersion> ServicePricingVersions { get; set; }
    public DbSet<ServicePricingVersionUnit> ServicePricingVersionUnits { get; set; }
    public DbSet<BookingPriceLine> BookingPriceLines { get; set; }
    public DbSet<Promotion> Promotions { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<ServiceFee> ServiceFees { get; set; }
    public DbSet<ServiceSurcharge> ServiceSurcharges { get; set; }
    public DbSet<PublicHoliday> PublicHolidays { get; set; }
}
