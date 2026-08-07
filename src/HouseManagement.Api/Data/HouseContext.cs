using Microsoft.EntityFrameworkCore;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Data;

public class HouseContext : DbContext
{
    public HouseContext(DbContextOptions<HouseContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
}
