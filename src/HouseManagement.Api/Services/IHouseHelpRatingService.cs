using HouseManagement.Api.Models;

namespace HouseManagement.Api.Services;

public interface IHouseHelpRatingService
{
    Task<HouseHelpRatingCreationResult> CreateAsync(int clientUserId, int bookingId, int score, string? comment);
    Task<IReadOnlyList<HouseHelpRating>> GetForHouseHelpAsync(int houseHelpId, int? page = null, int? pageSize = null);
    Task<HouseHelpRatingSummary> GetSummaryAsync(int houseHelpId);
}

public sealed record HouseHelpRatingCreationResult(HouseHelpRating? Rating, string? Error);
public sealed record HouseHelpRatingSummary(double? AverageScore, int RatingCount);
