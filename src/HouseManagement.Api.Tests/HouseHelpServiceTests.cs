using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using HouseManagement.Api.Data;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;

namespace HouseManagement.Api.Tests;

public class HouseHelpServiceTests
{
    private HouseContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<HouseContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new HouseContext(options);
    }

    [Fact]
    public async Task CreateHouseHelp_SavesHouseHelpAndSkills()
    {
        using var ctx = CreateContext("create_test");
        var svc = new HouseHelpService(ctx);

        var hh = new HouseHelp { FirstName = "Ana", LastName = "Doe", Phone = "+123456789", City = "Nairobi" };
        var created = await svc.CreateAsync(hh, new [] { "Cleaning", "Laundry" });

        Assert.True(created.Id != 0);
        var saved = ctx.HouseHelps.Include(h => h.Skills).SingleOrDefault(h => h.Id == created.Id);
        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Skills.Count);
        Assert.Contains(saved.Skills, s => s.ServiceName == "Cleaning");
    }

    [Fact]
    public async Task UpdateHouseHelp_ReplacesSkills()
    {
        using var ctx = CreateContext("update_test");
        var svc = new HouseHelpService(ctx);

        var hh = new HouseHelp { FirstName = "Ben", LastName = "Smith", Phone = "+111222333", City = "Kisumu" };
        var created = await svc.CreateAsync(hh, new [] { "Cleaning", "Laundry" });

        created.FirstName = "Benjamin";
        var ok = await svc.UpdateAsync(created, new [] { "Cleaning", "Ironing" });
        Assert.True(ok);

        var saved = ctx.HouseHelps.Include(h => h.Skills).SingleOrDefault(h => h.Id == created.Id);
        Assert.NotNull(saved);
        Assert.Equal("Benjamin", saved!.FirstName);
        var skillNames = saved.Skills.Select(s => s.ServiceName).ToList();
        Assert.Contains("Cleaning", skillNames);
        Assert.Contains("Ironing", skillNames);
        Assert.DoesNotContain("Laundry", skillNames);
    }
}
