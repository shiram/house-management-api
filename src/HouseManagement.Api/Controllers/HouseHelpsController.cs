using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using HouseManagement.Api.Common;
using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Infrastructure.Files;
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
    private readonly IHouseHelpProfileImageService _profileImageService;
    private readonly IAuditLogService _auditLogs;

    public HouseHelpsController(
        IHouseHelpService svc,
        IHouseHelpProfileImageService profileImageService,
        IAuditLogService auditLogs)
    {
        _svc = svc;
        _profileImageService = profileImageService;
        _auditLogs = auditLogs;
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

    [HttpGet("{id}/profile-image")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetProfileImage(
        int id,
        CancellationToken cancellationToken)
    {
        var image = await _profileImageService.OpenPublicAsync(id, cancellationToken);
        if (image == null) return NotFound();

        return File(image.Content, image.ContentType, enableRangeProcessing: true);
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

        var auditDetails = BuildProfileAuditDetails(
            "self-service",
            (nameof(HouseHelp.FirstName), !string.Equals(existing.FirstName, req.FirstName.Trim(), StringComparison.Ordinal)),
            (nameof(HouseHelp.LastName), !string.Equals(existing.LastName, req.LastName.Trim(), StringComparison.Ordinal)),
            (nameof(HouseHelp.Phone), !string.Equals(existing.Phone, req.Phone.Trim(), StringComparison.Ordinal)),
            (nameof(HouseHelp.City), !string.Equals(existing.City, req.City.Trim(), StringComparison.Ordinal)),
            (nameof(HouseHelp.Address), !string.Equals(existing.Address, NormalizeOptional(req.Address), StringComparison.Ordinal)),
            (nameof(HouseHelp.Bio), !string.Equals(existing.Bio, NormalizeOptional(req.Bio), StringComparison.Ordinal)),
            (nameof(HouseHelp.YearsOfExperience), existing.YearsOfExperience != req.YearsOfExperience),
            (nameof(HouseHelp.Languages), !string.Equals(existing.Languages, NormalizeOptional(req.Languages), StringComparison.Ordinal)));

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

        await _auditLogs.LogAsync(
            AuditEventTypes.HouseHelpProfileUpdated,
            nameof(HouseHelp),
            entityId: existing.Id,
            userId: userId,
            details: auditDetails);

        var updated = await _svc.GetByUserIdAsync(userId);
        var response = ApiResponseFactory.Create(this, ToOwnProfileDto(updated!), "HouseHelp profile updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.HouseHelpOnly)]
    [HttpPut("me/profile-image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProfileImageOptions.HardMaxMultipartBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfileImageOptions.HardMaxSizeBytes)]
    public async Task<IActionResult> ReplaceOwnProfileImage(
        [FromForm] UploadHouseHelpProfileImageRequest req,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength > ProfileImageOptions.HardMaxMultipartBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        if (!ModelState.IsValid) return ValidationResponseFactory.Create(this, ModelState);
        if (!TryGetAuthenticatedUserId(out var userId)) return Unauthorized();

        var existing = await _svc.GetByUserIdAsync(userId);
        if (existing == null) return NotFound();

        return await ReplaceProfileImageAsync(existing.Id, req.File!, true, userId, cancellationToken);
    }

    private static PublicHouseHelpDto ToPublicDto(HouseHelp houseHelp)
    {
        return new PublicHouseHelpDto
        {
            Id = houseHelp.Id,
            FirstName = houseHelp.FirstName,
            LastName = houseHelp.LastName,
            City = houseHelp.City,
            Bio = houseHelp.Bio,
            YearsOfExperience = houseHelp.YearsOfExperience,
            Languages = houseHelp.Languages,
            VerificationStatus = houseHelp.VerificationStatus == HouseHelpVerificationStatus.Verified
                ? nameof(HouseHelpVerificationStatus.Verified)
                : nameof(HouseHelpVerificationStatus.Unverified),
            ProfileImageUrl = BuildPublicProfileImageUrl(houseHelp),
            Skills = houseHelp.Skills.Select(s => s.ServiceName)
        };
    }

    private static string? BuildPublicProfileImageUrl(HouseHelp houseHelp)
    {
        if (string.IsNullOrWhiteSpace(houseHelp.ProfileImageStorageKey))
        {
            return null;
        }

        var version = houseHelp.ProfileImageUpdatedAt?.ToUnixTimeMilliseconds();
        return version.HasValue
            ? $"/api/househelps/{houseHelp.Id}/profile-image?v={version.Value}"
            : $"/api/househelps/{houseHelp.Id}/profile-image";
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
        if (!TryGetAuthenticatedUserId(out var actorUserId)) return Unauthorized();

        var existing = await _svc.GetByIdAsync(id);
        if (existing == null) return NotFound();

        var incomingSkills = (req.Skills ?? Enumerable.Empty<string>())
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .ToHashSet(StringComparer.Ordinal);
        var auditDetails = BuildProfileAuditDetails(
            "management",
            (nameof(HouseHelp.FirstName), !string.Equals(existing.FirstName, req.FirstName.Trim(), StringComparison.Ordinal)),
            (nameof(HouseHelp.LastName), !string.Equals(existing.LastName, req.LastName.Trim(), StringComparison.Ordinal)),
            (nameof(HouseHelp.Phone), !string.Equals(existing.Phone, req.Phone.Trim(), StringComparison.Ordinal)),
            (nameof(HouseHelp.City), !string.Equals(existing.City, req.City.Trim(), StringComparison.Ordinal)),
            (nameof(HouseHelp.Address), !string.Equals(existing.Address, NormalizeOptional(req.Address), StringComparison.Ordinal)),
            (nameof(HouseHelp.Skills), !incomingSkills.SetEquals(existing.Skills.Select(skill => skill.ServiceName))));

        existing.FirstName = req.FirstName;
        existing.LastName = req.LastName;
        existing.Phone = req.Phone;
        existing.City = req.City;
        existing.Address = req.Address;

        var ok = await _svc.UpdateAsync(existing, req.Skills);
        if (!ok) return NotFound();

        await _auditLogs.LogAsync(
            AuditEventTypes.HouseHelpProfileUpdated,
            nameof(HouseHelp),
            entityId: existing.Id,
            userId: actorUserId,
            details: auditDetails);

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
        if (!TryGetAuthenticatedUserId(out var actorUserId)) return Unauthorized();

        var existing = await _svc.GetByIdAsync(id);
        if (existing == null) return NotFound();

        var auditDetails = BuildProfileAuditDetails(
            "management",
            (nameof(HouseHelp.FirstName), !string.Equals(existing.FirstName, req.FirstName.Trim(), StringComparison.Ordinal)),
            (nameof(HouseHelp.LastName), !string.Equals(existing.LastName, req.LastName.Trim(), StringComparison.Ordinal)),
            (nameof(HouseHelp.Phone), !string.Equals(existing.Phone, req.Phone.Trim(), StringComparison.Ordinal)),
            (nameof(HouseHelp.City), !string.Equals(existing.City, req.City.Trim(), StringComparison.Ordinal)),
            (nameof(HouseHelp.Address), !string.Equals(existing.Address, NormalizeOptional(req.Address), StringComparison.Ordinal)),
            (nameof(HouseHelp.Bio), !string.Equals(existing.Bio, NormalizeOptional(req.Bio), StringComparison.Ordinal)),
            (nameof(HouseHelp.YearsOfExperience), existing.YearsOfExperience != req.YearsOfExperience),
            (nameof(HouseHelp.Languages), !string.Equals(existing.Languages, NormalizeOptional(req.Languages), StringComparison.Ordinal)),
            (nameof(HouseHelp.EmergencyContactName), !string.Equals(existing.EmergencyContactName, NormalizeOptional(req.EmergencyContactName), StringComparison.Ordinal)),
            (nameof(HouseHelp.EmergencyContactPhone), !string.Equals(existing.EmergencyContactPhone, NormalizeOptional(req.EmergencyContactPhone), StringComparison.Ordinal)),
            (nameof(HouseHelp.NationalIdLast4), !string.Equals(existing.NationalIdLast4, NormalizeOptional(req.NationalIdLast4), StringComparison.Ordinal)),
            (nameof(HouseHelp.VerificationStatus), existing.VerificationStatus != req.VerificationStatus));

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

        await _auditLogs.LogAsync(
            AuditEventTypes.HouseHelpProfileUpdated,
            nameof(HouseHelp),
            entityId: existing.Id,
            userId: actorUserId,
            details: auditDetails);

        var updated = await _svc.GetByIdAsync(id);
        var response = ApiResponseFactory.Create(this, ToProfileDto(updated!), "HouseHelp profile updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{id}/profile-image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProfileImageOptions.HardMaxMultipartBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfileImageOptions.HardMaxSizeBytes)]
    public Task<IActionResult> ReplaceProfileImage(
        int id,
        [FromForm] UploadHouseHelpProfileImageRequest req,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength > ProfileImageOptions.HardMaxMultipartBodyBytes)
        {
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status413PayloadTooLarge));
        }

        if (!ModelState.IsValid)
        {
            return Task.FromResult<IActionResult>(ValidationResponseFactory.Create(this, ModelState));
        }

        if (!TryGetAuthenticatedUserId(out var actorUserId))
        {
            return Task.FromResult<IActionResult>(Unauthorized());
        }

        return ReplaceProfileImageAsync(id, req.File!, false, actorUserId, cancellationToken);
    }

    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [HttpPut("{id}/activate")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool active = true)
    {
        if (!TryGetAuthenticatedUserId(out var actorUserId)) return Unauthorized();

        var existing = await _svc.GetByIdAsync(id);
        if (existing == null) return NotFound();

        var previousActive = existing.IsActive;
        var ok = await _svc.SetActiveAsync(id, active);
        if (!ok) return NotFound();

        await _auditLogs.LogAsync(
            AuditEventTypes.HouseHelpActivationChanged,
            nameof(HouseHelp),
            entityId: id,
            userId: actorUserId,
            details: $"{previousActive} -> {active}");

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

    private async Task<IActionResult> ReplaceProfileImageAsync(
        int houseHelpId,
        IFormFile file,
        bool ownProfile,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        HouseHelpProfileImageUpdateResult? result;
        try
        {
            await using var content = file.OpenReadStream();
            result = await _profileImageService.ReplaceAsync(
                houseHelpId,
                new ProfileImageUpload(file.FileName, file.ContentType, content),
                cancellationToken);
        }
        catch (ProfileImageValidationException exception)
        {
            foreach (var error in exception.Errors)
            {
                foreach (var message in error.Value)
                {
                    ModelState.AddModelError(error.Key, message);
                }
            }

            return ValidationResponseFactory.Create(this, ModelState);
        }

        if (result == null) return NotFound();

        await _auditLogs.LogAsync(
            AuditEventTypes.HouseHelpProfileImageUpdated,
            nameof(HouseHelp),
            entityId: result.HouseHelp.Id,
            userId: actorUserId,
            details: $"Scope: {(ownProfile ? "self-service" : "management")}; Operation: {(result.ReplacedExisting ? "replacement" : "upload")}");

        var data = ownProfile
            ? (object)ToOwnProfileDto(result.HouseHelp)
            : ToProfileDto(result.HouseHelp);
        var response = ApiResponseFactory.Create(this, data, "HouseHelp profile image updated", StatusCodes.Status200OK);
        return Ok(response);
    }

    private static string BuildProfileAuditDetails(
        string scope,
        params (string Field, bool Changed)[] fields)
    {
        var fieldList = string.Join(
            ",",
            fields.Where(field => field.Changed).Select(field => field.Field));
        return $"Scope: {scope}; Fields: {(fieldList.Length == 0 ? "none" : fieldList)}";
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
