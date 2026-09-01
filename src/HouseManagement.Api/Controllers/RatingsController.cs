using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class RatingsController : ControllerBase
{
    private readonly IHouseHelpRatingService _ratings;

    public RatingsController(IHouseHelpRatingService ratings)
    {
        _ratings = ratings;
    }

    [Authorize]
    [HttpPost("bookings/{id:int}/rating")]
    public async Task<IActionResult> RateBooking(int id, [FromBody] CreateHouseHelpRatingRequest request)
    {
        if (!ModelState.IsValid) return ValidationResponseFactory.Create(this, ModelState);

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(subject, out var userId))
        {
            return Unauthorized();
        }

        var result = await _ratings.CreateAsync(userId, id, request.Score, request.Comment);
        if (result.Rating == null)
        {
            if (result.Error == "The requested booking was not found.")
            {
                return NotFound();
            }

            return BadRequest(ApiResponseFactory.Create<object?>(this, null, result.Error!, StatusCodes.Status400BadRequest));
        }

        var response = ApiResponseFactory.Create(this, ToDto(result.Rating), "Rating submitted", StatusCodes.Status201Created);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [AllowAnonymous]
    [HttpGet("househelps/{id:int}/ratings")]
    public async Task<IActionResult> GetForHouseHelp(int id, [FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var ratings = await _ratings.GetForHouseHelpAsync(id, page, pageSize);
        var response = ApiResponseFactory.Create(this, ratings.Select(ToDto), "HouseHelp ratings retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("househelps/{id:int}/ratings/summary")]
    public async Task<IActionResult> GetSummaryForHouseHelp(int id)
    {
        var summary = await _ratings.GetSummaryAsync(id);
        var dto = new HouseHelpRatingSummaryDto
        {
            HouseHelpId = id,
            AverageScore = summary.AverageScore,
            RatingCount = summary.RatingCount
        };
        var response = ApiResponseFactory.Create(this, dto, "HouseHelp rating summary retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    private static HouseHelpRatingDto ToDto(Models.HouseHelpRating rating)
    {
        return new HouseHelpRatingDto
        {
            Id = rating.Id,
            BookingId = rating.BookingId,
            HouseHelpId = rating.HouseHelpId,
            Score = rating.Score,
            Comment = rating.Comment,
            ClientDisplayName = FirstNameOnly(rating.Client?.Name),
            CreatedAt = rating.CreatedAt
        };
    }

    private static string? FirstNameOnly(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return null;
        var trimmed = fullName.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        return spaceIndex > 0 ? trimmed[..spaceIndex] : trimmed;
    }
}
