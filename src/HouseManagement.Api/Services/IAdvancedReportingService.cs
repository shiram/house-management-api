using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IAdvancedReportingService
{
    Task<AdvancedReportSummaryDto> GetSummaryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null);
    Task<string> ExportBookingsCsvAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, BookingStatus? status = null);
}
