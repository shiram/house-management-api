using HouseManagement.Api.DTOs;

namespace HouseManagement.Api.Services;

public interface IHouseHelpEarningsService
{
    Task<HouseHelpEarningsReportDto?> GetForHouseHelpAsync(int houseHelpId, DateTimeOffset? from = null, DateTimeOffset? to = null);
    Task<HouseHelpEarningsReportDto?> GetForUserAsync(int userId, DateTimeOffset? from = null, DateTimeOffset? to = null);
    Task<IReadOnlyList<HouseHelpEarningsSummaryDto>> GetSummariesAsync(DateTimeOffset? from = null, DateTimeOffset? to = null);
}
