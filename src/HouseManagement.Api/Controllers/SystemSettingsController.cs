using HouseManagement.Api.Common.Api;
using HouseManagement.Api.Common;
using HouseManagement.Api.Common.Security;
using HouseManagement.Api.DTOs;
using HouseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HouseManagement.Api.Controllers;

[ApiController]
[Route("api/admin/settings")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class SystemSettingsController : ControllerBase
{
    private readonly ISystemSettingsService _settings;
    private readonly IAuditLogService _auditLogs;

    public SystemSettingsController(ISystemSettingsService settings, IAuditLogService auditLogs)
    {
        _settings = settings;
        _auditLogs = auditLogs;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? page, [FromQuery] int? pageSize)
    {
        var settings = await _settings.GetAllAsync(page, pageSize);
        var dtos = settings.Select(ToDto);

        var response = ApiResponseFactory.Create(this, dtos, "System settings retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key)
    {
        var setting = await _settings.GetByKeyAsync(key);
        if (setting == null) return NotFound();

        var response = ApiResponseFactory.Create(this, ToDto(setting), "System setting retrieved", StatusCodes.Status200OK);
        return Ok(response);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Upsert(string key, [FromBody] UpsertSystemSettingRequest request)
    {
        if (!ModelState.IsValid) return ValidationResponseFactory.Create(this, ModelState);

        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(ApiResponseFactory.Create<object?>(this, null, "A valid setting key is required.", StatusCodes.Status400BadRequest));
        }

        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        int? updatedByUserId = int.TryParse(subject, out var parsedUserId) ? parsedUserId : null;

        var setting = await _settings.UpsertAsync(key, request.Value, request.Description, updatedByUserId);
        await _auditLogs.LogAsync(
            AuditEventTypes.SystemSettingUpdated,
            nameof(Models.SystemSetting),
            entityId: setting.Id,
            userId: updatedByUserId);

        var response = ApiResponseFactory.Create(this, ToDto(setting), "System setting saved", StatusCodes.Status200OK);
        return Ok(response);
    }

    private static SystemSettingDto ToDto(Models.SystemSetting setting)
    {
        return new SystemSettingDto
        {
            Id = setting.Id,
            Key = setting.Key,
            Value = setting.Value,
            Description = setting.Description,
            CreatedAt = setting.CreatedAt,
            UpdatedAt = setting.UpdatedAt
        };
    }
}
