using System.Collections.Generic;
using System.Threading.Tasks;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IHouseHelpService
{
    Task<IEnumerable<HouseHelp>> GetAllAsync();
    Task<HouseHelp?> GetByIdAsync(int id);
    Task<HouseHelp> CreateAsync(HouseHelp houseHelp, IEnumerable<string>? skills);
    Task<bool> UpdateAsync(HouseHelp houseHelp, IEnumerable<string>? skills);
    Task<bool> SetActiveAsync(int id, bool isActive);
}