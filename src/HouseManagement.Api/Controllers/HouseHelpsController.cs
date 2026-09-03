using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Models;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HouseHelpsController : ControllerBase
{
    private readonly IHouseHelpService _svc;

    public HouseHelpsController(IHouseHelpService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? city, [FromQuery] string? skill, [FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var items = await _svc.GetFilteredAsync(city, skill, true, page, pageSize);
        var dtos = items.Select(ToPublicDto);
        var response = ApiResponseFactory.Create(this, dtos, "HouseHelp directory retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var h = await _svc.GetByIdAsync(id);
        if (h == null || !h.IsActive) return NotFound();

        var response = ApiResponseFactory.Create(this, ToPublicDto(h), "HouseHelp retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.HouseHelpOnly)]
    [HttpGet("me/profile")]
    public async Task<IActionResult> GetOwnProfile()
    {
        if (!TryGetAuthenticatedUserId(out var userId)) return Unauthorized();

        var houseHelp = await _svc.GetByUserIdAsync(userId);
        if (houseHelp == null) return NotFound();

        var response = ApiResponseFactory.Create(this, ToOwnProfileDto(houseHelp), "HouseHelp profile retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.HouseHelpOnly)]
    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateOwnProfile([FromBody] UpdateOwnHouseHelpProfileRequest req)
    {
        if (!ModelState.IsValid) return ValidationResponseFactory.Create(this, ModelState);
        if (!TryGetAuthenticatedUserId(out var userId)) return Unauthorized();

        var existing = await _svc.GetByUserIdAsync(userId);
        if (existing == null) return NotFound();

        existing.FirstName = req.FirstName;
        existing.LastName = req.LastName;
        existing.Phone = req.Phone;
        existing.City = req.City;
        existing.Address = req.Address;
        existing.Bio = req.Bio;
        existing.YearsOfExperience = req.YearsOfExperience;
        existing.Languages = req.Languages;

        var ok = await _svc.UpdateOwnProfileAsync(existing);
        if (!ok) return NotFound();

        var updated = await _svc.GetByUserIdAsync(userId);
        var response = ApiResponseFactory.Create(this, ToOwnProfileDto(updated!), "HouseHelp profile updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    private static PublicHouseHelpDto ToPublicDto(HouseHelp houseHelp)
    {
        return new PublicHouseHelpDto
        {
            Id = houseHelp.Id,
            FirstName = houseHelp.FirstName,
            LastName = houseHelp.LastName,
            City = houseHelp.City,
            Skills = houseHelp.Skills.Select(s => s.ServiceName)
        };
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHouseHelpRequest req)
    {
        if (!ModelState.IsValid) return ValidationResponseFactory.Create(this, ModelState);

        var entity = new HouseHelp
        {
            UserId = req.UserId,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Phone = req.Phone,
            City = req.City,
            Address = req.Address,
            IsActive = true
        };
        var created = await _svc.CreateAsync(entity, req.Skills);
        var dto = new HouseManagement.Api.DTOs.HouseHelpDto
        {
            Id = created.Id,
            UserId = created.UserId,
            FirstName = created.FirstName,
            LastName = created.LastName,
            Phone = created.Phone,
            City = created.City,
            Address = created.Address,
            IsActive = created.IsActive,
            Skills = created.Skills.Select(s => s.ServiceName)
        };
        var response = ApiResponseFactory.Create(this, dto, "HouseHelp created", StatusCodes.Status201Created);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHouseHelpRequest req)
    {
        if (!ModelState.IsValid) return ValidationResponseFactory.Create(this, ModelState);

        var existing = await _svc.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.FirstName = req.FirstName;
        existing.LastName = req.LastName;
        existing.Phone = req.Phone;
        existing.City = req.City;
        existing.Address = req.Address;

        var ok = await _svc.UpdateAsync(existing, req.Skills);
        if (!ok) return NotFound();

        var response = ApiResponseFactory.Create<object?>(this, null, "HouseHelp updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{id}/profile")]
    public async Task<IActionResult> UpdateProfile(int id, [FromBody] UpdateHouseHelpProfileRequest req)
    {
        if (!Enum.IsDefined(req.VerificationStatus))
        {
            ModelState.AddModelError(nameof(req.VerificationStatus), "Verification status is not supported.");
        }

        if (!ModelState.IsValid) return ValidationResponseFactory.Create(this, ModelState);

        var existing = await _svc.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.FirstName = req.FirstName;
        existing.LastName = req.LastName;
        existing.Phone = req.Phone;
        existing.City = req.City;
        existing.Address = req.Address;
        existing.Bio = req.Bio;
        existing.YearsOfExperience = req.YearsOfExperience;
        existing.Languages = req.Languages;
        existing.EmergencyContactName = req.EmergencyContactName;
        existing.EmergencyContactPhone = req.EmergencyContactPhone;
        existing.NationalIdLast4 = req.NationalIdLast4;
        existing.VerificationStatus = req.VerificationStatus;

        var ok = await _svc.UpdateProfileAsync(existing);
        if (!ok) return NotFound();

        var updated = await _svc.GetByIdAsync(id);
        var response = ApiResponseFactory.Create(this, ToProfileDto(updated!), "HouseHelp profile updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{id}/activate")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool active = true)
    {
        var ok = await _svc.SetActiveAsync(id, active);
        if (!ok) return NotFound();

        var response = ApiResponseFactory.Create<object?>(this, null, "HouseHelp status updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    private static HouseHelpProfileDto ToProfileDto(HouseHelp houseHelp)
    {
        return new HouseHelpProfileDto
        {
            Id = houseHelp.Id,
            UserId = houseHelp.UserId,
            FirstName = houseHelp.FirstName,
            LastName = houseHelp.LastName,
            Phone = houseHelp.Phone,
            City = houseHelp.City,
            Address = houseHelp.Address,
            Bio = houseHelp.Bio,
            YearsOfExperience = houseHelp.YearsOfExperience,
            Languages = houseHelp.Languages,
            EmergencyContactName = houseHelp.EmergencyContactName,
            EmergencyContactPhone = houseHelp.EmergencyContactPhone,
            NationalIdLast4 = houseHelp.NationalIdLast4,
            VerificationStatus = houseHelp.VerificationStatus.ToString(),
            ProfileImageContentType = houseHelp.ProfileImageContentType,
            ProfileImageSizeBytes = houseHelp.ProfileImageSizeBytes,
            ProfileImageUpdatedAt = houseHelp.ProfileImageUpdatedAt,
            IsActive = houseHelp.IsActive,
            Skills = houseHelp.Skills.Select(s => s.ServiceName)
        };
    }

    private static OwnHouseHelpProfileDto ToOwnProfileDto(HouseHelp houseHelp)
    {
        return new OwnHouseHelpProfileDto
        {
            Id = houseHelp.Id,
            FirstName = houseHelp.FirstName,
            LastName = houseHelp.LastName,
            Phone = houseHelp.Phone,
            City = houseHelp.City,
            Address = houseHelp.Address,
            Bio = houseHelp.Bio,
            YearsOfExperience = houseHelp.YearsOfExperience,
            Languages = houseHelp.Languages,
            VerificationStatus = houseHelp.VerificationStatus.ToString(),
            ProfileImageContentType = houseHelp.ProfileImageContentType,
            ProfileImageSizeBytes = houseHelp.ProfileImageSizeBytes,
            ProfileImageUpdatedAt = houseHelp.ProfileImageUpdatedAt,
            IsActive = houseHelp.IsActive,
            Skills = houseHelp.Skills.Select(s => s.ServiceName)
        };
    }

    private bool TryGetAuthenticatedUserId(out int userId)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(subject, out userId);
    }
}
