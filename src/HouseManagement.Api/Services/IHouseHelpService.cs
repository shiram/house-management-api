using System.Collections.Generic;
using System.Threading.Tasks;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IHouseHelpService
{
    Task<IEnumerable<HouseHelp>> GetAllAsync();
    Task<IEnumerable<HouseHelp>> GetFilteredAsync(string? city = null, string? skill = null, bool? isActive = null, int? page = null, int? pageSize = null, string? location = null, string? search = null);
    Task<HouseHelp?> GetByIdAsync(int id);
    Task<HouseHelp> CreateAsync(HouseHelp houseHelp, IEnumerable<string>? skills);
    Task<bool> UpdateAsync(HouseHelp houseHelp, IEnumerable<string>? skills);
    Task<bool> SetActiveAsync(int id, bool isActive);
}